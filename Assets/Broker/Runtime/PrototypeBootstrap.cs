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
        private double toastUntil;
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
            office.Player.OnFocusLost = () => { if (!selectingMode && !intro && !day.DayEnded && navigation.IsExploring) navigation.Pause(); };
            voice = gameObject.AddComponent<VoiceSession>();
            voice.OnTranscript = text => { if (!day.DayEnded && day.Call == CallStatus.Active && !busy) message = text; };
            InitializeCareer();
        }
        private void Update()
        {
            canvasScale = Mathf.Max(0.1f, Mathf.Min(Screen.width / 1440f, Screen.height / 960f));
            canvasOffset = new Vector2((Screen.width - 1440 * canvasScale) / 2, (Screen.height - 960 * canvasScale) / 2);
            bool escape = OfficeInput.EscapePressed();
            if (UtilityOverlayOpen && escape) CloseUtilityOverlay();
            else if (!selectingMode && !intro && !day.DayEnded && !showNews && day.PendingEvent == null && escape)
            {
                if (navigation.IsExploring) { navigation.Pause(); voice.Cancel(); }
                else { CancelPending(); EnterWorld(); }
            }
            if (!selectingMode && !intro && !showNews && !UtilityOverlayOpen && navigation.RunsClock && (OfficeInput.Available || navigation.View == OfficeView.Desk) && !busy && !voice.Working) day.AdvanceTime(Time.unscaledDeltaTime, office.IsInCity);
            if (day.DayEnded && !lastDayEnded) { CancelPending(); SettleCareerDay(); lastDayEnded = true; }
            bool blocked = selectingMode || intro || showNews || UtilityOverlayOpen || day.DayEnded || day.PendingEvent != null;
            office.Player.SetControl(navigation.IsExploring && !blocked && OfficeInput.Available);
            office.WorldMotionEnabled = !blocked && navigation.RunsClock && !busy && !voice.Working;
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
            CancelPending(); showVoiceSetup = false; showCallLog = false; confirmation = ConfirmationKind.None; deskPage = DeskPage.Calls;
            if (CurrentCareerFinished)
            {
                // Presentation-only day: the actual final ledger is stored in CareerState.
                day = IsEmployeeMode ? new BrokerDay(3, employment.PersonalSavings, employment.Reputation, mode: CareerMode.Employee)
                    : new BrokerDay(3, Math.Max(0, career.Balance), career.Reputation, career.GoldenPhone);
                day.EndDay(); lastDayEnded = true; intro = false;
            }
            else { day = IsEmployeeMode ? employment.CreateDay() : career.CreateDay(); lastDayEnded = false; intro = true; }
            showNews = false; newsScroll = Vector2.zero;
            foreach (var history in histories) history.Clear();
            foreach (var conversation in conversations) conversation.Clear();
            officeNews.Clear(); message = ""; scroll = Vector2.zero; DrainNews();
            office.ConfigureCareerMode(mode);
            navigation.Reset(); staffTargetIndex = 0; office.Player.SetControl(false); office.ResetPlayer();
        }
        private void DrainNews()
        {
            string news;
            while ((news = day.PopNotification()) != null) News(news);
        }
        private void News(string text)
        {
            toastUntil = Time.realtimeSinceStartupAsDouble + 5;
            officeNews.Add(text); if (officeNews.Count > 30) officeNews.RemoveAt(0);
        }
        private void CancelPending()
        {
            revision++; if (voice != null) voice.Cancel(); pending?.Cancel(); pending = null; busy = false;
        }
        private void Add(string line) { Conversation.Add(line); if (Conversation.Count > 80) Conversation.RemoveAt(0); scroll.y = float.MaxValue; }
        private void PresentClientReply(string reply)
        {
            // External providers may return text without a speaker prefix. Keep the
            // display history consistent so the compact latest-reply view never stays stale.
            string prefix = day.Current.Name + ": ";
            Add(reply.StartsWith(prefix, StringComparison.Ordinal) ? reply : prefix + reply);
            History.Add(new DialogueTurn("client", reply));
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
            GUI.Box(new Rect(x, y, w, h), "", theme.PanelStyle);
            if (!string.IsNullOrEmpty(title)) Label(x + 20, y + 16, w - 40, 30, title, theme.Heading);
        }
        private void Meter(float x, float y, float w, float value, Color color)
        { Paint(new Rect(x, y, w, 5), theme.Border); Paint(new Rect(x, y, w * Mathf.Clamp01(value), 5), color); }
        private void OnGUI()
        {
            if (theme == null) theme = new BrokerTheme();
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            if (UtilityOverlayOpen)
            {
                GUI.matrix = Matrix4x4.TRS(new Vector3(canvasOffset.x, canvasOffset.y, 0), Quaternion.identity, Vector3.one * canvasScale);
                GUI.enabled = true;
                if (showVoiceSetup) DrawVoiceSetup(); else if (showCallLog) DrawCallLog(); else DrawConfirmation();
                GUI.matrix = previous;
                return;
            }
            if (selectingMode)
            {
                GUI.matrix = Matrix4x4.TRS(new Vector3(canvasOffset.x, canvasOffset.y, 0), Quaternion.identity, Vector3.one * canvasScale);
                DrawModeSelection(); GUI.matrix = previous; return;
            }
            if (navigation.View != OfficeView.Desk)
            {
                GUI.matrix = Matrix4x4.TRS(new Vector3(canvasOffset.x, canvasOffset.y, 0), Quaternion.identity, Vector3.one * canvasScale);
                DrawExplorationScreen();
                GUI.matrix = previous;
                return;
            }
            GUI.matrix = Matrix4x4.TRS(new Vector3(canvasOffset.x, canvasOffset.y, 0), Quaternion.identity, Vector3.one * canvasScale);
            DrawModernDesk();
            GUI.enabled = true;
            if (intro) DrawIntro(); else if (day.DayEnded) DrawResults(); else if (day.PendingEvent != null) DrawEvent(); else if (showNews) DrawNewsJournal();
            GUI.matrix = previous;
        }
        private void Scrim() { Paint(new Rect(0, 0, 1440, 960), new Color(0.035f, 0.055f, 0.09f, 0.86f)); }
        private void DrawIntro()
        {
            if (IsEmployeeMode) { DrawEmploymentIntro(); return; }
            Scrim(); Panel(280, 112, 880, 736, "");
            Label(320, 152, 800, 32, "ДЕНЬ " + day.DayNumber + " / 3", theme.Tag);
            Label(320, 208, 800, 132, day.DayNumber == 1 ? "МАЛЕНЬКАЯ КОНТОРА.\nБОЛЬШИЕ АМБИЦИИ." : "СЕГОДНЯ ВАС\nУЖЕ ПОМНЯТ.", theme.Display);
            MetricCard(320, 376, 248, "В кассе", "$" + day.OpeningCash);
            MetricCard(592, 376, 248, "Счета сегодня", "$" + day.TotalExpenses);
            MetricCard(864, 376, 256, "Клиенты", "3");
            Label(320, 520, 800, 28, "WASD — ходить    Мышь — обзор    E — действие    Esc — пауза", theme.Small);
            Label(320, 560, 800, 52, saveNotice, theme.Small);
            if (Button(320, 632, 800, 56, "Начать день " + day.DayNumber, true, true)) { intro = false; EnterWorld(); }
            if (Button(320, 704, 800, 48, "Проверить микрофон без API")) OpenVoiceSetup();
            Label(320, 792, 800, 28, "Лифт справа — в квартал. Дверь у входа — закончить день.", theme.Small);
        }
        private void DrawEvent()
        {
            Scrim(); var item = day.PendingEvent;
            Panel(280, 188, 880, 584, ""); Label(320, 224, 800, 28, "РЕШЕНИЕ / ВРЕМЯ НА ПАУЗЕ", theme.Tag);
            Label(320, 284, 800, 92, item.Title, theme.Title);
            Label(320, 392, 800, 84, item.Description, theme.Text);
            if (Button(320, 492, 384, 136, item.FirstChoice, day.Cash >= item.FirstChoiceCost, true)) ResolveOfficeEvent(0);
            if (Button(736, 492, 384, 136, item.SecondChoice)) ResolveOfficeEvent(1);
            Label(320, 688, 800, 32, "В кассе $" + day.Cash, theme.Heading);
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
            if (IsEmployeeMode) { DrawEmploymentResults(); return; }
            if (career.IsFinished) { DrawCareerFinal(); return; }
            Scrim(); Panel(280, 112, 880, 736, "");
            Label(320, 152, 800, 28, "ДЕНЬ " + day.DayNumber + " ЗАКРЫТ", theme.Tag);
            Label(320, 208, 800, 72, day.Successful ? "КОНТОРА ЖИВЁТ." : "ДОЛГ НА ЗАВТРА.", theme.Display);
            MetricCard(320, 304, 248, "Остаток / долг", "$" + day.FinalBalance);
            MetricCard(592, 304, 248, "Сделки", day.DealsCount + " / 3");
            MetricCard(864, 304, 256, "Репутация", day.Reputation + "/100");
            Label(320, 448, 800, 28, "Утро + доход − покупки − счета", theme.Small);

            Label(320, 488, 800, 48, "$" + day.OpeningCash + " + $" + day.Earned + " − $" + day.Spent + " − $" + day.TotalExpenses + " = $" + day.FinalBalance, theme.Heading);
            Label(320, 560, 800, 52, saveNotice, theme.Small);
            bool settled = career.CompletedDays == day.DayNumber;
            if (Button(320, 632, 800, 56, saveSucceeded ? "Следующий день" : "Продолжить без записи на диск", settled, true)) Restart();
            if (Button(320, 704, 800, 48, "Новая кампания")) RequestConfirmation(ConfirmationKind.NewCareer);
            if (!saveSucceeded && Button(320, 776, 800, 48, "Повторить сохранение")) SaveCareer();
        }
    }
}
