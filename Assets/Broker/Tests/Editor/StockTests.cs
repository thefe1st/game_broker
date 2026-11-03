using System;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class StockTests
    {
        private static BrokerDay Day(string id = "artem")
        { var d = new BrokerDay(mode: CareerMode.Employee); d.SelectClient(ClientCatalog.Index(id)); return d; }
        [Test] public void CatalogHasSixUniqueImmutableFictionalOffers()
        {
            var offers = StockCatalog.Offers; Assert.That(offers.Length, Is.EqualTo(6));
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var s in offers) { Assert.That(ids.Add(s.Id), Is.True); Assert.That(s.Name, Is.Not.Empty); Assert.That(s.RiskDisclosure, Does.Contain("риск")); }
            offers[0] = null; Assert.That(StockCatalog.Offers[0], Is.Not.Null); Assert.That(StockCatalog.Find("unknown"), Is.Null);
        }
        [TestCase("boris")] [TestCase("inna")] [TestCase("artem")] [TestCase("vera")] [TestCase("denis")] [TestCase("oksana")]
        public void DefaultOfferFitsClientAndInvestmentMatchesUnchangedCommission(string id)
        {
            var c = ClientCatalog.Create(id); var s = StockCatalog.Find(c.StockId);
            Assert.That(StockCatalog.Fits(id, s), Is.True); Assert.That(c.Commission * 100 / StockCatalog.FeePercent * 2 / 100, Is.EqualTo(c.Commission));
        }
        [Test] public void SelectionIsPerClientAndDoesNotSpendOrCancelAssistant()
        {
            var firm = FirmCareerTests.NewFirm(); firm.TryHireAssistant(out firm); var d = firm.CreateDay();
            d.AssignTask(0, WorkKind.Research, 0); string other = d.Clients[1].StockId; int trust = d.Trust;
            d.SelectStock("utility"); Assert.That(d.Clients[1].StockId, Is.EqualTo(other)); Assert.That(d.Trust, Is.EqualTo(trust));
            Assert.That(d.Cash, Is.EqualTo(200)); Assert.That(d.Earned + d.Spent, Is.Zero); Assert.That(d.Employees[0].Work, Is.EqualTo(WorkKind.Research));
        }
        [Test] public void ChangingOfferResetsProductDisclosureButNotGoalReportComplaintOrTrust()
        {
            var d = Day(); d.StartCall(); d.Say("Какая цель? Есть риск убытка. Комиссия 2%. Персональный отчёт."); int trust = d.Trust;
            d.SelectStock("utility"); Assert.That(d.NeedsKnown && d.Current.ReportReviewed && d.Current.SpecialDiscussed, Is.True);
            Assert.That(d.RisksExplained || d.FeesExplained || d.Current.StockRiskExplained, Is.False); Assert.That(d.Trust, Is.EqualTo(trust));
            d.ExplainStockTerms(); d.SelectStock("utility"); Assert.That(d.RisksExplained && d.FeesExplained, Is.True);
        }
        [TestCase("ore")] [TestCase("biotech")]
        public void SpeculativeStockRequiresSpecificDisclosureAndStillNeedsClientQuestion(string stock)
        {
            var d = Day(); d.SelectStock(stock); d.StartCall();
            d.Say("Какая цель? Есть риск убытка. Комиссия 2%. Персональный отчёт.");
            Assert.That(d.ConversationStep, Is.EqualTo(ConversationStage.Risk)); d.OfferDeal(); Assert.That(d.Current.ObjectionRaised, Is.False);
            d.ExplainStockTerms(); Assert.That(d.Current.StockRiskExplained, Is.True); d.OfferDeal();
            Assert.That(d.Current.ObjectionRaised, Is.True); Assert.That(d.DealClosed, Is.False);
            d.Say("Личный менеджер, буду на связи."); string reply = d.OfferDeal();
            Assert.That(d.DealClosed, Is.True); Assert.That(d.FirmRevenue, Is.EqualTo(180)); Assert.That(d.Earned, Is.EqualTo(36));
            Assert.That(reply, Does.Contain(d.CurrentStock.Name)); Assert.That(reply, Does.Contain("вложение $9000"));
            d.OfferDeal(); Assert.That(d.Earned, Is.EqualTo(36));
        }
        [TestCase("boris")] [TestCase("inna")]
        public void UnsuitableOfferDoesNotCloseOrLockSelectionEvenAfterTerms(string id)
        {
            var d = Day(id); d.SelectStock("ore"); d.StartCall(); d.Say("Какая цель? Срок год."); d.ExplainStockTerms();
            Assert.That(d.OfferDeal(), Does.Contain("не готов")); Assert.That(d.Current.ObjectionRaised || d.DealClosed, Is.False);
            Assert.That(d.CanSelectStock, Is.True); d.SelectStock("utility"); Assert.That(d.Current.StockId, Is.EqualTo("utility"));
        }
        [Test] public void AcceptedProposalLocksOfferUntilClosedAndNoHiddenDisclosureOutsideCall()
        {
            var d = Day(); d.ExplainStockTerms(); Assert.That(d.RisksExplained || d.FeesExplained, Is.False);
            d.StartCall(); d.Say("Какая цель? Персональный отчёт."); d.ExplainStockTerms(); d.OfferDeal();
            d.SelectStock("utility"); Assert.That(d.Current.StockId, Is.EqualTo("cloud")); Assert.That(d.CanSelectStock, Is.False);
            d.EndCall(); d.SelectStock("ore"); d.ExplainStockTerms(); Assert.That(d.Current.StockId, Is.EqualTo("cloud"));
        }
        [Test] public void ExactDisclosureDoesNotRepairComplaintOrReviewOldReport()
        {
            var d = new BrokerDay(4, mode: CareerMode.EmployeeWork, clientIds: ClientCatalog.Appointments(4),
                returning: new[] { true, false, false }, complaints: new[] { true, false, false });
            d.StartCall(); d.ExplainStockTerms(); Assert.That(d.Current.ReportReviewed || d.Current.ComplaintResolved, Is.False);
            Assert.That(d.OfferDeal(), Does.Contain("претензию")); Assert.That(d.Earned, Is.Zero);
        }
        [TestCase("Высокий риск убытка: можно потерять всю сумму, покупателя может не оказаться.", true)]
        [TestCase("Есть риск убытка.", false)]
        [TestCase("Не высокий риск, нельзя потерять всю сумму, покупателя может не оказаться.", false)]
        public void NarrowSpeculativePhraseRulesAreExplicit(string text, bool expected)
        { Assert.That(StockDialogueRules.DisclosesSpeculativeRisk(text), Is.EqualTo(expected)); }
        [TestCase("Не раскрываю высокий риск. Можно потерять всю сумму. Покупателя может не оказаться.")]
        [TestCase("Высокий риск. Не можно потерять всю сумму. Покупателя может не оказаться.")]
        public void NegatedSpecificDisclosureDoesNotPassNarrowRules(string text)
        { Assert.That(StockDialogueRules.DisclosesSpeculativeRisk(text), Is.False); }
        [Test] public void InvalidEndedOrEventSelectionCannotChangeTerms()
        {
            var d = new BrokerDay(); d.StartCall(); d.Say("Есть риск убытка. Комиссия 2%.");
            string stock = d.Current.StockId; d.SelectStock("unknown"); Assert.That(d.RisksExplained && d.FeesExplained, Is.True);
            d.AdvanceTime(60); Assert.That(d.PendingEvent, Is.Not.Null); d.SelectStock("biotech"); d.ExplainStockTerms();
            Assert.That(d.Current.StockId, Is.EqualTo(stock)); d.EndDay(); d.SelectStock("ore");
            Assert.That(d.Current.StockId, Is.EqualTo(stock));
        }
        [Test] public void ExplicitTermsDoNotGrantTrustOrDiscloseNeedsOnRepeat()
        {
            var d = Day(); d.SelectStock("ore"); d.StartCall(); int trust = d.Trust;
            for (int n = 0; n < 10; n++) d.ExplainStockTerms();
            Assert.That(d.Trust, Is.EqualTo(trust)); Assert.That(d.NeedsKnown || d.Current.SpecialDiscussed || d.Current.ObjectionRaised, Is.False);
            Assert.That(d.Spent + d.Earned, Is.Zero); Assert.That(d.Call, Is.EqualTo(CallStatus.Active));
        }
        [Test] public void EveryCatalogSaleSettlesAndIsRecordedInFounderHistoryWithItsRateAndQuote()
        {
            foreach (var s in StockCatalog.Offers)
            {
                var firm = FirmCareerTests.NewFirm(); BrokerDay d; int artem;
                // v5: a day without a price concern for Artem; concerns and deep drops have their own tests.
                while (true)
                {
                    d = firm.CreateDay(); artem = Array.IndexOf(ClientCatalog.Appointments(d.DayNumber), "artem");
                    if (artem >= 0 && !d.Clients[artem].PriceConcern) break;
                    d.EndDay(); Assert.That(firm.CompleteDay(d), Is.True);
                }
                d.SelectClient(artem);
                d.SelectStock(s.Id); d.StartCall(); d.Say("Какая цель? Обсудим отчёт. Персональный отчёт."); d.ExplainStockTerms();
                d.OfferDeal(); d.Say("Личный менеджер, буду на связи."); d.OfferDeal(); Assert.That(d.DealClosed, Is.True);
                d.EndDay(); Assert.That(firm.CompleteDay(d), Is.True); Assert.That(FirmCareerState.TryRestore(firm.Capture(), out var restored), Is.True);
                Assert.That(firm.Capture().schemaVersion, Is.EqualTo(5)); int slot = Array.IndexOf(restored.LastReport.clientIds, "artem");
                Assert.That(restored.LastReport.soldStockIds[slot], Is.EqualTo(s.Id));
                Assert.That(restored.LastReport.soldPrices[slot], Is.EqualTo(StockMarket.PriceCents(s.Id, restored.LastReport.day)));
                Assert.That(restored.LastReport.revenue, Is.EqualTo(9000 * s.FeePercent / 100));
                Assert.That(restored.Clients[ClientCatalog.Index("artem")].lastStockId, Is.EqualTo(s.Id));
            }
        }
    }
}
