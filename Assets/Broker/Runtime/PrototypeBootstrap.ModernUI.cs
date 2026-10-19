using Broker.Core;
using Broker.Runtime.Office;
using UnityEngine;

namespace Broker.Runtime
{
    public sealed partial class PrototypeBootstrap
    {
        private enum DeskPage { Calls, Team, Office }
        private DeskPage deskPage;
        private readonly int[] teamTargets = { 0, 0 };
        private bool showCallLog, profileInLog;
        private Vector2 replyScroll, callLogScroll;
        private string shownReply = "";

        private void MetricCard(float x, float y, float width, string caption, string value, string detail = "")
        {
            Panel(x, y, width, 116, ""); Label(x + 24, y + 16, width - 48, 24, caption, theme.Small);
            Label(x + 24, y + 42, width - 48, 48, value, theme.Metric);
            if (!string.IsNullOrEmpty(detail)) Label(x + 24, y + 90, width - 48, 22, detail, theme.Small);
        }
        private string ClockText()
        { int seconds = Mathf.CeilToInt(day.RemainingSeconds); return $"{seconds / 60:00}:{seconds % 60:00}"; }
        private void DrawModernDesk()
        {
            Paint(new Rect(0, 0, 1440, 960), new Color(0.035f, 0.055f, 0.09f, 0.74f));
            Panel(40, 32, 56, 56, ""); Label(48, 42, 48, 36, "МК", theme.Heading);
            Label(112, 34, 264, 32, "МОЙКА КАПИТАЛ", theme.Heading);
            Label(112, 68, 264, 24, IsEmployeeMode ? "Наёмный брокер · личные продажи" : "Операционная система конторы", theme.Small);
            Label(580, 32, 180, 44, ClockText(), theme.Title); Label(580, 74, 180, 24, "День " + day.DayNumber + (IsWorkMode ? " · работа" : " / 3"), theme.Small);
            Label(816, 32, 250, 44, "$" + day.Cash, theme.Title); Label(816, 74, 250, 24, IsEmployeeMode ? "Ваши накопления · ставка 20%" : "из $" + day.TotalExpenses + " к закрытию", theme.Small);
            Label(1104, 32, 120, 40, day.Reputation + "/100", theme.Heading); Label(1104, 72, 120, 24, "Репутация", theme.Small);
            if (Button(1240, 40, 160, 48, "Встать · Esc")) { CancelPending(); EnterWorld(); }
            bool blocked = intro || showNews || day.DayEnded || day.PendingEvent != null;
            GUI.enabled = !blocked;
            string[] tabs = IsEmployeeMode ? new[] { "Звонки", "Начальник", "Связь" } : new[] { "Звонки", "Команда", "Офис" };
            for (int i = 0; i < tabs.Length; i++)
                if (Button(344 + i * 176, 120, 160, 48, tabs[i], !busy && !voice.Working && !voice.Recording, (int)deskPage == i))
                { voice.Cancel(); deskPage = (DeskPage)i; GUI.FocusControl(null); }
            if (deskPage == DeskPage.Calls) { DrawClientRail(); DrawCallWorkspace(); }
            else { DrawDayRail(); if (deskPage == DeskPage.Team) { if (IsEmployeeMode) DrawSupervisorWorkspace(); else DrawTeamWorkspace(); } else DrawOfficeWorkspace(); }
            if (Button(40, 840, 264, 48, "Журнал офиса")) showNews = true;
            Label(344, 880, 1056, 56, busy ? "Клиент отвечает…" : voiceEnabled ? (voice.Status.Length > 110 ? voice.Status.Substring(0, 107) + "…" : voice.Status) : "Локальный режим  /  без сервера", theme.Small);
            GUI.enabled = true;
        }
        private void DrawClientRail()
        {
            Label(40, 128, 264, 32, "Клиенты", theme.Heading);
            var relationships = IsWorkMode ? workCareer.Clients : null;
            for (int i = 0; i < day.Clients.Count; i++)
            {
                var c = day.Clients[i]; float y = 184 + i * 140;
                bool selected = i == day.SelectedClientIndex;
                if (Button(40, y, 264, 120, "", day.Call != CallStatus.Active && !busy && !voice.Working && !voice.Recording))
                { CancelPending(); day.SelectClient(i); message = ""; replyScroll = Vector2.zero; }
                if (selected) Paint(new Rect(48, y + 16, 3, 88), theme.Accent);
                Label(60, y + 16, 160, 32, c.Name, theme.Heading);
                Label(224, y + 20, 68, 24, "$" + (IsEmployeeMode ? BrokerDay.PersonalPayout(c.Commission) : c.Commission), theme.Small);
                Label(60, y + 66, 220, 32, c.DealClosed ? "Сделка закрыта" : c.Call == CallStatus.Closed ? "Звонок завершён" : c.Call == CallStatus.Active ? "На линии" : c.Returning ? "Вернулся за отчётом" : IsWorkMode ? "Готов к звонку" : "Первый контакт", theme.Small);
                if (relationships != null) Label(60, y + 96, 220, 22, "Отношения " + Signed(relationships[ClientCatalog.Index(c.Id)].relationship), theme.Small);
            }
            Panel(40, 632, 264, 168, ""); Label(64, 656, 216, 28, IsEmployeeMode ? "Ориентир · без штрафа" : "План дня", theme.Small);
            Label(64, 696, 216, 44, day.DealsCount + " / " + (IsEmployeeMode ? day.SalesTarget : 3) + " сделки", theme.Title);
            Meter(64, IsEmployeeMode ? 744 : 768, 216, day.DealsCount / (float)(IsEmployeeMode ? day.SalesTarget : 3), theme.Accent);
            if (IsEmployeeMode && Button(64, 756, 216, 36, "Следующий клиент", day.RecommendedClientIndex >= 0
                && day.Call != CallStatus.Active && !busy && !voice.Recording && !voice.Working))
            { CancelPending(); day.SelectClient(day.RecommendedClientIndex); message = ""; replyScroll = Vector2.zero; }
        }
        private void DrawDayRail()
        {
            Label(40, 128, 264, 32, "Сегодня", theme.Heading);
            MetricCard(40, 184, 264, "Сделки", day.DealsCount + " / 3");
            MetricCard(40, 320, 264, IsEmployeeMode ? "Ваша выплата за день" : "Команда", IsEmployeeMode ? "$" + day.Earned : day.TeamMorale + "%");
            MetricCard(40, 456, 264, IsEmployeeMode ? "Комиссия фирмы" : "Обязательства", "$" + (IsEmployeeMode ? day.FirmRevenue : day.TotalExpenses));
            Label(48, 616, 248, 54, day.DayTitle, theme.Heading);
            Label(48, 692, 248, 116, IsEmployeeMode ? "Лена и Макс — коллеги. Аренда и зарплаты — за счёт работодателя." : "Квартал открыт для прогулки. Поручения временно скрыты.", theme.Small);
        }
        private void DrawCallWorkspace()
        {
            Panel(344, 184, 1056, 664, "");
            Panel(376, 216, 72, 72, ""); Label(397, 228, 54, 52, day.Current.Name.Substring(0, 1), theme.Metric);
            Label(472, 212, 650, 44, day.Current.Name, theme.Title);
            Label(472, 262, 650, 28, day.Current.Role, theme.Small);
            Label(1184, 212, 184, 24, "Доверие " + day.Trust + "/4", theme.Small);
            for (int i = 0; i < 4; i++) Paint(new Rect(1184 + i * 46, 252, 34, 6), i < day.Trust ? theme.Accent : theme.Border);
            float chipX = 376;
            StageChip(ref chipX, "Цель", day.NeedsKnown); StageChip(ref chipX, "Риск", day.RisksExplained); StageChip(ref chipX, "2%", day.FeesExplained);
            if (ClientCatalog.NeedsYear(day.Current.Id) || ClientCatalog.NeedsService(day.Current.Id)) StageChip(ref chipX, ClientCatalog.NeedsYear(day.Current.Id) ? "Срок" : "Сервис", day.Current.SpecialDiscussed);
            if (day.Current.Returning) StageChip(ref chipX, "Отчёт", day.Current.ReportReviewed);
            StageChip(ref chipX, "Вопрос", day.Current.ObjectionResolved);
            Label(376, 360, 760, 40, BrokerPresentation.ShortHint(day.ConversationStep, day.Current.Id), theme.Heading);
            if (Button(1144, 352, 104, 44, "История", !busy && !voice.Working && !voice.Recording)) OpenCallLog(false);
            if (Button(1264, 352, 104, 44, "Досье", !busy && !voice.Working && !voice.Recording)) OpenCallLog(true);
            string latest = day.Call == CallStatus.NotStarted ? "Начни разговор. Клиент ещё не знает, насколько мы солидны." : LatestClientReply();
            if (latest != shownReply) { shownReply = latest; replyScroll = Vector2.zero; }
            Paint(new Rect(376, 428, 992, 184), BrokerTheme.Hex("#0F1B2D"));
            float height = theme.Text.CalcHeight(new GUIContent(latest), 920);
            replyScroll = GUI.BeginScrollView(new Rect(392, 444, 960, 152), replyScroll, new Rect(0, 0, 928, Mathf.Max(152, height)));
            GUI.Label(new Rect(0, 0, 920, height), latest, theme.Text); GUI.EndScrollView();
            Label(376, 640, 820, 28, day.Call == CallStatus.Active ? "Твоя реплика" : day.DealClosed ? "+$" + (IsEmployeeMode ? BrokerDay.PersonalPayout(day.Current.Commission) : day.Current.Commission) + (IsEmployeeMode ? " лично вам (20%)" : " в кассе") : "Одна линия. Один разговор.", theme.Small);
            bool canTalk = day.Call == CallStatus.Active && !busy && !voice.Recording && !voice.Working;
            bool before = GUI.enabled; GUI.enabled = before && canTalk;
            GUI.SetNextControlName("BrokerMessage"); message = GUI.TextField(new Rect(376, 684, 824, 56), message, 500, theme.Field); GUI.enabled = before;
            if (GUI.enabled && canTalk && GUI.GetNameOfFocusedControl() == "BrokerMessage" && Event.current.type == EventType.KeyDown
                && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            { Event.current.Use(); SendMessageToClient(); }
            if (Button(1216, 684, 152, 56, busy ? "…" : "Сказать", canTalk && !string.IsNullOrWhiteSpace(message), true))
            { SendMessageToClient(); GUI.FocusControl(null); }
            string action = day.Call == CallStatus.NotStarted ? "Позвонить" : !day.Current.ObjectionRaised ? "Предложить сделку" : day.Current.ObjectionResolved ? "Закрыть сделку" : "Повторить вопрос";
            if (Button(376, 764, 300, 56, action, (day.Call == CallStatus.NotStarted && !busy && !voice.Working) || canTalk, true))
            { if (day.Call == CallStatus.NotStarted) PresentClientReply(day.StartCall()); else PresentClientReply(day.OfferDeal()); }
            if (voiceEnabled)
            {
                if (Button(692, 764, 300, 56, voice.Recording ? "Распознать" : "Записать голос", day.Call == CallStatus.Active && !busy && !voice.Working))
                { if (voice.Recording) voice.FinishRecording(); else voice.BeginRecording(); }
            }
            else if (Button(692, 764, 300, 56, "Проверить микрофон", !busy && !voice.Working)) OpenVoiceSetup();
            if (Button(1008, 764, 360, 56, voice.Recording || voice.Working ? "Остановить голос" : "Завершить звонок", (day.Call == CallStatus.Active && !busy) || voice.Recording || voice.Working))
            { if (voice.Recording || voice.Working) voice.Cancel(); else RequestConfirmation(ConfirmationKind.EndCall); }
        }
        private void StageChip(ref float x, string caption, bool done)
        {
            Paint(new Rect(x, 312, 152, 32), done ? BrokerTheme.Hex("#244D4D") : BrokerTheme.Hex("#25354A"));
            Label(x + 12, 316, 128, 24, caption + (done ? " · OK" : " · —"), theme.Small); x += 164;
        }
        private string LatestClientReply()
        {
            string prefix = day.Current.Name + ": ";
            for (int i = Conversation.Count - 1; i >= 0; i--)
                if (Conversation[i].StartsWith(prefix, System.StringComparison.Ordinal)) return Conversation[i].Substring(prefix.Length);
            return "Линия готова.";
        }
        private void DrawTeamWorkspace()
        {
            for (int index = 0; index < 2; index++)
            {
                var worker = day.Employees[index]; float x = 344 + index * 544;
                Panel(x, 184, 512, 664, ""); Label(x + 32, 216, 448, 44, worker.Name, theme.Title);
                Label(x + 32, 276, 448, 28, "Настроение " + worker.Morale + "%", theme.Small);
                Meter(x + 32, 324, 448, worker.Morale / 100f, worker.Morale < 40 ? theme.Gold : theme.Accent);
                Label(x + 32, 364, 448, 40, BrokerPresentation.TaskLabel(worker.Work), theme.Heading);
                Label(x + 32, 412, 448, 28, worker.Work == WorkKind.Idle ? "Готов к поручению" : Mathf.CeilToInt(worker.Remaining) + " с", theme.Small);
                Meter(x + 32, 460, 448, worker.Work == WorkKind.Idle ? 0 : 1 - worker.Remaining / worker.Duration, theme.Accent);
                Label(x + 32, 504, 448, 28, "Досье для клиента", theme.Small);
                for (int client = 0; client < 3; client++)
                    if (Button(x + 32 + client * 152, 548, 144, 48, day.Clients[client].Name, worker.Work == WorkKind.Idle, teamTargets[index] == client)) teamTargets[index] = client;
                bool idle = worker.Work == WorkKind.Idle;
                if (Button(x + 32, 628, 448, 48, "Подготовить досье", idle && !day.Clients[teamTargets[index]].Researched, true)) News(day.AssignTask(index, WorkKind.Research, teamTargets[index]));
                if (Button(x + 32, 692, 216, 48, "Обзвон", idle && !worker.SideDealDone)) News(day.AssignTask(index, WorkKind.Leads, teamTargets[index]));
                if (Button(x + 264, 692, 216, 48, "Перерыв", idle)) News(day.AssignTask(index, WorkKind.Break, teamTargets[index]));
                Label(x + 32, 776, 448, 32, index == 0 ? "Досье 20 с  /  Обзвон $35" : "Досье 35 с  /  Обзвон $55", theme.Small);
            }
        }
        private void DrawOfficeWorkspace()
        {
            if (IsEmployeeMode) { DrawEmploymentConnection(); return; }
            MetricCard(344, 184, 336, "Заработано", "$" + day.Earned);
            MetricCard(704, 184, 336, "Потрачено", "$" + day.Spent);
            MetricCard(1064, 184, 336, "После счетов", "$" + day.FinalBalance);
            Panel(344, 324, 512, 224, ""); Label(376, 352, 448, 36, "Кофе команде", theme.Heading);
            Label(376, 402, 448, 28, "+20 настроения", theme.Small);
            if (Button(376, 464, 448, 56, day.CoffeeBought ? "Уже куплено" : "Купить · $40", !day.CoffeeBought && day.Cash >= 40, true)) News(day.BuyCoffee());
            Panel(888, 324, 512, 224, ""); Label(920, 352, 448, 36, "Золотой телефон", theme.Heading);
            Label(920, 402, 448, 28, "+1 доверия", theme.Small);
            if (Button(920, 464, 448, 56, day.HasGoldenPhone ? "Установлен" : "Купить · $90", !day.HasGoldenPhone && day.Cash >= 90, true)) News(day.BuyGoldenPhone());
            Panel(344, 572, 1056, 276, ""); Label(376, 600, 992, 36, "Связь", theme.Heading);
            bool before = GUI.enabled; GUI.enabled = before && !busy && day.Call != CallStatus.Active;
            useGateway = GUI.Toggle(new Rect(376, 656, 448, 52), useGateway, (useGateway ? "Вкл" : "Выкл") + " · ИИ-формулировки", theme.Button); GUI.enabled = before;
            bool next = GUI.Toggle(new Rect(856, 656, 512, 52), voiceEnabled, (voiceEnabled ? "Вкл" : "Выкл") + " · Голос → внешний API", theme.Button);
            if (next != voiceEnabled) { voiceEnabled = next; if (!next) voice.Cancel(); }
            Label(376, 712, 992, 24, "Голос: запись обрабатывается внешним API только по кнопке.", theme.Small);
            if (Button(376, 740, 992, 56, "Проверить микрофон и связь", !busy && !voice.Working && !voice.Recording)) OpenVoiceSetup();
        }
        private void OpenCallLog(bool profile)
        { voice.Cancel(); profileInLog = profile; showCallLog = true; callLogScroll = Vector2.zero; GUI.FocusControl(null); }
        private void DrawCallLog()
        {
            Scrim(); Panel(220, 100, 1000, 760, "");
            Label(252, 128, 936, 44, day.Current.Name, theme.Title);
            if (Button(252, 196, 200, 48, "История", true, !profileInLog)) profileInLog = false;
            if (Button(468, 196, 200, 48, "Досье", true, profileInLog)) profileInLog = true;
            if (profileInLog)
            {
                Label(252, 288, 936, 44, day.Current.Role, theme.Heading);
                Label(252, 360, 936, 100, day.Current.Brief, theme.Text);
                Label(252, 496, 936, 88, day.NextConversationStep, theme.Text);
                Label(252, 620, 936, 88, "Личный вопрос: " + ClientConversation.Hint(day.Current.Id), theme.Small);
            }
            else
            {
                float height = 8;
                foreach (string line in Conversation) height += theme.Text.CalcHeight(new GUIContent(line), 880) + 24;
                callLogScroll = GUI.BeginScrollView(new Rect(252, 276, 936, 460), callLogScroll, new Rect(0, 0, 896, Mathf.Max(460, height)));
                float y = 4;
                foreach (string line in Conversation)
                { float h = theme.Text.CalcHeight(new GUIContent(line), 880); GUI.Label(new Rect(0, y, 880, h), line, theme.Text); y += h + 24; }
                GUI.EndScrollView();
            }
            if (Button(252, 780, 936, 48, "Назад · Esc", true, true)) CloseUtilityOverlay();
        }
    }
}
