using System;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class EmploymentTests
    {
        public static void Sell(BrokerDay d, int i)
        {
            Assert.That(d.SelectClient(i), Is.True); d.StartCall();
            d.Say("Обсудим отчёт по прошлому разговору. Какая цель? Есть риск убытка. Комиссия 2%. Срок год. Персональный отчёт.");
            NegotiationTestSteps.AnswerObjection(d); d.OfferDeal(); Assert.That(d.DealClosed, Is.True);
        }
        private static BrokerDay Finish(EmploymentState c, bool sell = true)
        {
            var d = c.CreateDay(); if (sell) for (int i = 0; i < 3; i++) Sell(d, i);
            d.EndDay(); Assert.That(c.CompleteDay(d), Is.True); return d;
        }
        [Test] public void StartIsEmployeeNotOwnerWithNoPersonalExpenses()
        {
            var d = new EmploymentState().CreateDay();
            Assert.That(d.IsEmployee, Is.True); Assert.That(d.Cash, Is.Zero);
            Assert.That(d.TotalExpenses, Is.Zero); Assert.That(d.DailyRent + d.DailyPayroll + d.CarryDebt, Is.Zero);
            Assert.That(d.SupervisorBrief, Does.Contain("20%"));
            Assert.That(d.Clients[1].Brief, Does.Not.Contain("Подготовка досье"));
        }
        [TestCase(0, 150, 30)] [TestCase(1, 220, 44)] [TestCase(2, 180, 36)]
        public void EmployeeGetsTwentyPercentOnlyOnce(int i, int gross, int personal)
        {
            var d = new EmploymentState().CreateDay(); Sell(d, i);
            Assert.That(d.FirmRevenue, Is.EqualTo(gross)); Assert.That(d.Earned, Is.EqualTo(personal));
            Assert.That(d.Cash, Is.EqualTo(personal)); Assert.That(d.FirmRetained, Is.EqualTo(gross-personal));
            d.OfferDeal(); d.EndCall(); d.OfferDeal(); Assert.That(d.Cash, Is.EqualTo(personal));
            Assert.That(d.FirmRevenue, Is.EqualTo(gross));
        }
        [Test] public void NoCommissionBeforeAnswerAndClosing()
        {
            var d = new EmploymentState().CreateDay(); d.StartCall();
            d.Say("Какая цель? Есть риск убытка. Комиссия 2%."); d.OfferDeal();
            Assert.That(d.FirmRevenue + d.Cash, Is.Zero);
            d.Say("Оставьте резерв на оборудование"); Assert.That(d.Earned, Is.Zero);
            d.EndCall(); Assert.That(d.FirmRevenue, Is.Zero);
        }
        [Test] public void OwnerKeepsFullCommissionAndOriginalCosts()
        {
            var d = new CareerState().CreateDay(); Sell(d, 0);
            Assert.That(d.Cash, Is.EqualTo(150)); Assert.That(d.TotalExpenses, Is.EqualTo(340));
            d.EndDay(); var c = new CareerState(); Assert.That(c.CompleteDay(d), Is.True);
        }
        [TestCase(WorkKind.Research)] [TestCase(WorkKind.Leads)] [TestCase(WorkKind.Break)]
        public void CannotCommandColleagues(WorkKind kind)
        {
            var d = new EmploymentState().CreateDay(); d.AssignTask(0, kind, 0); d.AssignTask(1, kind, 0);
            d.AdvanceTime(100); Assert.That(d.Employees[0].Work, Is.EqualTo(WorkKind.Idle));
            Assert.That(d.Clients[0].Researched, Is.False); Assert.That(d.Earned, Is.Zero);
            Assert.That(d.Employees[1].SideDealDone, Is.False);
        }
        [Test] public void PersonalSavingsCannotBeSpentOnOfficePurchases()
        {
            var c = new EmploymentState(); Finish(c); var d = c.CreateDay();
            d.BuyGoldenPhone(); d.BuyCoffee(); Assert.That(d.Cash, Is.EqualTo(110));
            Assert.That(d.Spent, Is.Zero); Assert.That(d.CoffeeBought || d.HasGoldenPhone, Is.False);
        }
        [TestCase(CityWorkKind.Courier)] [TestCase(CityWorkKind.Interview)] [TestCase(CityWorkKind.TeamLunch)]
        public void EmployeeCannotAccessRetainedCityMechanics(CityWorkKind kind)
        {
            var d = new EmploymentState().CreateDay(); d.StartCityWork(kind); d.AdvanceTime(61, true);
            d.PickupCityWork(); d.CompleteCityWork(); Assert.That(d.CityWorkTaken, Is.False);
            Assert.That(d.Earned, Is.Zero); Assert.That(d.Reputation, Is.EqualTo(50));
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void EmployeeDayHasSoftTargetAndNoOwnerEvents(int n)
        {
            var d = new BrokerDay(n, mode: CareerMode.Employee); d.AdvanceTime(301);
            Assert.That(d.SalesTarget, Is.EqualTo(n)); Assert.That(d.PendingEvent, Is.Null);
            d.EndDay(); Assert.That(d.FinalBalance, Is.Zero);
        }
        [Test] public void EmptyDaysStillReachChapterSummaryWithoutDebtOrFalseVictory()
        {
            var c = new EmploymentState(); Finish(c, false); Finish(c, false); Finish(c, false);
            Assert.That(c.IsIntroComplete, Is.True); Assert.That(c.PersonalSavings, Is.Zero);
            Assert.That(c.TotalDeals, Is.Zero); Assert.Throws<InvalidOperationException>(() => c.CreateDay());
        }
        [Test] public void ThreeSellingDaysSaveSavingsAndSeparateEmployerTotals()
        {
            var c = new EmploymentState(); Finish(c); Finish(c); Finish(c);
            Assert.That(c.PersonalSavings, Is.EqualTo(330)); Assert.That(c.TotalFirmRevenue, Is.EqualTo(1650));
            Assert.That(c.TotalDeals, Is.EqualTo(9)); Assert.That(c.IsIntroComplete, Is.True);
        }
        [Test] public void CurrentDayRestartDoesNotDuplicateMoney()
        {
            var c = new EmploymentState(); Finish(c); var d = c.CreateDay(); Sell(d, 0);
            var again = c.CreateDay(); Assert.That(again.OpeningCash, Is.EqualTo(110));
            Assert.That(again.Earned + again.FirmRevenue, Is.Zero);
        }
        [Test] public void ReturningClientNeedsOldReportAndNewQuestion()
        {
            var c = new EmploymentState(); Finish(c); var d = c.CreateDay(); d.StartCall();
            d.Say("Какая цель? Есть риск убытка. Комиссия 2%."); d.OfferDeal();
            Assert.That(d.Current.ReportReviewed, Is.False); Assert.That(d.FirmRevenue, Is.Zero);
            d.Say("Обсудим отчёт по прошлому разговору."); d.OfferDeal(); Assert.That(d.Current.ObjectionRaised, Is.True);
            d.Say("Оставьте резерв на оборудование."); d.OfferDeal(); Assert.That(d.Earned, Is.EqualTo(30));
        }
        [Test] public void FailedCallLowersRelationshipAndUntouchedClientStaysNeutral()
        {
            var c = new EmploymentState(); var d = c.CreateDay(); d.StartCall(); d.EndDay(); c.CompleteDay(d);
            Assert.That(c.Capture().relationships, Is.EqualTo(new[] { -1, 0, 0 }));
        }
        [Test] public void FalsePromisesStillCostTrustAndReputationButDoNotInventPayout()
        {
            var d = new EmploymentState().CreateDay(); d.StartCall(); d.Say("Гарантирую прибыль, риска нет.");
            Assert.That(d.Reputation, Is.EqualTo(40)); Assert.That(d.Cash + d.FirmRevenue, Is.Zero);
        }
        [Test] public void ModesCannotSettleEachOthersDays()
        {
            var e = new EmploymentState(); var o = new CareerState(); var ed = e.CreateDay(); var od = o.CreateDay(); ed.EndDay(); od.EndDay();
            Assert.That(e.CompleteDay(od), Is.False); Assert.That(o.CompleteDay(ed), Is.False);
        }
        [Test] public void CannotSkipRepeatOrSettleAnActiveDay()
        {
            var c = new EmploymentState(); Assert.That(c.CompleteDay(null), Is.False);
            Assert.That(c.CompleteDay(c.CreateDay()), Is.False); var wrong = new BrokerDay(2, mode: CareerMode.Employee); wrong.EndDay();
            Assert.That(c.CompleteDay(wrong), Is.False); var done = Finish(c); Assert.That(c.CompleteDay(done), Is.False);
        }
        [Test] public void SaveAndLastReportAreDeepCopies()
        {
            var c = new EmploymentState(); Finish(c); var s = c.Capture();
            Assert.That(EmploymentState.TryRestore(s, out var restored), Is.True);
            s.relationships[0] = -1; s.reports[0].sold[0] = false; c.LastReport.closed[0] = false;
            Assert.That(c.LastReport.sold[0], Is.True); Assert.That(restored.LastReport.closed[0], Is.True);
        }
        [TestCase(0)] [TestCase(2)] public void UnknownSchemaIsRejected(int version)
        { var s = new EmploymentState().Capture(); s.schemaVersion = version; Assert.That(EmploymentState.TryRestore(s, out _), Is.False); }
        [TestCase(null)] [TestCase("owner")] public void MissingOrOtherModeIsRejected(string mode)
        { var s = new EmploymentState().Capture(); s.mode = mode; Assert.That(EmploymentState.TryRestore(s, out _), Is.False); }
        [TestCase("money")] [TestCase("revenue")] [TestCase("relationships")] [TestCase("sold")] [TestCase("closed")]
        [TestCase("personal")] [TestCase("reputation")] [TestCase("order")]
        public void InvalidLedgerIsRejected(string field)
        {
            var c = new EmploymentState(); Finish(c); var s = c.Capture();
            switch (field) {
                case "money": s.personalSavings++; break;
                case "revenue": s.reports[0].firmRevenue++; break;
                case "relationships": s.relationships[0] = -1; break;
                case "sold": s.lastSold[0] = false; break;
                case "closed": s.reports[0].closed[0] = false; break;
                case "personal": s.reports[0].personalEarned++; break;
                case "reputation": s.reputation = 101; break;
                case "order": s.reports[0].day = 2; break;
            }
            Assert.That(EmploymentState.TryRestore(s, out _), Is.False);
        }
        [Test] public void MissingArraysAndNullRowsAreRejectedWithoutThrowing()
        {
            var s = new EmploymentState().Capture(); s.relationships = null;
            Assert.That(EmploymentState.TryRestore(s, out _), Is.False);
            var c = new EmploymentState(); Finish(c); s = c.Capture(); s.reports[0] = null;
            Assert.That(EmploymentState.TryRestore(s, out _), Is.False);
        }
        [Test] public void EmployeeCannotStartWithOwnersDebtOrUpgrade()
        {
            Assert.Throws<ArgumentException>(() => new BrokerDay(carryDebt: 10, mode: CareerMode.Employee));
            Assert.Throws<ArgumentException>(() => new BrokerDay(goldenPhone: true, mode: CareerMode.Employee));
        }
        [Test] public void SupervisorPanelPausesTimeAndRoutesWithoutChangingMoney()
        {
            var n = new OfficeNavigation(); Assert.That(n.Open(OfficeStation.Supervisor), Is.True);
            Assert.That(n.View, Is.EqualTo(OfficeView.Supervisor)); Assert.That(n.RunsClock, Is.False);
        }
    }
}
