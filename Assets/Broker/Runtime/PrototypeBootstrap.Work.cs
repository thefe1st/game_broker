using Broker.Core;
using UnityEngine;

namespace Broker.Runtime
{
    public sealed partial class PrototypeBootstrap
    {
        private void RequestWorkDays()
        {
            if (!ApplicationActive || !saveSucceeded) return;
            if (workStorage.TryLoad(out workCareer, out workLoadNotice, out workLoadStatus)) { LoadMode(CareerMode.EmployeeWork); return; }
            if (workLoadStatus == WorkCareerLoadStatus.Unreadable) { saveNotice = workLoadNotice; News(workLoadNotice); return; }
            if (employment.IsIntroComplete) RequestConfirmation(ConfirmationKind.StartWork);
        }
        private void StartWorkFromIntro()
        {
            if (!ApplicationActive || !saveSucceeded || !employment.IsIntroComplete) return;
            // The confirmation can stay open while files are restored externally.
            if (workStorage.TryLoad(out workCareer, out workLoadNotice, out workLoadStatus))
            { CloseUtilityOverlay(); LoadMode(CareerMode.EmployeeWork); return; }
            if (workLoadStatus != WorkCareerLoadStatus.Missing) return;
            var candidate = WorkCareerState.FromIntro(employment);
            if (!workStorage.TryCreate(candidate, out workLoadNotice)) return;
            CloseUtilityOverlay(); LoadMode(CareerMode.EmployeeWork);
        }
        private void DrawWorkResults()
        {
            var report = workCareer.LastReport;
            if (report == null) return;
            Scrim(); Panel(200, 76, 1040, 808, "");
            Label(240, 112, 960, 28, "РАБОЧИЙ ДЕНЬ " + report.day + " ЗАВЕРШЁН / КАРЬЕРА ПРОДОЛЖАЕТСЯ", theme.Tag);
            Label(240, 168, 960, 64, "Завтра снова на линии.", theme.Title);
            MetricCard(240, 264, 296, "Ваши накопления", "$" + workCareer.PersonalSavings);
            MetricCard(560, 264, 296, "Лично за день", "+$" + report.personalEarned, report.deals + "/3 сделки");
            MetricCard(880, 264, 320, "Репутация", workCareer.Reputation + "/100");
            var clients = workCareer.Clients;
            for (int i = 0; i < clients.Length; i++)
            {
                var c = clients[i];
                Label(240 + (i / 3) * 488, 412 + (i % 3) * 40, 472, 36,
                    ClientCatalog.Create(c.id).Name + " · отношения " + Signed(c.relationship) + (c.awaitingReport ? " · ждёт отчёт" : "") + (c.complaintPending ? " · претензия" : ""), theme.Small);
            }
            Label(240, 536, 960, 64, StockHistory.SoldToday(report.clientIds, report.sold, report.soldStockIds, report.soldPrices) + " За рабочие дни: " + workCareer.CompletedDays
                + " дней, " + workCareer.TotalDeals + " сделок. Фирме сегодня $" + report.firmRevenue + ". Ориентир — без штрафов.", theme.Small);
            Label(240, 604, 960, 44, day.PriceReactionsSummary, theme.Small);
            var next = ClientCatalog.Appointments(workCareer.NextDayNumber);
            Label(240, 652, 960, 32, "Завтра: " + ClientCatalog.Create(next[0]).Name + ", " + ClientCatalog.Create(next[1]).Name + ", " + ClientCatalog.Create(next[2]).Name + ".", theme.Heading);
            Label(240, 688, 960, 44, saveNotice, theme.Small);
            if (Button(240, 740, 464, 48, saveSucceeded ? "Следующий рабочий день" : "Продолжить без записи", true, true)) Restart();
            if (Button(736, 740, 464, 48, saveSucceeded ? "Сбросить рабочие дни" : "Повторить сохранение"))
            { if (saveSucceeded) RequestConfirmation(ConfirmationKind.NewCareer); else SaveCareer(); }
            if (Button(240, 812, 464, 40, "Выбрать режим", saveSucceeded)) selectingMode = true;
            if (Button(736, 812, 464, 40, "Карьера")) OpenCareerPath();
        }
    }
}
