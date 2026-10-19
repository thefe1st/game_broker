using System;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class CityWorkTests
    {
        private static void ResolveEvents(BrokerDay day)
        { while (day.PendingEvent != null) day.ResolveEvent(1); }
        private static void Tick(BrokerDay day, float seconds, bool inCity = true)
        { for (int i = 0; i < (int)seconds; i++) { day.AdvanceTime(1, inCity); ResolveEvents(day); } }
        private static void Ready(BrokerDay day, CityWorkKind kind)
        { day.StartCityWork(kind); Tick(day, BrokerDay.CityWorkDuration(kind)); }
        private static void Finish(BrokerDay day, CityWorkKind kind)
        { Ready(day, kind); day.PickupCityWork(); day.CompleteCityWork(); }

        [Test] public void FreshDayHasNoJobAndExplainsWhereToGo()
        {
            var d = new BrokerDay(); Assert.That(d.CityWork, Is.EqualTo(CityWorkKind.None));
            Assert.That(d.CityStage, Is.EqualTo(CityWorkStage.Available)); Assert.That(d.CityWorkTaken, Is.False);
            Assert.That(d.CityWorkRemaining, Is.Zero); Assert.That(d.CityWorkStatus, Does.Contain("бюро"));
        }
        [TestCase(CityWorkKind.Courier, 60, 0)]
        [TestCase(CityWorkKind.Interview, 45, 0)]
        [TestCase(CityWorkKind.TeamLunch, 30, 30)]
        public void StartHasCorrectCostDurationAndNoReward(CityWorkKind kind, int seconds, int cost)
        {
            var d = new BrokerDay(openingCash: 100); d.StartCityWork(kind);
            Assert.That(d.CityStage, Is.EqualTo(CityWorkStage.Preparing)); Assert.That(d.CityWork, Is.EqualTo(kind));
            Assert.That(d.CityWorkRemaining, Is.EqualTo(seconds)); Assert.That(d.Spent, Is.EqualTo(cost));
            Assert.That(d.Cash, Is.EqualTo(100 - cost)); Assert.That(d.Earned, Is.Zero); Assert.That(d.Reputation, Is.EqualTo(50));
        }
        [TestCase(CityWorkKind.None)] [TestCase((CityWorkKind)999)] [TestCase((CityWorkKind)(-1))]
        public void InvalidKindDoesNotMutateDay(CityWorkKind kind)
        {
            var d = new BrokerDay(openingCash: 100); d.StartCityWork(kind);
            Assert.That(d.CityStage, Is.EqualTo(CityWorkStage.Available)); Assert.That(d.Cash, Is.EqualTo(100)); Assert.That(d.Spent, Is.Zero);
        }
        [Test] public void LunchRequiresCashAndDoesNotCreateDebt()
        {
            var d = new BrokerDay(openingCash: 29); d.StartCityWork(CityWorkKind.TeamLunch);
            Assert.That(d.CityWorkTaken, Is.False); Assert.That(d.Cash, Is.EqualTo(29)); Assert.That(d.Spent, Is.Zero);
            d.StartCityWork(CityWorkKind.Courier); Assert.That(d.CityWorkTaken, Is.True);
        }
        [Test] public void ExactlyThirtyCashBuysLunchOnce()
        {
            var d = new BrokerDay(openingCash: 30); d.StartCityWork(CityWorkKind.TeamLunch); d.StartCityWork(CityWorkKind.TeamLunch);
            Assert.That(d.Cash, Is.Zero); Assert.That(d.Spent, Is.EqualTo(30));
        }
        [Test] public void OfficeTimeDoesNotAdvancePreparation()
        {
            var d = new BrokerDay(); d.StartCityWork(CityWorkKind.Courier); Tick(d, 70, false);
            Assert.That(d.CityWorkRemaining, Is.EqualTo(60)); Assert.That(d.CityStage, Is.EqualTo(CityWorkStage.Preparing));
            Assert.That(d.RemainingSeconds, Is.EqualTo(530));
        }
        [Test] public void PreparationResumesAfterReturningToCity()
        {
            var d = new BrokerDay(); d.StartCityWork(CityWorkKind.Courier); Tick(d, 20); Tick(d, 15, false); Tick(d, 40);
            Assert.That(d.CityWorkRemaining, Is.Zero); Assert.That(d.CityStage, Is.EqualTo(CityWorkStage.ReadyForPickup));
        }
        [Test] public void ExactDurationMakesOrderReadyButDoesNotPay()
        {
            var d = new BrokerDay(); Ready(d, CityWorkKind.Courier);
            Assert.That(d.CityStage, Is.EqualTo(CityWorkStage.ReadyForPickup)); Assert.That(d.Cash, Is.Zero); Assert.That(d.Earned, Is.Zero);
        }
        [Test] public void MoreTimeCannotSkipPickupOrClaim()
        {
            var d = new BrokerDay(); Ready(d, CityWorkKind.Courier); Tick(d, 60);
            Assert.That(d.CityStage, Is.EqualTo(CityWorkStage.ReadyForPickup)); Assert.That(d.Earned, Is.Zero);
        }
        [Test] public void PickupThenHandInIsRequired()
        {
            var d = new BrokerDay(); d.CompleteCityWork(); d.PickupCityWork(); Assert.That(d.CityWorkTaken, Is.False);
            d.StartCityWork(CityWorkKind.Courier); d.PickupCityWork(); d.CompleteCityWork();
            Assert.That(d.CityStage, Is.EqualTo(CityWorkStage.Preparing));
            Tick(d, 60); d.CompleteCityWork(); Assert.That(d.Earned, Is.Zero);
            d.PickupCityWork(); Assert.That(d.CityStage, Is.EqualTo(CityWorkStage.Carrying)); Assert.That(d.Earned, Is.Zero);
            d.CompleteCityWork(); Assert.That(d.CityStage, Is.EqualTo(CityWorkStage.Completed)); Assert.That(d.Earned, Is.EqualTo(65));
        }
        [TestCase(CityWorkKind.Courier)] [TestCase(CityWorkKind.Interview)] [TestCase(CityWorkKind.TeamLunch)]
        public void RepeatedClaimsAndRestartsCannotFarmRewards(CityWorkKind kind)
        {
            var d = new BrokerDay(openingCash: 100); Finish(d, kind);
            int cash = d.Cash, rep = d.Reputation, morale = d.TeamMorale, spent = d.Spent;
            for (int i = 0; i < 5; i++) { d.PickupCityWork(); d.CompleteCityWork(); d.StartCityWork(kind); d.StartCityWork(CityWorkKind.Courier); }
            Assert.That(d.CityStage, Is.EqualTo(CityWorkStage.Completed)); Assert.That(d.Cash, Is.EqualTo(cash));
            Assert.That(d.Reputation, Is.EqualTo(rep)); Assert.That(d.TeamMorale, Is.EqualTo(morale)); Assert.That(d.Spent, Is.EqualTo(spent));
        }
        [Test] public void InterviewOnlyChangesReputation()
        {
            var d = new BrokerDay(); int morale = d.TeamMorale; Finish(d, CityWorkKind.Interview);
            Assert.That(d.Reputation, Is.EqualTo(58)); Assert.That(d.Cash, Is.Zero); Assert.That(d.TeamMorale, Is.EqualTo(morale));
            Assert.That(d.DealsCount, Is.Zero); Assert.That(d.Trust, Is.EqualTo(1));
        }
        [Test] public void InterviewReputationIsCapped()
        { var d = new BrokerDay(reputation: 98); Finish(d, CityWorkKind.Interview); Assert.That(d.Reputation, Is.EqualTo(100)); }
        [Test] public void LunchDoesNotConsumeDailyCoffeeOrIncreaseTrust()
        {
            var d = new BrokerDay(openingCash: 100); int morale = d.TeamMorale; Finish(d, CityWorkKind.TeamLunch);
            Assert.That(d.TeamMorale, Is.EqualTo(Math.Min(100, morale + 20))); Assert.That(d.CoffeeBought, Is.False);
            Assert.That(d.Cash, Is.EqualTo(70)); Assert.That(d.Spent, Is.EqualTo(30)); Assert.That(d.Trust, Is.EqualTo(1));
        }
        [Test] public void LunchAndCoffeeNeverOverflowMorale()
        {
            var d = new BrokerDay(openingCash: 100); d.BuyCoffee(); Finish(d, CityWorkKind.TeamLunch);
            foreach (var worker in d.Employees) Assert.That(worker.Morale, Is.LessThanOrEqualTo(100));
            Assert.That(d.CoffeeBought, Is.True); Assert.That(d.Spent, Is.EqualTo(70));
        }
        [Test] public void ActiveCallBlocksStartAndPreparation()
        {
            var d = new BrokerDay(); d.StartCall(); d.StartCityWork(CityWorkKind.Courier); Assert.That(d.CityWorkTaken, Is.False);
            d.EndCall(); d.StartCityWork(CityWorkKind.Courier); d.SelectClient(1); d.StartCall(); Tick(d, 10);
            Assert.That(d.CityWorkRemaining, Is.EqualTo(60));
        }
        [Test] public void ActiveCallBlocksPickupAndClaimWithoutLosingReward()
        {
            var d = new BrokerDay(); Ready(d, CityWorkKind.Courier); d.StartCall(); d.PickupCityWork();
            Assert.That(d.CityStage, Is.EqualTo(CityWorkStage.ReadyForPickup));
            d.EndCall(); d.PickupCityWork(); d.SelectClient(1); d.StartCall(); d.CompleteCityWork();
            Assert.That(d.CityStage, Is.EqualTo(CityWorkStage.Carrying)); Assert.That(d.Earned, Is.Zero);
            d.EndCall(); d.CompleteCityWork(); Assert.That(d.Earned, Is.EqualTo(65));
        }
        [Test] public void PendingEventFreezesPreparation()
        {
            var d = new BrokerDay(); d.AdvanceTime(60); Assert.That(d.PendingEvent, Is.Not.Null);
            d.StartCityWork(CityWorkKind.Courier); Assert.That(d.CityWorkTaken, Is.False);
            ResolveEvents(d); d.StartCityWork(CityWorkKind.Courier); d.AdvanceTime(120, true);
            Assert.That(d.PendingEvent, Is.Not.Null); float remaining = d.CityWorkRemaining;
            d.AdvanceTime(100, true); Assert.That(d.CityWorkRemaining, Is.EqualTo(remaining));
        }
        [Test] public void PendingEventBlocksPickupAndClaim()
        {
            var d = new BrokerDay(); Ready(d, CityWorkKind.Courier); d.AdvanceTime(120, true);
            Assert.That(d.PendingEvent, Is.Not.Null); d.PickupCityWork(); Assert.That(d.CityStage, Is.EqualTo(CityWorkStage.ReadyForPickup));
            ResolveEvents(d); d.PickupCityWork(); d.AdvanceTime(120, true); Assert.That(d.PendingEvent, Is.Not.Null);
            d.CompleteCityWork(); Assert.That(d.Earned, Is.Zero); ResolveEvents(d); d.CompleteCityWork(); Assert.That(d.Earned, Is.EqualTo(65));
        }
        [TestCase(0)] [TestCase(-1)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void InvalidTimeCannotPrepareAnOrder(float seconds)
        {
            var d = new BrokerDay(); d.StartCityWork(CityWorkKind.Courier); d.AdvanceTime(seconds, true);
            Assert.That(d.CityWorkRemaining, Is.EqualTo(60)); Assert.That(d.RemainingSeconds, Is.EqualTo(600));
        }
        [Test] public void EndDayExpiresPreparingOrderWithoutRefund()
        {
            var d = new BrokerDay(openingCash: 30); d.StartCityWork(CityWorkKind.TeamLunch); d.EndDay();
            Assert.That(d.CityStage, Is.EqualTo(CityWorkStage.Expired)); Assert.That(d.CityWorkRemaining, Is.Zero);
            Assert.That(d.Spent, Is.EqualTo(30)); Assert.That(d.Cash, Is.Zero); d.AdvanceTime(30, true); d.PickupCityWork(); d.CompleteCityWork();
            Assert.That(d.CityStage, Is.EqualTo(CityWorkStage.Expired));
        }
        [TestCase(false)] [TestCase(true)]
        public void UncollectedOrCarriedOrderExpiresAtEndOfDay(bool pickedUp)
        {
            var d = new BrokerDay(); Ready(d, CityWorkKind.Courier); if (pickedUp) d.PickupCityWork(); d.EndDay();
            d.CompleteCityWork(); Assert.That(d.CityStage, Is.EqualTo(CityWorkStage.Expired)); Assert.That(d.Earned, Is.Zero);
        }
        [Test] public void TimerEndingOnPreparationBoundaryDoesNotGrantReward()
        {
            var d = new BrokerDay(); Tick(d, 540, false); d.StartCityWork(CityWorkKind.Courier); d.AdvanceTime(60, true);
            Assert.That(d.DayEnded, Is.True); Assert.That(d.CityStage, Is.EqualTo(CityWorkStage.Expired)); Assert.That(d.Earned, Is.Zero);
        }
        [Test] public void UntakenAndCompletedOrdersAreNotExpired()
        {
            var a = new BrokerDay(); a.EndDay(); a.StartCityWork(CityWorkKind.Courier); Assert.That(a.CityWorkTaken, Is.False);
            var b = new BrokerDay(); Finish(b, CityWorkKind.Courier); b.EndDay(); b.EndDay(); Assert.That(b.CityStage, Is.EqualTo(CityWorkStage.Completed)); Assert.That(b.Earned, Is.EqualTo(65));
        }
        [Test] public void OnlyOneKindMayBeChosenPerDay()
        {
            var d = new BrokerDay(openingCash: 100); d.StartCityWork(CityWorkKind.Interview); d.StartCityWork(CityWorkKind.TeamLunch);
            Assert.That(d.CityWork, Is.EqualTo(CityWorkKind.Interview)); Assert.That(d.Cash, Is.EqualTo(100));
        }
        [Test] public void CourierIncomeIsIncludedInCampaignAndRestoredWithoutReplayingReward()
        {
            var c = new CareerState(); var d = c.CreateDay(); Finish(d, CityWorkKind.Courier); d.EndDay();
            Assert.That(c.CompleteDay(d), Is.True); Assert.That(c.LastReport.earned, Is.EqualTo(65)); Assert.That(c.Balance, Is.EqualTo(d.FinalBalance));
            Assert.That(CareerState.TryRestore(c.Capture(), out var restored), Is.True);
            var tomorrow = restored.CreateDay(); Assert.That(tomorrow.CityStage, Is.EqualTo(CityWorkStage.Available));
            Assert.That(tomorrow.CityWorkTaken, Is.False); Assert.That(tomorrow.Earned, Is.Zero);
            Assert.That(tomorrow.CarryDebt, Is.EqualTo(-c.Balance));
        }
        [Test] public void LunchCostRemainsInDailyLedgerWhenOrderExpires()
        {
            var d = new BrokerDay(2, 30); d.StartCityWork(CityWorkKind.TeamLunch); d.EndDay();
            Assert.That(d.FinalBalance, Is.EqualTo(-380)); Assert.That(d.Spent, Is.EqualTo(30));
        }
        [TestCase(CityWorkKind.Courier)] [TestCase(CityWorkKind.Interview)] [TestCase(CityWorkKind.TeamLunch)]
        public void ThreeDayCareerWithCityWorkRestoresInV1Format(CityWorkKind kind)
        {
            var c = new CareerState();
            for (int i = 0; i < 3; i++)
            {
                var d = c.CreateDay();
                // The courier finances lunch in the following days. Negative balances carry as debt.
                if (kind == CityWorkKind.TeamLunch && d.Cash < 30)
                {
                    d.AssignTask(0, WorkKind.Leads, 0); Tick(d, 40, false);
                }
                Finish(d, kind); Assert.That(d.CityStage, Is.EqualTo(CityWorkStage.Completed));
                d.EndDay(); Assert.That(c.CompleteDay(d), Is.True);
                Assert.That(c.CompleteDay(d), Is.False);
                var save = c.Capture(); Assert.That(save.schemaVersion, Is.EqualTo(1));
                Assert.That(CareerState.TryRestore(save, out var restored), Is.True); c = restored;
            }
            Assert.That(c.IsFinished, Is.True); Assert.That(c.CompletedDays, Is.EqualTo(3));
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void EveryDayOffersAllKindsWithDifferentNeighbourFlavour(int day)
        {
            var d = new BrokerDay(day); Assert.That(d.CityNeighbourLine, Does.StartWith("Вера:"));
            foreach (CityWorkKind kind in new[] { CityWorkKind.Courier, CityWorkKind.Interview, CityWorkKind.TeamLunch })
            { Assert.That(BrokerDay.CityWorkName(kind), Is.Not.Empty); Assert.That(BrokerDay.CityWorkDuration(kind), Is.GreaterThan(0)); }
            Assert.That(new BrokerDay(1).CityNeighbourLine, Is.Not.EqualTo(new BrokerDay(2).CityNeighbourLine));
        }
        [TestCase(OfficeStation.CityContact, OfficeView.CityContact)] [TestCase(OfficeStation.CityPickup, OfficeView.CityPickup)]
        public void CityPanelsPauseWithoutGrantingRewards(OfficeStation station, OfficeView view)
        {
            var n = new OfficeNavigation(); Assert.That(n.Open(station), Is.True); Assert.That(n.View, Is.EqualTo(view));
            Assert.That(n.RunsClock, Is.False); n.Close(); Assert.That(n.RunsClock, Is.True);
        }
    }
}
