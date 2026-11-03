using System;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class StockHistoryTests
    {
        // Simulates a pre-v3 file: no stock fields at all, as v1/v2 JSON had them.
        public static WorkCareerSave Legacy(WorkCareerSave s, int version)
        {
            s.schemaVersion = version; s.stockHistoryFromDay = 0;
            foreach (var clients in new[] { s.origin.clients, s.checkpoint.clients }) foreach (var c in clients) { c.lastStockId = ""; c.lastStockDay = 0; }
            foreach (var r in s.reports) r.soldStockIds = null;
            return s;
        }
        public static FirmCareerSave Legacy(FirmCareerSave s, int version)
        {
            s.schemaVersion = version; s.stockHistoryFromDay = 0;
            foreach (var clients in new[] { s.origin.clients, s.checkpoint.clients }) foreach (var c in clients) { c.lastStockId = ""; c.lastStockDay = 0; }
            foreach (var r in s.reports) r.soldStockIds = null;
            return s;
        }
        // stockId "" keeps the client's default offer.
        public static void SellStock(BrokerDay d, int index, string stockId)
        {
            Assert.That(d.SelectClient(index), Is.True);
            if (stockId != "") { d.SelectStock(stockId); Assert.That(d.Current.StockId, Is.EqualTo(stockId)); }
            d.StartCall(); d.ExplainStockTerms(); WorkCareerTests.SellAfterStarted(d);
        }
        // stocks[i]: null — no call; "" — default offer; otherwise the stock sold to slot i.
        private static BrokerDay Play(WorkCareerState c, params string[] stocks)
        {
            var d = c.CreateDay();
            for (int i = 0; i < stocks.Length; i++) if (stocks[i] != null) SellStock(d, i, stocks[i]);
            d.EndDay(); Assert.That(c.CompleteDay(d), Is.True); return d;
        }
        private static BrokerDay Play(FirmCareerState f, params string[] stocks)
        {
            var d = f.CreateDay();
            for (int i = 0; i < stocks.Length; i++) if (stocks[i] != null) SellStock(d, i, stocks[i]);
            d.EndDay(); Assert.That(f.CompleteDay(d), Is.True); return d;
        }
        private static WorkCareerState NewWork() => WorkCareerState.FromIntro(WorkCareerTests.Intro(true));
        private static WorkClientState Client(WorkClientState[] clients, string id) => clients[ClientCatalog.Index(id)];
        private static WorkCareerState Restored(WorkCareerSave s) { Assert.That(WorkCareerState.TryRestore(s, out var c), Is.True); return c; }

        [Test] public void WorkDayRecordsChosenStockAndDerivesAmounts()
        {
            var c = NewWork(); Assert.That(c.StockHistoryFromDay, Is.EqualTo(4));
            Assert.That(ClientCatalog.Appointments(4), Is.EqualTo(new[] { "boris", "vera", "denis" }));
            Play(c, "utility", "utility");
            var r = c.LastReport; Assert.That(r.soldStockIds, Is.EqualTo(new[] { "utility", "utility", "" }));
            Assert.That(c.Capture().schemaVersion, Is.EqualTo(3));
            var boris = Client(c.Clients, "boris"); Assert.That(boris.lastStockId, Is.EqualTo("utility")); Assert.That(boris.lastStockDay, Is.EqualTo(4));
            Assert.That(Client(c.Clients, "denis").lastStockId, Is.Empty);
            Assert.That(StockHistory.SoldToday(r.clientIds, r.sold, r.soldStockIds), Is.EqualTo("Продано сегодня: Борис — SVET $7500, Вера — SVET $10000."));
            Assert.That(StockHistory.Describe("boris", "utility", true), Is.EqualTo("«Городской Свет» (SVET), вложение $7500, комиссия фирмы $150, лично вам $30"));
            Assert.That(StockHistory.LastPurchase(boris, true), Is.EqualTo("Последняя покупка, день 4: «Городской Свет» (SVET), вложение $7500, комиссия фирмы $150, лично вам $30."));
            Assert.That(StockHistory.LastPurchase(Client(c.Clients, "denis"), true), Is.EqualTo("Покупок в рабочих днях ещё не было."));
            var copy = Restored(c.Capture()); Assert.That(copy.LastReport.soldStockIds, Is.EqualTo(r.soldStockIds));
        }
        [Test] public void LastPurchaseSurvivesSevenDayFoldingAndNewestWins()
        {
            var c = NewWork(); Play(c, "logistics");                      // day 4: Борис
            for (int day = 5; day <= 6; day++) Play(c);
            Play(c, "utility");                                           // day 7: Борис, other stock
            for (int day = 8; day <= 15; day++) Play(c);
            Assert.That(c.RecentReports[0].day, Is.EqualTo(9));
            var boris = Client(c.Clients, "boris"); Assert.That(boris.deals, Is.EqualTo(2));
            Assert.That(boris.lastStockId, Is.EqualTo("utility")); Assert.That(boris.lastStockDay, Is.EqualTo(7));
            Assert.That(Client(Restored(c.Capture()).Clients, "boris").lastStockDay, Is.EqualTo(7));
            Assert.That(ClientCatalog.Appointments(16)[0], Is.EqualTo("boris")); Play(c, "logistics");
            boris = Client(c.Clients, "boris"); Assert.That(boris.lastStockId, Is.EqualTo("logistics")); Assert.That(boris.lastStockDay, Is.EqualTo(16));
            Assert.That(c.Capture().checkpoint.clients[0].lastStockDay, Is.EqualTo(7));
        }
        [Test] public void EveryCatalogStockArtemCanBuyIsRecordedOnHisWorkDay()
        {
            foreach (var stock in StockCatalog.Offers)
            {
                var c = NewWork(); while (Array.IndexOf(ClientCatalog.Appointments(c.NextDayNumber), "artem") < 0) Play(c);
                var stocks = new string[3]; stocks[Array.IndexOf(ClientCatalog.Appointments(c.NextDayNumber), "artem")] = stock.Id; Play(c, stocks);
                var artem = Client(Restored(c.Capture()).Clients, "artem"); Assert.That(artem.lastStockId, Is.EqualTo(stock.Id));
            }
        }
        // Ten days: checkpoint holds days 4-6 (Борис bought RLOG on day 4), the last report leaves slot 2 unsold.
        private static WorkCareerSave Folded()
        {
            var c = NewWork(); Play(c, "logistics", "utility", "logistics");
            for (int n = 0; n < 8; n++) Play(c, "", "", "");
            Play(c, "", ""); return c.Capture();
        }
        [TestCase("unknown")] [TestCase("unsuitable")] [TestCase("stockWithoutSale")] [TestCase("saleWithoutStock")]
        [TestCase("short")] [TestCase("null")] [TestCase("checkpointUnsuitable")] [TestCase("checkpointFuture")]
        [TestCase("checkpointNotAppointed")] [TestCase("checkpointCleared")] [TestCase("checkpointDayOnly")] [TestCase("originStock")]
        [TestCase("historyEarly")] [TestCase("historyLate")] [TestCase("historyAfterSale")] [TestCase("legacyWithStock")]
        public void TamperedWorkStockHistoryIsRejected(string kind)
        {
            var s = Folded(); var r = s.reports[6];
            Assert.That(WorkCareerState.TryRestore(s, out _), Is.True); Assert.That(r.clientIds, Is.EqualTo(new[] { "inna", "oksana", "vera" }));
            var boris = s.checkpoint.clients[0]; Assert.That(boris.lastStockId, Is.EqualTo("logistics")); Assert.That(boris.lastStockDay, Is.EqualTo(4));
            switch (kind)
            {
                case "unknown": r.soldStockIds[0] = "moon"; break;
                case "unsuitable": r.soldStockIds[0] = "ore"; break;
                case "stockWithoutSale": r.soldStockIds[2] = "utility"; break;
                case "saleWithoutStock": r.soldStockIds[0] = ""; break;
                case "short": r.soldStockIds = new[] { "logistics", "" }; break;
                case "null": r.soldStockIds = null; break;
                case "checkpointUnsuitable": boris.lastStockId = "ore"; break;
                case "checkpointFuture": boris.lastStockDay = s.checkpoint.completedWorkDays + 4; break;
                case "checkpointNotAppointed": boris.lastStockDay = 5; break;
                case "checkpointCleared": boris.lastStockId = ""; boris.lastStockDay = 0; break;
                case "checkpointDayOnly": boris.lastStockId = ""; break;
                case "originStock": s.origin.clients[0].lastStockId = "logistics"; s.origin.clients[0].lastStockDay = 4; break;
                case "historyEarly": s.stockHistoryFromDay = 3; break;
                case "historyLate": s.stockHistoryFromDay = s.checkpoint.completedWorkDays + s.reports.Length + 5; break;
                case "historyAfterSale": s.stockHistoryFromDay = s.reports[0].day + 1; break;
                case "legacyWithStock": s.schemaVersion = 2; s.stockHistoryFromDay = 0; break;
            }
            Assert.That(WorkCareerState.TryRestore(s, out _), Is.False);
        }
        [TestCase(1)] [TestCase(2)]
        public void LegacyWorkImportKeepsDealsWithoutInventingTickers(int version)
        {
            var c = NewWork(); for (int n = 0; n < 12; n++) Play(c, "", "", "");
            var s = Legacy(c.Capture(), version); if (version == 1) foreach (var r in s.reports) { r.guaranteed = null; r.complaintResolved = null; }
            var loaded = Restored(s);
            Assert.That(s.schemaVersion, Is.EqualTo(version)); Assert.That(s.reports[0].soldStockIds, Is.Null);
            Assert.That(loaded.Capture().schemaVersion, Is.EqualTo(3)); Assert.That(loaded.StockHistoryFromDay, Is.EqualTo(16));
            Assert.That(loaded.TotalDeals, Is.EqualTo(c.TotalDeals)); Assert.That(loaded.PersonalSavings, Is.EqualTo(c.PersonalSavings));
            foreach (var r in loaded.RecentReports) Assert.That(r.soldStockIds, Is.EqualTo(new[] { "", "", "" }));
            foreach (var client in loaded.Clients)
            {
                Assert.That(client.lastStockId, Is.Empty); Assert.That(client.deals, Is.GreaterThan(0));
                Assert.That(StockHistory.LastPurchase(client, true), Does.Contain("до истории акций"));
            }
            var r0 = loaded.RecentReports[0]; Assert.That(StockHistory.SoldToday(r0.clientIds, r0.sold, r0.soldStockIds), Does.Contain("бумага до истории акций"));
            Play(loaded, "logistics"); Assert.That(loaded.LastReport.soldStockIds[0], Is.EqualTo("logistics"));
            var again = Restored(loaded.Capture()); Assert.That(again.StockHistoryFromDay, Is.EqualTo(16));
            Assert.That(Client(again.Clients, "boris").lastStockDay, Is.EqualTo(16));
            Assert.That(loaded.RestartFromOrigin().StockHistoryFromDay, Is.EqualTo(4));
        }
        [Test] public void LegacyWorkWithMissingArraysOrStockFieldsIsRejected()
        {
            var c = NewWork(); for (int n = 0; n < 3; n++) Play(c, "", "");
            var s = Legacy(c.Capture(), 2); s.reports[0].guaranteed = null; Assert.That(WorkCareerState.TryRestore(s, out _), Is.False);
            s = Legacy(c.Capture(), 2); s.reports[1].soldStockIds = new[] { "logistics", "", "" }; Assert.That(WorkCareerState.TryRestore(s, out _), Is.False);
            s = Legacy(c.Capture(), 2); s.checkpoint.clients[0].lastStockDay = 4; Assert.That(WorkCareerState.TryRestore(s, out _), Is.False);
            s = Legacy(c.Capture(), 2); s.stockHistoryFromDay = 4; Assert.That(WorkCareerState.TryRestore(s, out _), Is.False);
            s = Legacy(c.Capture(), 1); s.origin.clients[1].lastStockId = "utility"; Assert.That(WorkCareerState.TryRestore(s, out _), Is.False);
            s = Legacy(c.Capture(), 2); s.checkpoint = null; Assert.That(WorkCareerState.TryRestore(s, out _), Is.False);
        }

        [Test] public void FounderCarriesStillEmployedPurchaseAndRecordsOwnSales()
        {
            var work = FirmCareerTests.ReadyWork(); var firm = FirmCareerState.FromWork(work);
            Assert.That(firm.StockHistoryFromDay, Is.EqualTo(firm.OpeningDayNumber)); Assert.That(firm.Capture().schemaVersion, Is.EqualTo(3));
            for (int i = 0; i < 6; i++)
            {
                var carried = firm.Clients[i]; var employed = work.Clients[i];
                Assert.That(carried.lastStockId, Is.EqualTo(employed.lastStockId)); Assert.That(carried.lastStockDay, Is.EqualTo(employed.lastStockDay));
                Assert.That(carried.deals, Is.Zero); Assert.That(carried.lastStockDay, Is.LessThan(firm.OpeningDayNumber));
                string text = StockHistory.LastPurchase(carried, false, firm.OpeningDayNumber);
                if (employed.lastStockId == "") Assert.That(text, Is.EqualTo("В своей конторе покупок ещё не было."));
                else Assert.That(text, Does.StartWith("Последняя покупка ещё в найме, день " + employed.lastStockDay + ": «"));
            }
            Assert.That(Array.FindAll(work.Clients, c => c.lastStockId != "").Length, Is.EqualTo(6));
            int opening = firm.OpeningDayNumber; var ids = ClientCatalog.Appointments(opening);
            var stocks = new string[3]; for (int i = 0; i < 3; i++) stocks[i] = StockCatalog.DefaultFor(ids[i]);
            stocks[2] = null; Play(firm, stocks);
            var r = firm.LastReport; Assert.That(r.soldStockIds[2], Is.Empty); Assert.That(r.soldStockIds[0], Is.EqualTo(StockCatalog.DefaultFor(ids[0])));
            var buyer = Client(firm.Clients, ids[0]); Assert.That(buyer.lastStockDay, Is.EqualTo(opening)); Assert.That(buyer.deals, Is.EqualTo(1));
            string own = StockHistory.LastPurchase(buyer, false, opening);
            Assert.That(own, Does.StartWith("Последняя покупка, день " + opening)); Assert.That(own, Does.Contain("комиссия фирмы $")); Assert.That(own, Does.Not.Contain("лично"));
            Assert.That(Client(firm.Clients, ids[2]).lastStockDay, Is.LessThan(opening));
            Assert.That(FirmCareerState.TryRestore(firm.Capture(), out var restored), Is.True); Assert.That(restored.LastReport.soldStockIds, Is.EqualTo(r.soldStockIds));
            var reset = firm.RestartFromOrigin(); Assert.That(reset.StockHistoryFromDay, Is.EqualTo(opening));
            Assert.That(Client(reset.Clients, ids[0]).lastStockDay, Is.EqualTo(Client(work.Clients, ids[0]).lastStockDay));
        }
        [Test] public void FounderLastPurchaseSurvivesFolding()
        {
            var firm = FirmCareerTests.NewFirm(); string first = ClientCatalog.Appointments(firm.OpeningDayNumber)[0];
            var stocks = new string[1]; stocks[0] = StockCatalog.DefaultFor(first); Play(firm, stocks);
            for (int n = 0; n < 9; n++) FirmCareerTests.Finish(firm);
            var client = Client(firm.Clients, first); Assert.That(firm.RecentReports[0].day, Is.GreaterThan(firm.OpeningDayNumber));
            Assert.That(client.lastStockDay, Is.EqualTo(firm.OpeningDayNumber)); Assert.That(client.lastStockId, Is.EqualTo(stocks[0]));
            Assert.That(FirmCareerState.TryRestore(firm.Capture(), out _), Is.True);
        }
        [TestCase("unsuitable")] [TestCase("saleWithoutStock")] [TestCase("stockWithoutSale")] [TestCase("null")]
        [TestCase("historyEarly")] [TestCase("historyLate")] [TestCase("carriedWithDeals")] [TestCase("carriedAtOpening")]
        [TestCase("buyerCleared")] [TestCase("originUnsuitable")] [TestCase("legacyWithStock")]
        public void TamperedFounderStockHistoryIsRejected(string kind)
        {
            var firm = FirmCareerTests.NewFirm(); for (int n = 0; n < 9; n++) FirmCareerTests.Finish(firm, true);
            Play(firm, "", "");
            var s = firm.Capture(); var r = s.reports[6]; Assert.That(FirmCareerState.TryRestore(s, out _), Is.True);
            int buyer = Array.FindIndex(s.checkpoint.clients, c => c.deals > 0 && c.lastStockDay >= s.sourceDay);
            int carried = Array.FindIndex(s.origin.clients, c => !string.IsNullOrEmpty(c.lastStockId));
            int both = Array.FindIndex(s.checkpoint.clients, c => c.deals > 0 && !string.IsNullOrEmpty(s.origin.clients[ClientCatalog.Index(c.id)].lastStockId));
            Assert.That(buyer, Is.GreaterThanOrEqualTo(0)); Assert.That(carried, Is.GreaterThanOrEqualTo(0)); Assert.That(both, Is.GreaterThanOrEqualTo(0));
            Assert.That(r.sold, Is.EqualTo(new[] { true, true, false }));
            switch (kind)
            {
                case "unsuitable": r.soldStockIds[Array.FindIndex(r.clientIds, id => id != "artem")] = "ore"; break;
                case "saleWithoutStock": r.soldStockIds[1] = ""; break;
                case "stockWithoutSale": r.soldStockIds[2] = StockCatalog.DefaultFor(r.clientIds[2]); break;
                case "null": r.soldStockIds = null; break;
                case "historyEarly": s.stockHistoryFromDay = s.sourceDay - 1; break;
                case "historyLate": s.stockHistoryFromDay = s.sourceDay + 11; break;
                case "carriedWithDeals": s.checkpoint.clients[both].lastStockDay = s.origin.clients[both].lastStockDay;
                    s.checkpoint.clients[both].lastStockId = s.origin.clients[both].lastStockId; break;
                case "carriedAtOpening": s.origin.clients[carried].lastStockDay = s.sourceDay; break;
                case "buyerCleared": s.checkpoint.clients[buyer].lastStockId = ""; s.checkpoint.clients[buyer].lastStockDay = 0; break;
                case "originUnsuitable": s.origin.clients[carried].lastStockId = "moon"; break;
                case "legacyWithStock": s.schemaVersion = 2; s.stockHistoryFromDay = 0; break;
            }
            Assert.That(FirmCareerState.TryRestore(s, out _), Is.False);
        }
        [TestCase(1)] [TestCase(2)]
        public void LegacyFounderImportStartsStockHistoryNextDay(int version)
        {
            var firm = FirmCareerTests.NewFirm(); for (int n = 0; n < 10; n++) FirmCareerTests.Finish(firm, true);
            var s = Legacy(firm.Capture(), version); Assert.That(FirmCareerState.TryRestore(s, out var loaded), Is.True);
            Assert.That(s.schemaVersion, Is.EqualTo(version)); Assert.That(loaded.Capture().schemaVersion, Is.EqualTo(3));
            Assert.That(loaded.StockHistoryFromDay, Is.EqualTo(firm.NextDayNumber)); Assert.That(loaded.Balance, Is.EqualTo(firm.Balance));
            foreach (var r in loaded.RecentReports) Assert.That(r.soldStockIds, Is.EqualTo(new[] { "", "", "" }));
            foreach (var c in loaded.Clients)
            { Assert.That(c.lastStockId, Is.Empty); Assert.That(StockHistory.LastPurchase(c, false, loaded.OpeningDayNumber), Does.Contain("до истории акций")); }
            FirmCareerTests.Finish(loaded, true); Assert.That(loaded.LastReport.soldStockIds[0], Is.Not.Empty);
            Assert.That(FirmCareerState.TryRestore(loaded.Capture(), out _), Is.True);
            var reset = loaded.RestartFromOrigin(); Assert.That(reset.StockHistoryFromDay, Is.EqualTo(reset.OpeningDayNumber));
            foreach (var c in reset.Clients) Assert.That(StockHistory.LastPurchase(c, false, reset.OpeningDayNumber), Is.EqualTo("В своей конторе покупок ещё не было."));
        }
        [TestCase("boris", 7500)] [TestCase("inna", 11000)] [TestCase("artem", 9000)] [TestCase("vera", 10000)] [TestCase("denis", 8500)] [TestCase("oksana", 9500)]
        public void InvestmentIsDerivedFromUnchangedTwoPercentCommission(string id, int investment)
        {
            Assert.That(StockHistory.Investment(id), Is.EqualTo(investment));
            Assert.That(StockHistory.Investment(id) * StockCatalog.FeePercent / 100, Is.EqualTo(ClientCatalog.Create(id).Commission));
        }
        [Test] public void TextsNeverGuessMissingStock()
        {
            Assert.That(StockHistory.SoldToday(new[] { "boris", "vera", "denis" }, new bool[3], new[] { "", "", "" }), Is.EqualTo("Сегодня сделок нет."));
            Assert.That(StockHistory.SoldToday(new[] { "boris", "vera", "denis" }, new[] { true, false, false }, null), Is.EqualTo("Продано сегодня: Борис — бумага до истории акций."));
            Assert.That(StockHistory.Describe("boris", "", false), Is.EqualTo("до истории акций: бумага не записана"));
            Assert.That(StockHistory.Describe("artem", "ore", false), Is.EqualTo("«Новые Недра» (NEDR), вложение $9000, комиссия фирмы $180"));
            Assert.That(StockHistory.Short("inna", "medical"), Is.EqualTo("MDAT $11000"));
        }
    }
}
