using System;
using System.Collections.Generic;
using System.Threading;
using Broker.Core;
using Broker.Runtime.Dialogue;
using Broker.Runtime.Office;
using Broker.Runtime.Voice;
using UnityEngine;

namespace Broker.Runtime
{
    public sealed partial class PrototypeBootstrap : MonoBehaviour
    {
        private BrokerDay day;
        private readonly List<DialogueTurn>[] histories = { new List<DialogueTurn>(), new List<DialogueTurn>(), new List<DialogueTurn>() };
        private readonly List<string>[] conversations = { new List<string>(), new List<string>(), new List<string>() };
        private readonly List<string> officeNews = new List<string>();
        private CancellationTokenSource pending;
        private int revision;
        private bool busy, useGateway, voiceEnabled, lastDayEnded, showNews, intro = true;
        private Vector2 newsScroll;
        private VoiceSession voice;
        private OfficeScene office;
        private BrokerTheme theme;
        private string message = "";
        private Vector2 scroll;
        private Vector2 canvasOffset;
        private float canvasScale;
        private List<DialogueTurn> History => histories[day.SelectedClientIndex];
        private List<string> Conversation => conversations[day.SelectedClientIndex];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (UnityEngine.Object.FindFirstObjectByType<PrototypeBootstrap>() == null)
                new GameObject("Broker Game").AddComponent<PrototypeBootstrap>();
        }
        private void Awake()
        {
            office = new GameObject("First-person broker office").AddComponent<OfficeScene>();
            office.Player.OnInteraction = OnWorldInteraction;
            office.Player.OnFocusLost = () => { if (!intro && !day.DayEnded && navigation.IsExploring) navigation.Pause(); };
            voice = gameObject.AddComponent<VoiceSession>();
            voice.OnTranscript = text => { if (!day.DayEnded && day.Call == CallStatus.Active && !busy) message = text; };
            Restart();
        }
        private void Update()
        {
            canvasScale = Mathf.Max(0.1f, Mathf.Min(Screen.width / 1440f, Screen.height / 960f));
            canvasOffset = new Vector2((Screen.width - 1440 * canvasScale) / 2, (Screen.height - 960 * canvasScale) / 2);
            if (!intro && !day.DayEnded && !showNews && day.PendingEvent == null && OfficeInput.EscapePressed())
            {
                if (navigation.IsExploring) { navigation.Pause(); voice.Cancel(); }
                else { CancelPending(); EnterWorld(); }
            }
            if (!intro && !showNews && navigation.RunsClock && (OfficeInput.Available || navigation.View == OfficeView.Desk) && !busy && !voice.Working) day.AdvanceTime(Time.unscaledDeltaTime);
            if (day.DayEnded && !lastDayEnded) { CancelPending(); lastDayEnded = true; }
            bool blocked = intro || showNews || day.DayEnded || day.PendingEvent != null;
            office.Player.SetControl(navigation.IsExploring && !blocked && OfficeInput.Available);
            office.Present(navigation.View == OfficeView.Desk, canvasScale, canvasOffset);
            DrainNews(); office.Refresh(day);
        }
        private void OnDestroy()
        {
            CancelPending();
            if (office != null) Destroy(office.gameObject);
            theme?.Dispose();
        }
        private void Restart()
        {
            CancelPending(); day = new BrokerDay(); lastDayEnded = false; intro = true; showNews = false; newsScroll = Vector2.zero;
            foreach (var history in histories) history.Clear();
            foreach (var conversation in conversations) conversation.Clear();
            officeNews.Clear(); message = ""; scroll = Vector2.zero; DrainNews();
            navigation.Reset(); staffTargetIndex = 0; office.Player.SetControl(false); office.ResetPlayer();
        }
        private void DrainNews()
        {
            string news;
            while ((news = day.PopNotification()) != null) News(news);
        }
        private void News(string text)
        {
            officeNews.Add(text); if (officeNews.Count > 30) officeNews.RemoveAt(0);
        }
        private void CancelPending()
        {
            revision++; if (voice != null) voice.Cancel(); pending?.Cancel(); pending = null; busy = false;
        }
        private void Add(string line) { Conversation.Add(line); if (Conversation.Count > 80) Conversation.RemoveAt(0); scroll.y = float.MaxValue; }
        private void PresentClientReply(string reply)
        {
            Add(reply); History.Add(new DialogueTurn("client", reply));
            while (History.Count > 12) History.RemoveAt(0);
            if (voiceEnabled && !day.DayEnded && day.PendingEvent == null) voice.PlayReply(reply);
        }
        private async void SendMessageToClient()
        {
            if (busy || intro || day.DayEnded || day.PendingEvent != null || day.Call != CallStatus.Active
                || string.IsNullOrWhiteSpace(message) || voice.Recording || voice.Working) return;
            voice.Cancel();
            string input = message.Trim(); message = ""; Add("Вы: " + input);
            string canonical = day.Say(input);
            var request = new DialogueRequest { clientId = day.Current.Id, message = input, canonicalReply = canonical, history = History.ToArray() };
            History.Add(new DialogueTurn("player", input));
            var source = new CancellationTokenSource(); pending = source; int version = ++revision; busy = true;
            try
            {
                IDialogueProvider provider = useGateway ? (IDialogueProvider)new LocalGatewayDialogueProvider() : new OfflineDialogueProvider();
                string reply = await provider.ReplyAsync(request, source.Token);
                if (version == revision && !day.DayEnded) PresentClientReply(reply);
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                if (version != revision || day.DayEnded) return;
                News("Шлюз недоступен. Использована локальная реплика без повторного применения правил."); PresentClientReply(canonical);
            }
            finally { source.Dispose(); if (version == revision) { pending = null; busy = false; } }
        }

        private void Paint(Rect rect, Color color)
        {
            var old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
        }
        private void Label(float x, float y, float w, float h, string text, GUIStyle style = null) { GUI.Label(new Rect(x, y, w, h), text, style ?? theme.Text); }
        private bool Button(float x, float y, float w, float h, string text, bool enabled = true, bool primary = false)
        {
            bool was = GUI.enabled; GUI.enabled = was && enabled;
            bool clicked = GUI.Button(new Rect(x, y, w, h), text, primary ? theme.Primary : theme.Button); GUI.enabled = was; return clicked;
        }
        private void Panel(float x, float y, float w, float h, string title)
        {
            Paint(new Rect(x, y, w, h), theme.Surface); Paint(new Rect(x, y, w, 2), theme.Border);
            if (!string.IsNullOrEmpty(title)) Label(x + 20, y + 16, w - 40, 30, title, theme.Heading);
        }
        private void Meter(float x, float y, float w, float value, Color color)
        { Paint(new Rect(x, y, w, 5), theme.Border); Paint(new Rect(x, y, w * Mathf.Clamp01(value), 5), color); }
        private void OnGUI()
        {
            if (theme == null) theme = new BrokerTheme();
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            if (navigation.View != OfficeView.Desk)
            {
                GUI.matrix = Matrix4x4.TRS(new Vector3(canvasOffset.x, canvasOffset.y, 0), Quaternion.identity, Vector3.one * canvasScale);
                DrawExplorationScreen();
                GUI.matrix = previous;
                return;
            }
            var view = new Rect(canvasOffset.x + 340 * canvasScale, canvasOffset.y + 110 * canvasScale, 720 * canvasScale, 300 * canvasScale);
            // Leave the camera viewport uncovered by the GUI background.
            Paint(new Rect(0, 0, Screen.width, view.y), theme.Canvas);
            Paint(new Rect(0, view.yMax, Screen.width, Screen.height - view.yMax), theme.Canvas);
            Paint(new Rect(0, view.y, view.x, view.height), theme.Canvas);
            Paint(new Rect(view.xMax, view.y, Screen.width - view.xMax, view.height), theme.Canvas);
            GUI.matrix = Matrix4x4.TRS(new Vector3(canvasOffset.x, canvasOffset.y, 0), Quaternion.identity, Vector3.one * canvasScale);
            bool blocked = intro || showNews || day.DayEnded || day.PendingEvent != null;
            GUI.enabled = !blocked;
            DrawHeader(); DrawClients(); DrawEconomy(); DrawOfficeNews(); DrawConversation(); DrawTeam(); DrawSettings();
            GUI.enabled = true;
            if (intro) DrawIntro(); else if (day.DayEnded) DrawResults(); else if (day.PendingEvent != null) DrawEvent(); else if (showNews) DrawNewsJournal();
            GUI.matrix = previous;
        }
        private void DrawHeader()
        {
            Label(24, 20, 400, 36, "АЛЛО, ЭТО ДЕНЬГИ", theme.Title);
            Label(24, 58, 400, 24, "ДЕНЬ 01  /  БОЛЬШИЕ АМБИЦИИ, МАЛЕНЬКИЙ ОФИС", theme.Small);
            int seconds = Mathf.CeilToInt(day.RemainingSeconds);
            Label(470, 20, 150, 30, $"{seconds / 60:00}:{seconds % 60:00}", theme.Title); Label(470, 56, 150, 24, "до конца дня", theme.Small);
            Label(650, 20, 170, 30, "$" + day.Cash, theme.Title); Label(650, 56, 170, 24, "в кассе / нужно " + day.TotalExpenses, theme.Small);
            Label(850, 20, 160, 30, day.TeamMorale + "%", theme.Title); Label(850, 56, 160, 24, "настроение команды", theme.Small);
            Label(1040, 20, 160, 30, day.Reputation + "/100", theme.Title); Label(1040, 56, 160, 24, "репутация", theme.Small);
            if (Button(1230, 24, 190, 48, "Встать / Esc")) { CancelPending(); EnterWorld(); }
        }
        private void DrawClients()
        {
            Panel(20, 110, 300, 470, "КЛИЕНТСКАЯ БАЗА");
            for (int i = 0; i < day.Clients.Count; i++)
            {
                var client = day.Clients[i]; float y = 164 + i * 108;
                bool chosen = i == day.SelectedClientIndex;
                Paint(new Rect(36, y, 268, 98), chosen ? BrokerTheme.Hex("#304750") : BrokerTheme.Hex("#14262E"));
                if (chosen) Paint(new Rect(36, y, 3, 98), theme.Gold);
                bool canSelect = !busy && day.Call != CallStatus.Active;
                if (Button(40, y + 4, 260, 90, "", canSelect))
                {
                    CancelPending();
                    if (day.SelectClient(i)) { message = ""; scroll = Vector2.zero; }
                }
                Label(52, y + 8, 150, 28, client.Name, theme.Heading);
                Label(212, y + 12, 76, 24, "+$" + client.Commission, theme.Tag);
                Label(52, y + 38, 232, 24, client.Role, theme.Small);
                Label(52, y + 65, 232, 24, client.DealClosed ? "СДЕЛКА ЗАКРЫТА" : client.Call == CallStatus.Closed ? "ЗВОНОК ЗАВЕРШЁН" : client.Call == CallStatus.Active ? "НА ЛИНИИ" : chosen ? "ВЫБРАН ДЛЯ ЗВОНКА" : "ОЖИДАЕТ ЗВОНКА", theme.Tag);
            }
            if (Button(40, 510, 260, 48, day.Call == CallStatus.Active ? "Разговор идёт…" : "Позвонить: " + day.Current.Name, day.Call == CallStatus.NotStarted && !busy, true))
                PresentClientReply(day.StartCall());
        }
        private void DrawEconomy()
        {
            Panel(20, 596, 300, 324, "КАССА И АПГРЕЙДЫ");
            Label(40, 644, 260, 48, "Аренда $100 + зарплаты $240\nИтоговые расходы: $" + day.TotalExpenses, theme.Small);
            Meter(40, 702, 260, day.Cash / (float)day.TotalExpenses, day.Successful ? theme.Green : theme.Gold);
            if (Button(40, 724, 260, 48, day.CoffeeBought ? "Кофе уже куплен" : "Кофе команде • $40", !day.CoffeeBought && day.Cash >= 40)) News(day.BuyCoffee());
            if (Button(40, 784, 260, 48, day.HasGoldenPhone ? "Золотой телефон установлен" : "Золотой телефон • $90", !day.HasGoldenPhone && day.Cash >= 90)) News(day.BuyGoldenPhone());
            Label(40, 846, 260, 60, "Кофе: +20 настроения.\nТелефон: +1 доверия оставшихся клиентов. Не забудь про зарплаты.", theme.Small);
        }
        private void DrawOfficeNews()
        {
            Label(356, 120, 680, 24, "МОЙКА КАПИТАЛ  /  ВИД ИЗ КАБИНЕТА", theme.Tag);
            Panel(340, 426, 720, 66, "");
            string news = officeNews.Count == 0 ? "Офис пока подозрительно тихий." : officeNews[officeNews.Count - 1];
            if (news.Length > 140) news = news.Substring(0, 137) + "…";
            Label(356, 434, 560, 48, news, theme.Small);
            if (Button(932, 436, 112, 44, "Журнал")) showNews = true;
        }
        private void DrawConversation()
        {
            Panel(340, 508, 720, 412, "ЛИНИЯ: " + day.Current.Name.ToUpperInvariant());
            Label(830, 525, 210, 26, "ДОВЕРИЕ  " + day.Trust + "/4", theme.Tag);
            string checks = (day.NeedsKnown ? "[+]" : "[ ]") + " цель     " + (day.RisksExplained ? "[+]" : "[ ]") + " риски     " + (day.FeesExplained ? "[+]" : "[ ]") + " комиссия";
            Label(360, 556, 680, 28, checks, theme.Small);
            var transcriptRect = new Rect(356, 594, 688, 144);
            float height = 10;
            foreach (var line in Conversation) height += theme.Text.CalcHeight(new GUIContent(line), 648) + 16;
            string emptyText = day.Current.Brief + "\n\nПродукт: «Мойка-Капитал». Риск убытка, комиссия 2%, срок — год, персональный отчёт.";
            float emptyHeight = theme.Text.CalcHeight(new GUIContent(emptyText), 636);
            if (Conversation.Count == 0) height = Mathf.Max(height, emptyHeight + 12);
            scroll = GUI.BeginScrollView(transcriptRect, scroll, new Rect(0, 0, 654, Mathf.Max(144, height)));
            float y = 4;
            if (Conversation.Count == 0)
                GUI.Label(new Rect(4, y, 636, emptyHeight), emptyText, theme.Text);
            else foreach (var line in Conversation)
            {
                float lineHeight = theme.Text.CalcHeight(new GUIContent(line), 648);
                GUI.Label(new Rect(4, y, 648, lineHeight), line, theme.Text); y += lineHeight + 16;
            }
            GUI.EndScrollView();
            bool canTalk = day.Call == CallStatus.Active && !busy && !voice.Recording && !voice.Working;
            bool enabled = GUI.enabled; GUI.enabled = enabled && canTalk;
            GUI.SetNextControlName("BrokerMessage");
            message = GUI.TextField(new Rect(360, 750, 680, 48), message, 500, theme.Field); GUI.enabled = enabled;
            if (GUI.enabled && canTalk && GUI.GetNameOfFocusedControl() == "BrokerMessage"
                && Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            { Event.current.Use(); SendMessageToClient(); }
            if (Button(360, 812, 212, 44, busy ? "Клиент думает…" : "Отправить", canTalk && !string.IsNullOrWhiteSpace(message), true)) { SendMessageToClient(); GUI.FocusControl(null); }
            if (Button(584, 812, 220, 44, "Предложить сделку", canTalk)) PresentClientReply(day.OfferDeal());
            if (Button(816, 812, 224, 44, "Повесить трубку", day.Call == CallStatus.Active)) { CancelPending(); PresentClientReply(day.EndCall()); }
            if (voiceEnabled)
            {
                if (Button(360, 868, 440, 44, voice.Recording ? "Закончить запись и распознать" : "Записать реплику • до 20 секунд", day.Call == CallStatus.Active && !busy && !voice.Working))
                { if (voice.Recording) voice.FinishRecording(); else voice.BeginRecording(); }
                if (Button(816, 868, 224, 44, "Остановить голос")) voice.Cancel();
            }
            else Label(360, 872, 680, 36, "Подсказка: цель → риск → комиссия 2% → сделка. У каждого клиента есть свой нюанс.", theme.Small);
        }
        private void DrawTeam()
        {
            Panel(1080, 110, 340, 500, "ВАША КОМАНДА");
            for (int i = 0; i < day.Employees.Count; i++)
            {
                var worker = day.Employees[i]; float y = 164 + i * 214;
                Label(1100, y, 200, 28, worker.Name, theme.Heading); Label(1310, y + 2, 90, 24, worker.Morale + "%", theme.Tag);
                Label(1100, y + 34, 300, 48, worker.Trait, theme.Small);
                string work = worker.Work == WorkKind.Idle ? "Свободен" : worker.Work == WorkKind.Research ? "Досье: " + day.Clients[worker.TargetClient].Name : worker.Work == WorkKind.Leads ? "Побочный обзвон" : "На перерыве";
                Label(1100, y + 88, 300, 26, work + (worker.Work == WorkKind.Idle ? "" : " • " + Mathf.CeilToInt(worker.Remaining) + " с"), theme.Small);
                Meter(1100, y + 122, 300, worker.Work == WorkKind.Idle ? 0 : 1 - worker.Remaining / worker.Duration, theme.Green);
                bool idle = worker.Work == WorkKind.Idle;
                if (Button(1100, y + 140, 92, 44, "Досье", idle && !day.Current.Researched)) News(day.AssignTask(i, WorkKind.Research, day.SelectedClientIndex));
                if (Button(1204, y + 140, 92, 44, "Обзвон", idle && !worker.SideDealDone)) News(day.AssignTask(i, WorkKind.Leads, day.SelectedClientIndex));
                if (Button(1308, y + 140, 92, 44, "Перерыв", idle)) News(day.AssignTask(i, WorkKind.Break, day.SelectedClientIndex));
            }
        }
        private void DrawSettings()
        {
            Panel(1080, 626, 340, 294, "СВЯЗЬ И ПАМЯТКА");
            bool before = GUI.enabled; GUI.enabled = before && !busy && day.Call != CallStatus.Active;
            useGateway = GUI.Toggle(new Rect(1100, 676, 300, 44), useGateway, " ИИ-формулировки через сервер", theme.Button); GUI.enabled = before;
            bool nextVoice = GUI.Toggle(new Rect(1100, 732, 300, 44), voiceEnabled, " Голос: внешний API и микрофон", theme.Button);
            if (nextVoice != voiceEnabled) { voiceEnabled = nextVoice; if (!voiceEnabled) voice.Cancel(); }
            Label(1100, 790, 300, 112, voiceEnabled ? voice.Status + "\nЗапись только по кнопке; звук уходит провайдеру." : "Esc — встать из-за стола. День завершается у входной двери.\n\nБез сервера работает весь офис. Досье относится к выбранному клиенту.", theme.Small);
        }
        private void Scrim() { Paint(new Rect(0, 0, 1440, 960), new Color(0.02f, 0.05f, 0.07f, 0.92f)); }
        private void DrawIntro()
        {
            Scrim(); Panel(340, 220, 760, 510, "ДЕНЬ 01 / СНАЧАЛА ЗАРАБОТАЙТЕ НА ОФИС");
            Label(372, 288, 696, 64, "Вы — генеральный директор.\nОб этом пока знают только визитки.", theme.Title);
            Label(372, 380, 696, 160, "WASD — ходить. Мышь — осматриваться. E — взаимодействовать. Esc — пауза или встать из-за стола.\n\nПодойдите к своему столу и телефону, чтобы звонить клиентам. Поговорите с Леной и Максом, поручите работу. Кофе — у кофемашины, конец дня — у входной двери. Нужно заработать $340.", theme.Text);
            Label(372, 562, 696, 52, "Это сценарная демоверсия: понимание реплик по ключевым словам. Все компании и деньги вымышлены.", theme.Small);
            if (Button(372, 646, 696, 52, "Войти в 3D-офис", true, true)) { intro = false; EnterWorld(); }
        }
        private void DrawEvent()
        {
            Scrim(); var item = day.PendingEvent;
            Panel(340, 228, 760, 490, "ОФИСНОЕ ПРОИСШЕСТВИЕ / ВРЕМЯ НА ПАУЗЕ");
            Label(372, 294, 696, 80, item.Title, theme.Title);
            Label(372, 388, 696, 96, item.Description, theme.Text);
            if (Button(372, 514, 696, 56, item.FirstChoice, day.Cash >= item.FirstChoiceCost)) ResolveOfficeEvent(0);
            // Resolution may replace PendingEvent; the local immutable item stays safe for this GUI frame.
            if (Button(372, 590, 696, 56, item.SecondChoice)) ResolveOfficeEvent(1);
            Label(372, 668, 696, 30, "В кассе $" + day.Cash + ". Бесплатный вариант доступен всегда.", theme.Small);
        }
        private void DrawNewsJournal()
        {
            Scrim(); Panel(340, 140, 760, 650, "ЖУРНАЛ ОФИСА / ВРЕМЯ НА ПАУЗЕ");
            float height = 8;
            foreach (string entry in officeNews) height += theme.Text.CalcHeight(new GUIContent(entry), 644) + 24;
            newsScroll = GUI.BeginScrollView(new Rect(372, 212, 696, 438), newsScroll, new Rect(0, 0, 656, Mathf.Max(438, height)));
            float y = 4;
            foreach (string entry in officeNews)
            {
                float lineHeight = theme.Text.CalcHeight(new GUIContent(entry), 644);
                GUI.Label(new Rect(4, y, 644, lineHeight), entry, theme.Text); y += lineHeight + 24;
            }
            GUI.EndScrollView();
            if (Button(372, 688, 696, 52, "Вернуться в офис", true, true)) showNews = false;
        }
        private void DrawResults()
        {
            Scrim(); Panel(340, 140, 760, 650, "РАБОЧИЙ ДЕНЬ ЗАКРЫТ");
            Label(372, 210, 696, 80, day.Successful ? "КОНТОРА ПЕРЕЖИЛА ДЕНЬ." : "ВИЗИТКИ БЫЛИ УБЕДИТЕЛЬНЕЕ КАССЫ.", theme.Title);
            Label(372, 318, 696, 220, "Основных сделок: " + day.DealsCount + "/3\nЗаработано: $" + day.Earned + "\nПотрачено в течение дня: $" + day.Spent + "\nОбязательства: $" + day.TotalExpenses + "\nОстаток / долг после расчёта: $" + day.FinalBalance + "\nРепутация: " + day.Reputation + "/100 • настроение: " + day.TeamMorale + "%", theme.Text);
            Label(372, 556, 696, 86, day.Successful ? day.Reputation < 40 ? "Деньги есть. А вот желание клиентов перезванивать — под вопросом." : "Аренда и зарплаты покрыты. Можно снова говорить «мы динамично развиваемся»." : "Арендодатель принимает переводы. Ваш потенциал он принимать отказался.", theme.Text);
            if (Button(372, 688, 696, 52, "Начать день заново", true, true)) Restart();
        }
    }
}
