using System;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class CareerTests
    {
        private static void Sell(BrokerDay day, int client)
        {
            day.SelectClient(client); day.StartCall();
            day.Say("Какая цель? Есть риск убытка. Комиссия 2%. Срок год. Персональный отчёт.");
            day.OfferDeal(); Assert.That(day.DealClosed, Is.True);
        }
        private static BrokerDay Finish(CareerState career, bool sell = true)
        {
            var day = career.CreateDay();
            if (sell) for (int i = 0; i < 3; i++) Sell(day, i);
            day.EndDay(); Assert.That(career.CompleteDay(day), Is.True); return day;
        }
        [Test] public void NewCareerHasThreeDaysAndNoMoney()
        {
            var c = new CareerState(); var d = c.CreateDay();
            Assert.That(d.DayNumber, Is.EqualTo(1)); Assert.That(d.Cash, Is.Zero);
            Assert.That(d.TotalExpenses, Is.EqualTo(340)); Assert.That(c.IsFinished, Is.False);
        }
        [Test] public void ActiveDayAndNullCannotSettle()
        { var c = new CareerState(); Assert.That(c.CompleteDay(c.CreateDay()), Is.False); Assert.That(c.CompleteDay(null), Is.False); }
        [Test] public void SettlementCannotRepeatOrSkipDay()
        {
            var c = new CareerState(); var wrong = new BrokerDay(2); wrong.EndDay();
            Assert.That(c.CompleteDay(wrong), Is.False); var d = Finish(c);
            Assert.That(c.CompleteDay(d), Is.False); Assert.That(c.CompletedDays, Is.EqualTo(1));
        }
        [Test] public void ThreeProfitableDaysEndCampaignWithoutFourth()
        {
            var c = new CareerState(); Finish(c); Finish(c); Finish(c);
            Assert.That(c.IsFinished && c.Won, Is.True); Assert.That(c.TotalDeals, Is.EqualTo(9));
            Assert.That(c.Balance, Is.EqualTo(510)); Assert.Throws<InvalidOperationException>(() => c.CreateDay());
        }
        [Test] public void DebtCarriesOnceAndDailyCostsIncrease()
        {
            var c = new CareerState(); Finish(c, false); var d = c.CreateDay();
            Assert.That(d.OpeningCash, Is.Zero); Assert.That(d.CarryDebt, Is.EqualTo(340));
            Assert.That(d.DailyRent + d.DailyPayroll, Is.EqualTo(380)); Assert.That(d.TotalExpenses, Is.EqualTo(720));
            d.EndDay(); c.CompleteDay(d); Assert.That(c.Balance, Is.EqualTo(-720));
            d = c.CreateDay(); Assert.That(d.TotalExpenses, Is.EqualTo(1140)); d.EndDay(); c.CompleteDay(d);
            Assert.That(c.Balance, Is.EqualTo(-1140)); Assert.That(c.Won, Is.False);
        }
        [Test] public void RestartingCurrentDayDoesNotAwardOrSpendMoney()
        {
            var c = new CareerState(); Finish(c); var a = c.CreateDay(); Sell(a, 0);
            var b = c.CreateDay(); Assert.That(b.Cash, Is.EqualTo(210)); Assert.That(b.Earned, Is.Zero);
            Assert.That(c.Balance, Is.EqualTo(210));
        }
        [Test] public void PhoneAndRelationshipsPersistWithoutRepeatPurchase()
        {
            var c = new CareerState(); var d = c.CreateDay(); Sell(d, 0); d.BuyGoldenPhone(); d.EndDay(); c.CompleteDay(d);
            var next = c.CreateDay(); Assert.That(next.HasGoldenPhone, Is.True);
            Assert.That(next.Clients[0].Trust, Is.EqualTo(3)); Assert.That(next.Clients[0].Returning, Is.True);
            next.BuyGoldenPhone(); Assert.That(next.Spent, Is.Zero);
        }
        [Test] public void ReturningClientRequiresReportBeforeNewDeal()
        {
            var c = new CareerState(); Finish(c); var d = c.CreateDay(); d.StartCall();
            d.Say("Какая цель? Есть риск. Комиссия 2%."); d.OfferDeal(); Assert.That(d.DealClosed, Is.False);
            d.Say("Вот отчёт по прошлому разговору."); d.OfferDeal(); Assert.That(d.DealClosed, Is.True);
            int cash = d.Cash; d.OfferDeal(); Assert.That(d.Cash, Is.EqualTo(cash));
        }
        [Test] public void ClosedUnsuccessfulCallDamagesOnlyItsRelationship()
        {
            var c = new CareerState(); var d = c.CreateDay(); d.StartCall(); d.EndCall(); d.EndDay(); c.CompleteDay(d);
            Assert.That(c.Capture().relationships, Is.EqualTo(new[] { -1, 0, 0 }));
            Assert.That(c.CreateDay().Clients[0].Returning, Is.False);
        }
        [Test] public void CaptureAndRestoreAreDeepCopies()
        {
            var c = new CareerState(); Finish(c); var s = c.Capture();
            Assert.That(CareerState.TryRestore(s, out var restored), Is.True);
            s.relationships[0] = -1; s.reports[0].sold[0] = false;
            var last = c.LastReport; last.sold[0] = false;
            Assert.That(restored.Capture().reports[0].sold[0], Is.True); Assert.That(c.LastReport.sold[0], Is.True);
        }
        [TestCase(0)] [TestCase(2)] public void UnknownSaveVersionsAreRejected(int version)
        { var s = new CareerState().Capture(); s.schemaVersion = version; Assert.That(CareerState.TryRestore(s, out _), Is.False); }
        [Test] public void InvalidArraysAndAccountingAreRejected()
        {
            var c = new CareerState(); Finish(c); var s = c.Capture(); s.reports[0].balance++;
            Assert.That(CareerState.TryRestore(s, out _), Is.False);
            s = c.Capture(); s.relationships = new int[2]; Assert.That(CareerState.TryRestore(s, out _), Is.False);
            s = c.Capture(); s.lastSold[0] = false; Assert.That(CareerState.TryRestore(s, out _), Is.False);
        }
        [Test] public void AllThreeDayReportsCanRestore()
        {
            var c = new CareerState();
            for (int i = 0; i < 3; i++) { Finish(c); Assert.That(CareerState.TryRestore(c.Capture(), out var loaded), Is.True); Assert.That(loaded.Balance, Is.EqualTo(c.Balance)); }
        }
        [Test] public void LaterDaysKeepThreeEventsAndTheirConsequences()
        {
            for (int number = 2; number <= 3; number++)
            {
                var d = new BrokerDay(number); d.AdvanceTime(60);
                Assert.That(d.PendingEvent.Id, Is.EqualTo(0)); d.ResolveEvent(1);
                d.AdvanceTime(120); Assert.That(d.PendingEvent.Id, Is.EqualTo(1)); d.ResolveEvent(1);
                d.AdvanceTime(120); Assert.That(d.PendingEvent.Id, Is.EqualTo(2)); d.ResolveEvent(1);
                Assert.That(d.TotalExpenses, Is.EqualTo(340 + (number - 1) * 40 + 100));
                Assert.That(d.Reputation, Is.EqualTo(35));
            }
        }
        [Test] public void NonnegativeBalanceStillNeedsFortyReputationToWin()
        {
            var c = new CareerState(); Finish(c); Finish(c); Finish(c);
            var s = c.Capture(); s.reputation = 39; s.reports[2].reputation = 39;
            Assert.That(CareerState.TryRestore(s, out var loaded), Is.True);
            Assert.That(loaded.Balance, Is.GreaterThanOrEqualTo(0)); Assert.That(loaded.Won, Is.False);
        }
        [Test] public void IncorrectOpeningCashCannotSettle()
        { var c = new CareerState(); var d = new BrokerDay(1, 500); d.EndDay(); Assert.That(c.CompleteDay(d), Is.False); }
    }
}
