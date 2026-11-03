using Broker.Core;
using UnityEngine;

namespace Broker.Runtime
{
    public sealed partial class PrototypeBootstrap
    {
        private bool showWorkNotebook, workNotebookHistory;
        private Vector2 workNotebookScroll;

        private void OpenWorkNotebook(bool history = false)
        {
            if (!ApplicationActive || (!IsWorkMode && !IsFounderMode) || selectingMode || intro || day.DayEnded || UtilityOverlayOpen
                || busy || voice.Recording || voice.Working) return;
            voice.Cancel(); showWorkNotebook = true; workNotebookHistory = history; workNotebookScroll = Vector2.zero;
            office.Player.SetControl(false); GUI.FocusControl(null);
        }
        private void DrawWorkNotebook()
        {
            Scrim(); Panel(160, 64, 1120, 832, "");
            Label(200, 104, 1040, 32, "РАБОЧИЙ БЛОКНОТ / ВРЕМЯ НА ПАУЗЕ", theme.Tag);
            if (Button(200, 160, 504, 48, "База клиентов", true, !workNotebookHistory))
            { workNotebookHistory = false; workNotebookScroll = Vector2.zero; }
            if (Button(728, 160, 512, 48, IsFounderMode ? "История своей конторы" : "История рабочих дней", true, workNotebookHistory))
            { workNotebookHistory = true; workNotebookScroll = Vector2.zero; }
            Label(200, 228, 1040, 60, !saveSucceeded
                ? "ВНИМАНИЕ: есть незаписанный прогресс. Здесь итоги завершённых дней текущего запуска, не гарантия записи на диск. Текущий день ещё не включён."
                : "Отношения и суммы — по завершённым рабочим дням. Текущий день не включён; сегодняшнее состояние звонков показано отдельно. Вступление в историю рабочих дней не входит.", theme.Small);
            // Measure each text block using the actual GUIStyle: no fixed-height clipping of dossiers.
            float height = WorkNotebookContent(false);
            workNotebookScroll = GUI.BeginScrollView(new Rect(200, 304, 1040, 464), workNotebookScroll,
                new Rect(0, 0, 1008, Mathf.Max(464, height)));
            WorkNotebookContent(true); GUI.EndScrollView();
            Label(200, 784, 1040, 28, "Только просмотр: список звонков, деньги, отношения и файлы не меняются.", theme.Small);
            if (Button(200, 832, 1040, 48, "Вернуться к работе / Esc", true, true)) CloseUtilityOverlay();
        }
        private float NotebookLine(float y, string text, GUIStyle style, bool draw, float minimum = 26)
        {
            float h = Mathf.Max(minimum, style.CalcHeight(new GUIContent(text), 976));
            if (draw) Label(8, y, 976, h, text, style);
            return y + h + 10;
        }
        private float WorkNotebookContent(bool draw)
        {
            float y = 8;
            if (!workNotebookHistory)
            {
                foreach (var client in IsFounderMode ? firmCareer.Clients : workCareer.Clients)
                {
                    var profile = ClientCatalog.Create(client.id);
                    y = NotebookLine(y, profile.Name + " / " + profile.Role, theme.Heading, draw, 34);
                    y = NotebookLine(y, "По завершённым дням: отношения " + Signed(client.relationship) + " · сделок " + client.deals
                        + (client.awaitingReport ? " · остался отчёт по сделке" : " · необсуждённых отчётов нет")
                        + (client.complaintPending ? " · ПРЕТЕНЗИЯ К ГАРАНТИИ" : " · претензий нет"), theme.Small, draw);
                    y = NotebookLine(y, StockHistory.LastPurchase(client, !IsFounderMode, IsFounderMode ? firmCareer.OpeningDayNumber : 0), theme.Small, draw);
                    y = NotebookLine(y, WorkNotebookToday(client), theme.Small, draw);
                    y = NotebookLine(y, "Комиссия фирмы $" + profile.Commission + (IsFounderMode ? " · вся сумма в кассу фирмы. " : " · лично вам $" + BrokerDay.PersonalPayout(profile.Commission) + " (20%). ") + profile.Brief, theme.Small, draw);
                    y += 24;
                }
                return y;
            }
            if (IsFounderMode) return FirmNotebookHistory(y, draw);
            var reports = workCareer.RecentReports;
            if (reports.Length == 0)
                return NotebookLine(y, "Рабочие дни ещё не завершены. Перенесённые накопления вступления не считаются заработком рабочего дня.", theme.Text, draw, 60);
            y = NotebookLine(y, "Показано " + reports.Length + " из " + workCareer.CompletedDays + " завершённых рабочих дней, новые сверху."
                + (workCareer.CompletedDays > reports.Length ? " Более ранние подробности свёрнуты; накопления, сделки, отношения и последние покупки сохранены." : "")
                + StockHistoryNote(workCareer.StockHistoryFromDay, WorkCareerState.FirstWorkDay), theme.Small, draw);
            for (int i = reports.Length - 1; i >= 0; i--)
            {
                var report = reports[i];
                y = NotebookLine(y, "День " + report.day + " · " + report.deals + "/3 сделки · репутация " + report.reputation + "/100", theme.Heading, draw, 34);
                y = NotebookLine(y, "Лично: $" + report.openingSavings + " + $" + report.personalEarned + " = $" + report.savings
                    + ". Комиссия фирмы $" + report.firmRevenue + ", после вашей выплаты осталось $" + (report.firmRevenue - report.personalEarned) + ".", theme.Small, draw);
                for (int c = 0; c < report.clientIds.Length; c++)
                    y = NotebookLine(y, ClientCatalog.Create(report.clientIds[c]).Name + ": "
                        + (report.sold[c] ? "сделка · " + StockHistory.Describe(report.clientIds[c], report.soldStockIds[c], true) : report.closed[c] ? "звонок без сделки" : "не звонили")
                        + (report.guaranteed[c] ? " · ложная гарантия: претензия на следующий звонок" : "")
                        + (report.complaintResolved[c] ? " · претензия урегулирована" : ""), theme.Small, draw);
                y += 24;
            }
            return y;
        }
        private string WorkNotebookToday(WorkClientState client)
        {
            foreach (var c in day.Clients)
            {
                if (c.Id != client.id) continue;
                string state = c.DealClosed ? "сделка закрыта: " + StockHistory.Short(c.Id, c.StockId) + " (в историю попадёт после завершения дня)" : c.Call == CallStatus.Active ? "на линии"
                    : c.Call == CallStatus.Closed ? "звонок завершён без сделки" : "ещё не звонили";
                return "Сегодня в списке: " + state
                    + (IsFounderMode && c.Researched ? ". Досье подготовлено сегодня (+1 доверия, без автоматической продажи)." : "")
                    + (c.FalseGuaranteeMade ? ". Сегодня дана ложная гарантия; претензия останется на следующий звонок." : "")
                    + (c.ComplaintPending ? (c.ComplaintResolved ? ". Претензия урегулирована сегодня." : ". Претензия ещё не урегулирована.") : "")
                    + (c.Returning
                    ? (c.ReportReviewed ? ". Прошлый отчёт обсуждён сегодня." : ". Сначала обсудите прошлый отчёт.") : ".");
            }
            return "Сегодня не в списке. " + (client.lastSeenWorkDay == 0 ? "В этом режиме ещё не появлялся."
                : "Последний раз в списке: день " + (IsFounderMode ? firmCareer.OpeningDayNumber + client.lastSeenWorkDay - 1 : client.lastSeenWorkDay + 3) + ". Это не означает, что вы звонили.");
        }
        // Imported v1/v2 days keep their deals without tickers; recording starts from the next day.
        private static string StockHistoryNote(int fromDay, int firstDay)
            => fromDay > firstDay ? " Бумаги записываются с дня " + fromDay + "; более ранние сделки — до истории акций." : "";
        private float FirmNotebookHistory(float y, bool draw)
        {
            var reports = firmCareer.RecentReports;
            y = NotebookLine(y, "Показано " + reports.Length + " из " + firmCareer.CompletedDays + " завершённых дней своей конторы. Личный резерв $" + firmCareer.PersonalReserve + ". Старые рабочие дни не включены."
                + (firmCareer.CompletedDays > reports.Length ? " Более ранние подробности свёрнуты; последние покупки сохранены в базе клиентов." : "")
                + StockHistoryNote(firmCareer.StockHistoryFromDay, firmCareer.OpeningDayNumber), theme.Small, draw);
            y = NotebookLine(y, firmCareer.HasAssistant ? "Помощник нанят перед днём " + firmCareer.AssistantHireDay
                + ": −$100 до утренней кассы этого дня. Зарплата $40/день. Текущая касса/долг $" + firmCareer.Balance + "."
                : "Помощник не нанят; зарплата $0. Найм доступен утром или в итогах при кассе от $200.", theme.Small, draw);
            for (int i = reports.Length - 1; i >= 0; i--)
            {
                var r = reports[i];
                y = NotebookLine(y, "День " + r.day + " · касса/долг $" + r.balance, theme.Heading, draw, 34);
                y = NotebookLine(y, "Фирма: $" + r.openingBalance + " + $" + r.revenue + " − $" + r.rent + " аренды − $" + r.salary + " зарплаты = $" + r.balance + ". Личной выплаты нет.", theme.Small, draw);
                for (int c = 0; c < 3; c++) y = NotebookLine(y, ClientCatalog.Create(r.clientIds[c]).Name + ": "
                    + (r.sold[c] ? "сделка · " + StockHistory.Describe(r.clientIds[c], r.soldStockIds[c], false) : r.closed[c] ? "звонок без сделки" : "не звонили")
                    + (r.guaranteed[c] ? " · ложная гарантия" : "") + (r.complaintResolved[c] ? " · претензия урегулирована" : ""), theme.Small, draw);
            }
            return y;
        }
    }
}
