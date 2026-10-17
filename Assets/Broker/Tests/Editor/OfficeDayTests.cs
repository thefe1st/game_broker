using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class OfficeDayTests
    {
        private static void Qualify(BrokerDay day)
        { day.StartCall(); day.Say("Какая цель? Есть риск убытка. Комиссия 2%. Срок — год. Персональный отчёт."); }
        [Test] public void ThreeDistinctClientsAndTwoEmployeesExist()
        { var day = new BrokerDay(); Assert.That(day.Clients.Count, Is.EqualTo(3)); Assert.That(day.Employees.Count, Is.EqualTo(2)); }
        [Test] public void CannotSwitchDuringActiveCall()
        { var day = new BrokerDay(); day.StartCall(); Assert.That(day.SelectClient(1), Is.False); Assert.That(day.Current.Id, Is.EqualTo("boris")); }
        [Test] public void AllDisclosuresInOneMessageAreRecognized()
        { var day = new BrokerDay(); Qualify(day); Assert.That(day.NeedsKnown && day.RisksExplained && day.FeesExplained, Is.True); }
        [Test] public void KnownNegatedGuaranteeDoesNotLoseClient()
        { var day = new BrokerDay(); day.StartCall(); day.Say("Не гарантирую прибыль, есть риск убытка."); Assert.That(day.Call, Is.EqualTo(CallStatus.Active)); Assert.That(day.RisksExplained, Is.True); }
        [Test] public void ZeroPercentDoesNotCountAsTwoPercentFee()
        { var day = new BrokerDay(); day.StartCall(); day.Say("Комиссия 0%."); Assert.That(day.FeesExplained, Is.False); }
        [Test] public void EachClientCanPayOnlyOnce()
        {
            var day = new BrokerDay();
            for (int i = 0; i < 3; i++) { Assert.That(day.SelectClient(i), Is.True); Qualify(day); day.OfferDeal(); day.OfferDeal(); }
            Assert.That(day.Cash, Is.EqualTo(550)); Assert.That(day.DealsCount, Is.EqualTo(3)); Assert.That(day.Earned, Is.EqualTo(550));
        }
        [Test] public void InnaRequiresInvestmentHorizon()
        {
            var day = new BrokerDay(); day.SelectClient(1); day.StartCall(); day.Say("Какая цель? Есть риск. Комиссия 2%."); day.OfferDeal();
            Assert.That(day.DealClosed, Is.False); day.Say("Срок — год."); day.OfferDeal(); Assert.That(day.DealClosed, Is.True);
        }
        [Test] public void ArtemRequiresPersonalReport()
        {
            var day = new BrokerDay(); day.SelectClient(2); day.StartCall(); day.Say("Какая цель? Есть риск. Комиссия 2%."); day.OfferDeal();
            Assert.That(day.DealClosed, Is.False); day.Say("Предоставим персональный отчёт."); day.OfferDeal(); Assert.That(day.DealClosed, Is.True);
        }
        [Test] public void ResearchTargetsAssignedClientNotCurrentSelection()
        {
            var day = new BrokerDay(); day.AssignTask(0, WorkKind.Research, 1); day.SelectClient(2); day.AdvanceTime(21);
            Assert.That(day.Clients[1].Researched, Is.True); Assert.That(day.Clients[2].Researched, Is.False);
        }
        [Test] public void DuplicateConcurrentResearchDoesNotFarmTrust()
        {
            var day = new BrokerDay(); day.AssignTask(0, WorkKind.Research, 1); day.AssignTask(1, WorkKind.Research, 1); day.AdvanceTime(36);
            Assert.That(day.Clients[1].Trust, Is.EqualTo(1));
        }
        [Test] public void SideDealHasOnePayoutPerEmployee()
        {
            var day = new BrokerDay(); day.AssignTask(1, WorkKind.Leads, 0); day.AdvanceTime(41); day.AssignTask(1, WorkKind.Leads, 0); day.AdvanceTime(1);
            Assert.That(day.Cash, Is.EqualTo(55)); Assert.That(day.Employees[1].Work, Is.EqualTo(WorkKind.Idle));
        }
        [Test] public void BusyEmployeeCannotBeReassigned()
        {
            var day = new BrokerDay(); day.AssignTask(0, WorkKind.Research, 0); day.AssignTask(0, WorkKind.Leads, 0);
            Assert.That(day.Employees[0].Work, Is.EqualTo(WorkKind.Research));
        }
        [Test] public void EventPausesClockAndWorkers()
        {
            var day = new BrokerDay(); day.AdvanceTime(60); Assert.That(day.PendingEvent, Is.Not.Null);
            float remaining = day.RemainingSeconds; day.AdvanceTime(100); Assert.That(day.RemainingSeconds, Is.EqualTo(remaining));
        }
        [Test] public void PaidChoiceWithoutMoneyKeepsEventPending()
        {
            var day = new BrokerDay(); day.AdvanceTime(60); day.ResolveEvent(0);
            Assert.That(day.PendingEvent, Is.Not.Null); Assert.That(day.Cash, Is.Zero); day.ResolveEvent(1); Assert.That(day.PendingEvent, Is.Null);
        }
        [Test] public void ThreeEventsResolveOnlyOnceAndDeferralAddsExpense()
        {
            var day = new BrokerDay(); day.AdvanceTime(60); day.ResolveEvent(1); day.ResolveEvent(1);
            day.AdvanceTime(120); Assert.That(day.PendingEvent.Id, Is.EqualTo(1)); day.ResolveEvent(1);
            day.AdvanceTime(120); Assert.That(day.PendingEvent.Id, Is.EqualTo(2)); day.ResolveEvent(1); day.ResolveEvent(1);
            Assert.That(day.TotalExpenses, Is.EqualTo(440));
        }
        [Test] public void GoldenPhoneCostsMoneyAndCannotBeBoughtTwice()
        {
            var day = new BrokerDay(); Qualify(day); day.OfferDeal(); day.BuyGoldenPhone(); day.BuyGoldenPhone();
            Assert.That(day.Cash, Is.EqualTo(60)); Assert.That(day.Spent, Is.EqualTo(90)); Assert.That(day.Clients[1].Trust, Is.EqualTo(1));
        }
        [Test] public void CoffeeCannotBeBoughtWithoutFunds()
        { var day = new BrokerDay(); day.BuyCoffee(); Assert.That(day.CoffeeBought, Is.False); Assert.That(day.Cash, Is.Zero); }
        [Test] public void BreakImprovesMoraleButDoesNotPayCash()
        { var day = new BrokerDay(); day.AssignTask(0, WorkKind.Break, 0); day.AdvanceTime(19); Assert.That(day.Employees[0].Morale, Is.EqualTo(80)); Assert.That(day.Cash, Is.Zero); }
        [Test] public void EndedDayCannotEarnOrAssignNewWork()
        {
            var day = new BrokerDay(); day.AssignTask(1, WorkKind.Leads, 0); day.EndDay(); day.AdvanceTime(100); day.AssignTask(0, WorkKind.Leads, 0);
            Assert.That(day.Cash, Is.Zero); Assert.That(day.Employees[0].Work, Is.EqualTo(WorkKind.Idle));
        }
        [Test] public void CoffeeCanIncreaseSideDealPayoutAndAccountingBalances()
        {
            var day = new BrokerDay(); Qualify(day); day.OfferDeal(); day.BuyCoffee();
            day.AssignTask(1, WorkKind.Leads, 0); day.AdvanceTime(41);
            Assert.That(day.Cash, Is.EqualTo(180)); Assert.That(day.Cash, Is.EqualTo(day.Earned - day.Spent));
        }
        [Test] public void LowMoraleMakesWorkSlower()
        {
            var day = new BrokerDay(); day.AdvanceTime(60); day.ResolveEvent(1);
            for (int i = 0; i < 3; i++) { day.AssignTask(0, WorkKind.Research, i); day.AdvanceTime(20); }
            day.AssignTask(0, WorkKind.Leads, 0);
            Assert.That(day.Employees[0].Duration, Is.EqualTo(60));
        }
        [Test] public void TwelvePercentDoesNotCountAsTwoPercent()
        {
            var day = new BrokerDay(); day.StartCall(); day.Say("Комиссия 12%.");
            Assert.That(day.FeesExplained, Is.False); day.Say("Комиссия 0.2%."); Assert.That(day.FeesExplained, Is.False);
        }
        [Test] public void ThreeDealsAndPaidEventChoicesCanCoverAllExpenses()
        {
            var day = new BrokerDay();
            for (int i = 0; i < 3; i++)
            {
                day.SelectClient(i); Qualify(day); day.OfferDeal(); day.AdvanceTime(0.1f);
                Assert.That(day.PendingEvent, Is.Not.Null); Assert.That(day.PendingEvent.Id, Is.EqualTo(i));
                day.ResolveEvent(0);
            }
            day.EndDay(); Assert.That(day.Successful, Is.True); Assert.That(day.FinalBalance, Is.EqualTo(25));
            Assert.That(day.Cash, Is.EqualTo(day.Earned - day.Spent));
        }
        [Test] public void EndOfDayIncludesPayrollNotJustRent()
        { var day = new BrokerDay(); Qualify(day); day.OfferDeal(); day.EndDay(); Assert.That(day.RentCovered, Is.True); Assert.That(day.Successful, Is.False); Assert.That(day.FinalBalance, Is.EqualTo(-190)); }
    }
}
