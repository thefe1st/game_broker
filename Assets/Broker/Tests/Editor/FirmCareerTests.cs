using System;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class FirmCareerTests
    {
        public static WorkCareerState ReadyWork()
        {
            var work = WorkCareerState.FromIntro(WorkCareerTests.Intro(true));
            while (!FirmCareerState.CanOpen(work))
            {
                var day = work.CreateDay(); for (int i = 0; i < 3; i++) WorkCareerTests.Sell(day, i);
                day.EndDay(); Assert.That(work.CompleteDay(day), Is.True);
            }
            return work;
        }
        public static FirmCareerState NewFirm() => FirmCareerState.FromWork(ReadyWork());
        // stockId: sell this stock to every client instead of the default offers.
        public static void Finish(FirmCareerState firm, bool sell = false, string stockId = null)
        {
            var day = firm.CreateDay();
            if (sell) for (int i = 0; i < 3; i++) { if (stockId != null) { day.SelectClient(i); day.SelectStock(stockId); } WorkCareerTests.Sell(day, i); }
            day.EndDay(); Assert.That(firm.CompleteDay(day), Is.True);
        }
        [Test] public void OpeningRequiresSettledSavingsAndDoesNotTakeTodayPayout()
        {
            Assert.That(FirmCareerState.CanOpen(null), Is.False);
            Assert.Throws<ArgumentException>(() => FirmCareerState.FromWork(null));
            var work = WorkCareerState.FromIntro(WorkCareerTests.Intro(true));
            var day = work.CreateDay(); for (int i = 0; i < 3; i++) WorkCareerTests.Sell(day, i);
            Assert.That(FirmCareerState.CanOpen(work), Is.False);
            Assert.Throws<ArgumentException>(() => FirmCareerState.FromWork(work));
        }
        [TestCase(598, false)] [TestCase(600, true)] [TestCase(630, true)]
        public void ExactSavingsThreshold(int savings, bool allowed)
        {
            var work = WorkCareerState.FromIntro(WorkCareerTests.Intro()); var s = work.Capture();
            s.checkpoint.completedWorkDays = 10; s.checkpoint.clients[0].deals = 10;
            s.checkpoint.clients[0].lastSeenWorkDay = 10; s.checkpoint.clients[0].lastStockId = "logistics"; s.checkpoint.clients[0].lastStockDay = 7;
            s.checkpoint.clients[0].lastStockPrice = StockMarket.PriceCents("logistics", 7);
            s.checkpoint.clients[0].portfolio = new[] { new StockPosition { stockId = "logistics", day = 7, priceCents = StockMarket.PriceCents("logistics", 7) } };
            s.origin.startingSavings = s.origin.savings = savings - 300;
            s.checkpoint.startingSavings = savings - 300; s.checkpoint.savings = savings;
            s.reports = new WorkDayReport[7];
            for (int n = 0; n < 7; n++) s.reports[n] = new WorkDayReport { day = 14 + n, openingSavings = savings, savings = savings,
                reputation = 50, clientIds = ClientCatalog.Appointments(14 + n), sold = new bool[3], closed = new bool[3],
                reportReviewed = new[] { true, true, true }, guaranteed = new bool[3], complaintResolved = new bool[3], soldStockIds = new[] { "", "", "" }, soldPrices = new int[3],
                concernResolved = new bool[3] };
            Assert.That(WorkCareerState.TryRestore(s, out work), Is.True);
            Assert.That(FirmCareerState.CanOpen(work), Is.EqualTo(allowed));
            if (allowed) Assert.That(FirmCareerState.FromWork(work).PersonalReserve, Is.EqualTo(savings - 500));
        }
        [Test] public void OpeningSeparatesMoneyKeepsObligationsAndDoesNotMutateWork()
        {
            var work = ReadyWork(); var day = work.CreateDay();
            day.StartCall(); day.Say("Какая цель? Гарантирую прибыль"); day.EndDay(); Assert.That(work.CompleteDay(day), Is.True);
            var before = work.Clients; int savings = work.PersonalSavings;
            var firm = FirmCareerState.FromWork(work);
            Assert.That(firm.Balance, Is.EqualTo(300)); Assert.That(firm.PersonalReserve + firm.Balance + 200, Is.EqualTo(savings));
            Assert.That(firm.NextDayNumber, Is.EqualTo(work.NextDayNumber)); Assert.That(firm.Reputation, Is.EqualTo(work.Reputation));
            for (int i = 0; i < 6; i++)
            {
                Assert.That(firm.Clients[i].relationship, Is.EqualTo(before[i].relationship));
                Assert.That(firm.Clients[i].awaitingReport, Is.EqualTo(before[i].awaitingReport));
                Assert.That(firm.Clients[i].complaintPending, Is.EqualTo(before[i].complaintPending));
                Assert.That(firm.Clients[i].deals + firm.Clients[i].lastSeenWorkDay, Is.Zero);
                Assert.That(work.Clients[i].deals, Is.EqualTo(before[i].deals));
            }
            Assert.That(work.PersonalSavings, Is.EqualTo(savings)); Assert.That(firm.RecentReports, Is.Empty);
            Assert.That(Array.Exists(firm.Clients, c => c.complaintPending), Is.True);
        }
        [Test] public void SalePaysFullCommissionExactlyOnceAndRentSettlesExactlyOnce()
        {
            var firm = NewFirm(); int reserve = firm.PersonalReserve; var day = firm.CreateDay();
            WorkCareerTests.Sell(day, 0); int commission = day.Current.Commission;
            day.OfferDeal(); Assert.That(day.Earned, Is.EqualTo(commission)); Assert.That(day.FirmRevenue, Is.EqualTo(commission));
            Assert.That(firm.CompleteDay(day), Is.False); day.EndDay(); day.EndDay();
            Assert.That(firm.CompleteDay(day), Is.True); Assert.That(firm.CompleteDay(day), Is.False);
            Assert.That(firm.Balance, Is.EqualTo(300 + commission - 60)); Assert.That(firm.PersonalReserve, Is.EqualTo(reserve));
            Assert.That(firm.LastReport.rent, Is.EqualTo(60)); Assert.That(firm.CompletedDays, Is.EqualTo(1));
        }
        [Test] public void IdleDaysCreateDebtAndSalesRecoverWithoutUsingReserveOrDoubleDebt()
        {
            var firm = NewFirm(); int reserve = firm.PersonalReserve;
            for (int n = 0; n < 7; n++) Finish(firm);
            Assert.That(firm.Balance, Is.EqualTo(-120)); var day = firm.CreateDay();
            Assert.That(day.Cash, Is.Zero); Assert.That(day.CarryDebt, Is.EqualTo(120)); Assert.That(day.TotalExpenses, Is.EqualTo(180));
            for (int i = 0; i < 3; i++) WorkCareerTests.Sell(day, i);
            int revenue = day.FirmRevenue; day.EndDay(); Assert.That(firm.CompleteDay(day), Is.True);
            Assert.That(firm.Balance, Is.EqualTo(-120 + revenue - 60)); Assert.That(firm.Balance, Is.GreaterThan(0));
            Assert.That(firm.PersonalReserve, Is.EqualTo(reserve)); Assert.That(firm.CreateDay().CarryDebt, Is.Zero);
        }
        [Test] public void FounderHasNoEmployerExpensesEventsPurchasesTasksOrCityRewards()
        {
            var day = NewFirm().CreateDay(); Assert.That(day.IsEmployee, Is.False); Assert.That(day.IsFounder, Is.True);
            Assert.That(day.DailyRent, Is.EqualTo(60)); Assert.That(day.DailyPayroll, Is.Zero);
            day.BuyCoffee(); day.BuyGoldenPhone(); day.AssignTask(0, WorkKind.Research, 0); day.AssignTask(1, WorkKind.Leads, 0);
            day.StartCityWork(CityWorkKind.Courier); day.AdvanceTime(400, true);
            Assert.That(day.Cash, Is.EqualTo(300)); Assert.That(day.Spent, Is.Zero); Assert.That(day.PendingEvent, Is.Null);
            Assert.That(day.HasGoldenPhone || day.CoffeeBought || day.CityWorkTaken || day.Current.Researched, Is.False);
            Assert.That(day.Current.Brief, Does.Not.Contain("Подготовка досье"));
        }
        [Test] public void CarriedComplaintSurvivesPruningThenRepairStillRequiresReport()
        {
            var work = ReadyWork(); var d = work.CreateDay(); d.StartCall(); d.Say("Какая цель?"); d.Say("Гарантирую прибыль");
            string id = d.Current.Id; d.EndDay(); Assert.That(work.CompleteDay(d), Is.True); var firm = FirmCareerState.FromWork(work);
            for (int n = 0; n < 10; n++) Finish(firm);
            Assert.That(FirmCareerState.TryRestore(firm.Capture(), out firm), Is.True);
            Assert.That(firm.Clients[ClientCatalog.Index(id)].complaintPending, Is.True);
            while (Array.IndexOf(ClientCatalog.Appointments(firm.NextDayNumber), id) < 0) Finish(firm);
            d = firm.CreateDay(); d.SelectClient(Array.IndexOf(ClientCatalog.Appointments(d.DayNumber), id)); d.StartCall();
            Assert.That(d.ConversationStep, Is.EqualTo(ConversationStage.Complaint)); d.OfferDeal(); Assert.That(d.DealClosed, Is.False);
            d.Say("Признаю ошибку. Есть риск убытка."); Assert.That(d.Current.ComplaintResolved, Is.True);
            // v5: a portfolio drop of the carried purchases is discussed after the complaint, before the report.
            Assert.That(d.ConversationStep, Is.EqualTo(d.Current.PriceConcern ? ConversationStage.PriceConcern : ConversationStage.Report));
            WorkCareerTests.SellAfterStarted(d); // Same call, without opening it again.
            d.EndDay(); Assert.That(firm.CompleteDay(d), Is.True);
            Assert.That(firm.Clients[ClientCatalog.Index(id)].complaintPending, Is.False);
            Assert.That(firm.Clients[ClientCatalog.Index(id)].awaitingReport, Is.True);
        }
        [Test] public void FounderGuaranteeAndRepairArePersistedWithoutMoneyReward()
        {
            var firm = NewFirm(); var day = firm.CreateDay(); string id = day.Current.Id;
            day.StartCall(); day.Say("Какая цель?"); day.Say("Гарантирую прибыль"); day.EndDay(); Assert.That(firm.CompleteDay(day), Is.True);
            while (Array.IndexOf(ClientCatalog.Appointments(firm.NextDayNumber), id) < 0) Finish(firm);
            day = firm.CreateDay(); day.SelectClient(Array.IndexOf(ClientCatalog.Appointments(day.DayNumber), id)); day.StartCall();
            day.Say("Признаю ошибку. Есть риск убытка."); Assert.That(day.Earned, Is.Zero);
            day.EndDay(); Assert.That(firm.CompleteDay(day), Is.True); Assert.That(firm.Clients[ClientCatalog.Index(id)].complaintPending, Is.False);
        }
        [Test] public void ThousandDaysRestoreBoundedHistoryAndResetToOpeningNotEmployeeIntro()
        {
            var firm = NewFirm(); var origin = firm.Capture().origin; int source = firm.NextDayNumber, reserve = firm.PersonalReserve;
            for (int n = 0; n < 1000; n++) { Finish(firm, n % 3 == 0); Assert.That(FirmCareerState.TryRestore(firm.Capture(), out firm), Is.True); }
            Assert.That(firm.CompletedDays, Is.EqualTo(1000)); Assert.That(firm.NextDayNumber, Is.EqualTo(source + 1000));
            Assert.That(firm.RecentReports.Length, Is.EqualTo(7)); Assert.That(firm.Capture().checkpoint.completedDays, Is.EqualTo(993));
            var reset = firm.RestartFromOrigin(); Assert.That(reset.Balance, Is.EqualTo(300)); Assert.That(reset.NextDayNumber, Is.EqualTo(source));
            Assert.That(reset.PersonalReserve, Is.EqualTo(reserve)); Assert.That(reset.RecentReports, Is.Empty);
            for (int i = 0; i < 6; i++) Assert.That(reset.Clients[i].awaitingReport, Is.EqualTo(origin.clients[i].awaitingReport));
        }
        [Test] public void ReadbackAndRestoreUseDefensiveCopies()
        {
            var firm = NewFirm(); Finish(firm, true); var save = firm.Capture();
            Assert.That(FirmCareerState.TryRestore(save, out var restored), Is.True); int balance = firm.Balance;
            save.origin.clients[0].id = "changed"; save.reports[0].revenue = -1;
            firm.Clients[0].relationship = -100; firm.LastReport.clientIds[0] = "changed"; firm.RecentReports[0].sold[0] = false;
            Assert.That(restored.Balance, Is.EqualTo(balance)); Assert.That(FirmCareerState.TryRestore(firm.Capture(), out _), Is.True);
        }
        [TestCase(CareerMode.Employee)] [TestCase(CareerMode.Owner)] [TestCase(CareerMode.EmployeeWork)]
        public void ForeignDayCannotSettleFirm(CareerMode mode)
        {
            var firm = NewFirm(); var day = mode == CareerMode.EmployeeWork ? ReadyWork().CreateDay() : new BrokerDay(mode: mode);
            day.EndDay(); Assert.That(firm.CompleteDay(day), Is.False); Assert.That(firm.CompletedDays, Is.Zero);
        }
        [Test] public void WrongOpeningDayOrCashCannotSettle()
        {
            var firm = NewFirm(); var d = new BrokerDay(firm.NextDayNumber, 301, mode: CareerMode.Founder, clientIds: ClientCatalog.Appointments(firm.NextDayNumber));
            d.EndDay(); Assert.That(firm.CompleteDay(d), Is.False); Assert.That(firm.CompleteDay(null), Is.False);
        }
        [Test] public void ReportCannotBeReviewedWithoutClosedCall()
        {
            var firm = NewFirm(); Finish(firm); var s = firm.Capture();
            Assert.That(s.origin.clients[ClientCatalog.Index(s.reports[0].clientIds[0])].awaitingReport, Is.True);
            s.reports[0].reportReviewed[0] = true;
            Assert.That(FirmCareerState.TryRestore(s, out _), Is.False);
        }
        [Test] public void HireCandidateIsIndependentPersistentAndChargesOnceBeforeSalary()
        {
            var firm = NewFirm(); int reserve = firm.PersonalReserve, source = firm.NextDayNumber;
            Assert.That(firm.CanHireAssistant, Is.True); Assert.That(firm.TryHireAssistant(out var hired), Is.True);
            Assert.That(firm.HasAssistant, Is.False); Assert.That(firm.Balance, Is.EqualTo(300));
            Assert.That(hired.HasAssistant, Is.True); Assert.That(hired.AssistantHireDay, Is.EqualTo(source));
            Assert.That(hired.Balance, Is.EqualTo(200)); Assert.That(hired.CompletedDays, Is.Zero);
            Assert.That(hired.TryHireAssistant(out _), Is.False);
            Assert.That(FirmCareerState.TryRestore(hired.Capture(), out hired), Is.True);
            var d = hired.CreateDay(); Assert.That(d.DailyPayroll, Is.EqualTo(40)); Assert.That(d.TotalExpenses, Is.EqualTo(100));
            Assert.That(hired.CompleteDay(d), Is.False); d.EndDay(); Assert.That(hired.CompleteDay(d), Is.True);
            Assert.That(hired.CompleteDay(d), Is.False); Assert.That(hired.Balance, Is.EqualTo(100));
            Assert.That(hired.LastReport.openingBalance, Is.EqualTo(200)); Assert.That(hired.LastReport.salary, Is.EqualTo(40));
            Finish(hired); Assert.That(hired.Balance, Is.Zero); Assert.That(hired.PersonalReserve, Is.EqualTo(reserve));
        }
        [TestCase(0, true)] [TestCase(1, true)] [TestCase(2, false)] [TestCase(6, false)]
        public void HiringRequiresFirmCashNotPersonalReserve(int idleDays, bool allowed)
        {
            var firm = NewFirm(); for (int n = 0; n < idleDays; n++) Finish(firm);
            Assert.That(firm.CanHireAssistant, Is.EqualTo(allowed)); Assert.That(firm.TryHireAssistant(out _), Is.EqualTo(allowed));
        }
        [Test] public void SalaryContinuesInDebtAndSalesRecoverWithoutPersonalMoney()
        {
            var firm = NewFirm(); firm.TryHireAssistant(out firm); int reserve = firm.PersonalReserve;
            for (int n = 0; n < 4; n++) Finish(firm);
            Assert.That(firm.Balance, Is.EqualTo(-200)); var d = firm.CreateDay();
            Assert.That(d.CarryDebt, Is.EqualTo(200)); Assert.That(d.TotalExpenses, Is.EqualTo(300));
            for (int i = 0; i < 3; i++) WorkCareerTests.Sell(d, i); int revenue = d.FirmRevenue;
            d.EndDay(); Assert.That(firm.CompleteDay(d), Is.True);
            Assert.That(firm.Balance, Is.EqualTo(-200 + revenue - 100)); Assert.That(firm.PersonalReserve, Is.EqualTo(reserve));
        }
        [Test] public void ThousandEmployeeDaysPreserveHireDatePayrollPruningAndReset()
        {
            var firm = NewFirm(); for (int n = 0; n < 10; n++) Finish(firm, true);
            int hire = firm.NextDayNumber, cash = firm.Balance; firm.TryHireAssistant(out firm);
            long revenue = 0;
            for (int n = 0; n < 1000; n++)
            {
                var d = firm.CreateDay(); if (n % 2 == 0) for (int i = 0; i < 3; i++) WorkCareerTests.Sell(d, i);
                revenue += d.FirmRevenue; d.EndDay(); Assert.That(firm.CompleteDay(d), Is.True);
                Assert.That(FirmCareerState.TryRestore(firm.Capture(), out firm), Is.True);
                Assert.That(firm.AssistantHireDay, Is.EqualTo(hire));
                Assert.That(firm.Balance, Is.EqualTo(cash - 100 + revenue - (n + 1L) * 100));
            }
            Assert.That(firm.RecentReports.Length, Is.EqualTo(7)); Assert.That(firm.Capture().checkpoint.completedDays, Is.EqualTo(1003));
            var reset = firm.RestartFromOrigin(); Assert.That(reset.HasAssistant, Is.False); Assert.That(reset.Balance, Is.EqualTo(300));
            Assert.That(reset.CreateDay().DailyPayroll, Is.Zero); Assert.That(reset.CanHireAssistant, Is.True);
        }
        [Test] public void DossierTakesExactly25SecondsAndDoesNotSellOrRevealConditions()
        {
            var firm = NewFirm(); firm.TryHireAssistant(out firm); var d = firm.CreateDay(); int trust = d.Trust;
            d.AssignTask(0, WorkKind.Research, 0); Assert.That(d.Employees[0].Duration, Is.EqualTo(25));
            d.AdvanceTime(24); Assert.That(d.Current.Researched, Is.False); Assert.That(d.Trust, Is.EqualTo(trust));
            d.AdvanceTime(1); Assert.That(d.Current.Researched, Is.True); Assert.That(d.Trust, Is.EqualTo(Math.Min(4, trust + 1)));
            d.AssignTask(0, WorkKind.Research, 0); d.AdvanceTime(50);
            Assert.That(d.Trust, Is.EqualTo(Math.Min(4, trust + 1))); Assert.That(d.Employees[0].Work, Is.EqualTo(WorkKind.Idle));
            Assert.That(d.Cash, Is.EqualTo(200)); Assert.That(d.Earned, Is.Zero); Assert.That(d.DealsCount, Is.Zero);
            Assert.That(d.NeedsKnown || d.RisksExplained || d.FeesExplained || d.Current.ObjectionResolved, Is.False);
            d.StartCall(); d.OfferDeal(); Assert.That(d.DealClosed, Is.False);
        }
        [TestCase(0, WorkKind.Leads)] [TestCase(0, WorkKind.Break)] [TestCase(0, WorkKind.Idle)]
        [TestCase(1, WorkKind.Research)] [TestCase(-1, WorkKind.Research)] [TestCase(2, WorkKind.Research)] [TestCase(0, (WorkKind)99)]
        public void AssistantCannotUseOtherWorkerOrOwnerTasks(int index, WorkKind kind)
        {
            var firm = NewFirm(); firm.TryHireAssistant(out firm); var d = firm.CreateDay();
            d.AssignTask(index, kind, 0); d.BuyCoffee(); d.BuyGoldenPhone(); d.AdvanceTime(200);
            Assert.That(d.Employees[0].Work, Is.EqualTo(WorkKind.Idle)); Assert.That(d.Employees[1].Work, Is.EqualTo(WorkKind.Idle));
            Assert.That(d.Current.Researched || d.HasGoldenPhone || d.CoffeeBought, Is.False);
            Assert.That(d.Cash, Is.EqualTo(200)); Assert.That(d.Earned + d.Spent, Is.Zero); Assert.That(d.PendingEvent, Is.Null);
        }
        [Test] public void BusyInvalidClosedOrEndedAssignmentsCannotReplaceTask()
        {
            var firm = NewFirm(); firm.TryHireAssistant(out firm); var d = firm.CreateDay();
            d.AssignTask(0, WorkKind.Research, -1); d.AssignTask(0, WorkKind.Research, 3);
            Assert.That(d.Employees[0].Work, Is.EqualTo(WorkKind.Idle));
            d.AssignTask(0, WorkKind.Research, 0); d.AdvanceTime(5); d.AssignTask(0, WorkKind.Research, 1);
            Assert.That(d.Employees[0].TargetClient, Is.Zero); Assert.That(d.Employees[0].Remaining, Is.EqualTo(20));
            d.AdvanceTime(20); d.SelectClient(1); d.StartCall(); d.EndCall(); d.AssignTask(0, WorkKind.Research, 1);
            Assert.That(d.Employees[0].Work, Is.EqualTo(WorkKind.Idle)); d.EndDay(); d.AssignTask(0, WorkKind.Research, 2);
            Assert.That(d.Employees[0].Work, Is.EqualTo(WorkKind.Idle));
        }
        [Test] public void UnfinishedDossierEndsWithoutRewardAndDoesNotSurviveNextDay()
        {
            var firm = NewFirm(); firm.TryHireAssistant(out firm); var d = firm.CreateDay();
            d.AssignTask(0, WorkKind.Research, 0); d.AdvanceTime(24); d.EndDay(); d.AdvanceTime(100);
            Assert.That(d.Current.Researched, Is.False); Assert.That(firm.CompleteDay(d), Is.True);
            Assert.That(firm.CreateDay().Employees[0].Work, Is.EqualTo(WorkKind.Idle));
            Assert.That(firm.CreateDay().Current.Researched, Is.False); Assert.That(firm.Balance, Is.EqualTo(100));
        }
        [Test] public void AssistantFlagMustMatchSettledCareerAndOtherModesRejectIt()
        {
            var firm = NewFirm(); var without = firm.CreateDay(); firm.TryHireAssistant(out firm); without.EndDay();
            Assert.That(firm.CompleteDay(without), Is.False);
            var noHire = NewFirm(); var with = firm.CreateDay(); with.EndDay(); Assert.That(noHire.CompleteDay(with), Is.False);
            foreach (var mode in new[] { CareerMode.Owner, CareerMode.Employee, CareerMode.EmployeeWork })
                Assert.Throws<ArgumentException>(() => new BrokerDay(mode: mode, founderAssistant: true));
        }
        [TestCase("end")] [TestCase("sale")] [TestCase("guarantee")]
        public void ClosingTargetCallCancelsUnfinishedDossierWithoutBonus(string kind)
        {
            var firm = NewFirm(); firm.TryHireAssistant(out firm); var d = firm.CreateDay();
            d.AssignTask(0, WorkKind.Research, 0); d.AdvanceTime(24);
            if (kind == "sale") WorkCareerTests.Sell(d, 0);
            else
            {
                d.StartCall();
                if (kind == "end") d.EndCall();
                else for (int i = 0; i < 5 && d.Call == CallStatus.Active; i++) d.Say("Гарантирую прибыль");
            }
            int trust = d.Trust, cash = d.Cash;
            Assert.That(d.Call, Is.EqualTo(CallStatus.Closed)); Assert.That(d.Current.Researched, Is.False);
            Assert.That(d.Employees[0].Work, Is.EqualTo(WorkKind.Idle)); Assert.That(d.Employees[0].Remaining, Is.Zero);
            d.AdvanceTime(1); Assert.That(d.Trust, Is.EqualTo(trust)); Assert.That(d.Cash, Is.EqualTo(cash));
            d.AssignTask(0, WorkKind.Research, 1); d.AdvanceTime(25); Assert.That(d.Clients[1].Researched, Is.True);
            Assert.That(d.Current.Researched, Is.False);
        }
        [Test] public void ClosingOtherCallDoesNotCancelAssignedDossier()
        {
            var firm = NewFirm(); firm.TryHireAssistant(out firm); var d = firm.CreateDay();
            d.AssignTask(0, WorkKind.Research, 1); d.StartCall(); d.EndCall();
            Assert.That(d.Employees[0].Work, Is.EqualTo(WorkKind.Research));
            d.AdvanceTime(25); Assert.That(d.Clients[1].Researched, Is.True); Assert.That(d.Current.Researched, Is.False);
        }
        [TestCase(24, false)] [TestCase(25, true)] [TestCase(26, true)]
        public void DossierAtClosingBellUsesOnlyRemainingGameTime(float remaining, bool completed)
        {
            var firm = NewFirm(); firm.TryHireAssistant(out firm); var d = firm.CreateDay();
            d.AdvanceTime(BrokerDay.DurationSeconds - remaining); int trust = d.Trust;
            d.AssignTask(0, WorkKind.Research, 0); d.AdvanceTime(100);
            Assert.That(d.DayEnded, Is.True); Assert.That(d.Current.Researched, Is.EqualTo(completed));
            Assert.That(d.Trust, Is.EqualTo(completed ? Math.Min(4, trust + 1) : trust));
            Assert.That(d.Employees[0].Work, Is.EqualTo(WorkKind.Idle));
            d.AdvanceTime(100); Assert.That(firm.CompleteDay(d), Is.True); Assert.That(firm.Balance, Is.EqualTo(100));
        }
        [Test] public void ValidLegacyFounderMigratesWithoutChangingInputOrAddingPastSalary()
        {
            var firm = NewFirm(); for (int n = 0; n < 12; n++) Finish(firm, true, StockHistoryTests.Moderate);
            var s = StockHistoryTests.Legacy(firm.Capture(), 1);
            Assert.That(FirmCareerState.TryRestore(s, out var restored), Is.True); Assert.That(s.schemaVersion, Is.EqualTo(1));
            Assert.That(restored.Capture().schemaVersion, Is.EqualTo(5)); Assert.That(restored.Balance, Is.EqualTo(firm.Balance));
            Assert.That(restored.StockHistoryFromDay, Is.EqualTo(firm.NextDayNumber));
            Assert.That(restored.HasAssistant, Is.False); Assert.That(restored.TryHireAssistant(out _), Is.True);
        }
        [TestCase("past")] [TestCase("future")] [TestCase("negative")] [TestCase("legacyHire")]
        [TestCase("salary")] [TestCase("opening")] [TestCase("balance")] [TestCase("noHire")]
        [TestCase("prunedBalance")] [TestCase("legacySalary")]
        public void MalformedEmployeeLedgerIsRejected(string kind)
        {
            var firm = NewFirm(); firm.TryHireAssistant(out firm); for (int n = 0; n < 10; n++) Finish(firm, true);
            var s = firm.Capture();
            switch (kind)
            {
                case "past": s.assistantHireDay = s.sourceDay - 1; break;
                case "future": s.assistantHireDay = firm.NextDayNumber + 1; break;
                case "negative": s.assistantHireDay = -1; break;
                case "legacyHire": StockHistoryTests.Legacy(s, 1); break;
                case "salary": s.reports[0].salary = 0; break;
                case "opening": s.reports[0].openingBalance += 100; break;
                case "balance": s.reports[0].balance += 40; break;
                case "noHire": s.assistantHireDay = 0; break;
                case "prunedBalance": s.checkpoint.balance += 100; break;
                case "legacySalary": var d = NewFirm(); Finish(d); s = StockHistoryTests.Legacy(d.Capture(), 1); s.reports[0].salary = 40; break;
            }
            Assert.That(FirmCareerState.TryRestore(s, out _), Is.False);
        }
        [Test] public void PendingHireWithInsufficientCashAndSalaryWithoutHireAreInvalid()
        {
            var firm = NewFirm(); Finish(firm); Finish(firm); var s = firm.Capture(); s.assistantHireDay = firm.NextDayNumber;
            Assert.That(FirmCareerState.TryRestore(s, out _), Is.False);
            s = firm.Capture(); s.reports[0].salary = 40; s.reports[0].balance -= 40;
            Assert.That(FirmCareerState.TryRestore(s, out _), Is.False);
        }
        [TestCase("vera", 200, true)] [TestCase("oksana", 190, false)]
        public void HiringAtCashThresholdLeavesOneDayOfExpenses(string id, int cash, bool allowed)
        {
            var firm = NewFirm(); bool sold = false;
            for (int n = 0; n < 5; n++)
            {
                var d = firm.CreateDay(); int i = Array.IndexOf(ClientCatalog.Appointments(d.DayNumber), id);
                // A moderate 2% stock keeps the threshold arithmetic: Вера $200, Оксана $190.
                if (!sold && i >= 0) { d.SelectClient(i); d.SelectStock("utility"); WorkCareerTests.Sell(d, i); sold = true; }
                d.EndDay(); Assert.That(firm.CompleteDay(d), Is.True);
            }
            Assert.That(sold, Is.True); Assert.That(firm.Balance, Is.EqualTo(cash)); Assert.That(firm.TryHireAssistant(out var hired), Is.EqualTo(allowed));
            if (allowed) { Assert.That(hired.Balance, Is.EqualTo(100)); Finish(hired); Assert.That(hired.Balance, Is.Zero); }
        }
        [Test] public void ThreeDossiersStay25SecondsWithoutMoraleSystemAndResetNextDay()
        {
            var firm = NewFirm(); firm.TryHireAssistant(out firm); var d = firm.CreateDay();
            for (int i = 0; i < 3; i++)
            { d.AssignTask(0, WorkKind.Research, i); Assert.That(d.Employees[0].Duration, Is.EqualTo(25)); d.AdvanceTime(25); Assert.That(d.Clients[i].Researched, Is.True); }
            Assert.That(d.Employees[0].Morale, Is.EqualTo(65)); d.EndDay(); Assert.That(firm.CompleteDay(d), Is.True);
            d = firm.CreateDay(); foreach (var c in d.Clients) Assert.That(c.Researched, Is.False);
        }
        [Test] public void DossierDoesNotResolveCarriedComplaintOrReviewReport()
        {
            var work = ReadyWork(); var d = work.CreateDay(); d.StartCall(); d.Say("Какая цель?"); d.Say("Гарантирую прибыль"); string id = d.Current.Id;
            d.EndDay(); Assert.That(work.CompleteDay(d), Is.True); var firm = FirmCareerState.FromWork(work);
            while (Array.IndexOf(ClientCatalog.Appointments(firm.NextDayNumber), id) < 0) Finish(firm, true);
            Assert.That(firm.TryHireAssistant(out firm), Is.True); d = firm.CreateDay(); int i = Array.IndexOf(ClientCatalog.Appointments(d.DayNumber), id);
            d.SelectClient(i); d.AssignTask(0, WorkKind.Research, i); d.AdvanceTime(25); d.StartCall();
            Assert.That(d.Current.Researched, Is.True); Assert.That(d.ConversationStep, Is.EqualTo(ConversationStage.Complaint));
            Assert.That(d.Current.ComplaintResolved || d.Current.ReportReviewed, Is.False); d.OfferDeal(); Assert.That(d.DealClosed, Is.False);
        }
        [TestCase("null")] [TestCase("version")] [TestCase("mode")] [TestCase("sourceDay")] [TestCase("sourceOverflow")]
        [TestCase("savings")] [TestCase("reserve")] [TestCase("origin")] [TestCase("checkpoint")] [TestCase("clients")]
        [TestCase("clientNull")] [TestCase("id")] [TestCase("relationship")] [TestCase("deals")] [TestCase("lastSeen")]
        [TestCase("balance")] [TestCase("reputation")] [TestCase("reports")] [TestCase("tooMany")] [TestCase("reportNull")]
        [TestCase("reportDay")] [TestCase("opening")] [TestCase("rent")] [TestCase("revenue")] [TestCase("reportBalance")]
        [TestCase("reportReputation")] [TestCase("ids")] [TestCase("sold")] [TestCase("closed")] [TestCase("reviewed")]
        [TestCase("guaranteed")] [TestCase("resolved")] [TestCase("fakeRepair")] [TestCase("fakeSale")] [TestCase("fakeGuarantee")]
        [TestCase("checkpointMismatch")] [TestCase("shortPruned")] [TestCase("originDays")]
        [TestCase("pricesNull")] [TestCase("pricesShort")] [TestCase("priceWithoutSale")] [TestCase("marketBeforeHistory")] [TestCase("marketAfterNext")]
        [TestCase("rateCount")] [TestCase("rateForbidden")] [TestCase("lastPriceWithoutStock")]
        public void MalformedSavesAreRejectedWithoutThrowing(string kind)
        {
            var firm = NewFirm(); Finish(firm); var s = firm.Capture(); var r = s.reports[0];
            switch (kind)
            {
                case "null": s = null; break; case "version": s.schemaVersion = 6; break; case "mode": s.mode = "owner"; break;
                case "sourceDay": s.sourceDay = 3; break; case "sourceOverflow": s.sourceDay = int.MaxValue; break;
                case "savings": s.sourceSavings = 599; break; case "reserve": s.personalReserve++; break;
                case "origin": s.origin = null; break; case "checkpoint": s.checkpoint = null; break;
                case "clients": s.origin.clients = new WorkClientState[5]; break; case "clientNull": s.origin.clients[0] = null; break;
                case "id": s.checkpoint.clients[0].id = "missing"; break; case "relationship": s.origin.clients[0].relationship = 3; break;
                case "deals": s.origin.clients[0].deals = -1; break; case "lastSeen": s.origin.clients[0].lastSeenWorkDay = 1; break;
                case "balance": s.checkpoint.balance++; break; case "reputation": s.origin.reputation = 101; break;
                case "reports": s.reports = null; break; case "tooMany": s.reports = new FirmDayReport[8]; break; case "reportNull": s.reports[0] = null; break;
                case "reportDay": r.day++; break; case "opening": r.openingBalance++; break; case "rent": r.rent = 0; break;
                case "revenue": r.revenue++; break; case "reportBalance": r.balance++; break; case "reportReputation": r.reputation = -1; break;
                case "ids": r.clientIds[0] = "inna"; break; case "sold": r.sold = null; break; case "closed": r.closed = new bool[2]; break;
                case "reviewed": r.reportReviewed = null; break; case "guaranteed": r.guaranteed = null; break; case "resolved": r.complaintResolved = new bool[4]; break;
                case "fakeRepair": r.complaintResolved[0] = true; r.closed[0] = true; break;
                case "fakeSale": r.sold[0] = true; break; case "fakeGuarantee": r.guaranteed[0] = true; break;
                case "checkpointMismatch": s.checkpoint.clients[0].complaintPending = !s.origin.clients[0].complaintPending; break;
                case "shortPruned": s.checkpoint.completedDays = 1; s.checkpoint.balance = 240; break; case "originDays": s.origin.completedDays = 1; break;
                case "pricesNull": r.soldPrices = null; break; case "pricesShort": r.soldPrices = new int[2]; break;
                case "priceWithoutSale": r.soldPrices[0] = StockMarket.PriceCents("utility", r.day); break;
                case "marketBeforeHistory": s.marketFromDay = s.stockHistoryFromDay - 1; break; case "marketAfterNext": s.marketFromDay = s.sourceDay + 2; break;
                case "rateCount": s.checkpoint.clients[0].dealsAt3 = 1; break;
                case "rateForbidden": s.origin.clients[ClientCatalog.Index("boris")].dealsAt3 = 1; s.origin.clients[ClientCatalog.Index("boris")].deals = 1; break;
                case "lastPriceWithoutStock": s.origin.clients[ClientCatalog.Index("vera")].lastStockId = ""; s.origin.clients[ClientCatalog.Index("vera")].lastStockDay = 0;
                    s.origin.clients[ClientCatalog.Index("vera")].lastStockPrice = 1; break;
            }
            Assert.That(FirmCareerState.TryRestore(s, out var restored), Is.False); Assert.That(restored, Is.Null);
        }
    }
}
