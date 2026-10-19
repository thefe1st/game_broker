using System;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class WorkCareerTests
    {
        public static void Sell(BrokerDay d, int index)
        {
            Assert.That(d.SelectClient(index), Is.True); d.StartCall();
            d.Say("Обсудим отчёт по прошлой сделке. Какая цель? Есть риск убытка. Комиссия 2%. Срок год. Персональный отчёт.");
            d.OfferDeal();
            d.Say(ClientConversation.AnswerIntent(d.Current.Id) == DialogueIntent.Reserve ? "Оставьте резерв, не вкладывайте всю сумму."
                : ClientConversation.AnswerIntent(d.Current.Id) == DialogueIntent.Written ? "Все условия в письменном договоре." : "Личный менеджер, буду на связи.");
            d.OfferDeal(); Assert.That(d.DealClosed, Is.True, d.Current.Id + ": " + d.NextConversationStep);
        }
        public static EmploymentState Intro(bool sell = false)
        {
            var intro = new EmploymentState();
            for (int n = 0; n < 3; n++) { var d = intro.CreateDay(); if (sell) for (int i = 0; i < 3; i++) Sell(d, i); d.EndDay(); Assert.That(intro.CompleteDay(d), Is.True); }
            return intro;
        }
        private static BrokerDay Finish(WorkCareerState state, bool sell = false)
        {
            var d = state.CreateDay(); if (sell) for (int i = 0; i < 3; i++) Sell(d, i);
            d.EndDay(); Assert.That(state.CompleteDay(d), Is.True); return d;
        }
        [Test] public void CannotStartBeforeIntroIsComplete()
        { Assert.Throws<ArgumentException>(() => WorkCareerState.FromIntro(null)); Assert.Throws<ArgumentException>(() => WorkCareerState.FromIntro(new EmploymentState())); }
        [Test] public void CopyFromIntroPreservesOriginalAndStartsAtDayFour()
        {
            var intro = Intro(true); var before = intro.Capture(); var work = WorkCareerState.FromIntro(intro); var d = work.CreateDay();
            Assert.That(d.DayNumber, Is.EqualTo(4)); Assert.That(d.Mode, Is.EqualTo(CareerMode.EmployeeWork));
            Assert.That(d.Cash, Is.EqualTo(330)); Assert.That(d.TotalExpenses, Is.Zero); Assert.That(d.SalesTarget, Is.EqualTo(2));
            Assert.That(d.Clients[0].Id, Is.EqualTo("boris")); Assert.That(d.Clients[0].Returning, Is.True);
            Assert.That(d.Clients[0].Trust, Is.EqualTo(3));
            Finish(work, true); Assert.That(intro.Capture().personalSavings, Is.EqualTo(before.personalSavings));
            Assert.That(intro.CompletedDays, Is.EqualTo(3)); Assert.That(work.TotalDeals, Is.EqualTo(3));
        }
        [TestCase(4, "boris,vera,denis")] [TestCase(5, "inna,oksana,vera")] [TestCase(6, "artem,denis,oksana")]
        [TestCase(7, "boris,inna,artem")] [TestCase(8, "boris,vera,denis")]
        public void AppointmentsAreStableAndRotateThroughSixIdentities(int day, string expected)
        { Assert.That(string.Join(",", ClientCatalog.Appointments(day)), Is.EqualTo(expected)); }
        [TestCase(0, 150, 30)] [TestCase(1, 200, 40)] [TestCase(2, 170, 34)]
        public void EachWorkSalePaysTwentyPercentExactlyOnce(int index, int gross, int personal)
        {
            var c = WorkCareerState.FromIntro(Intro()); var d = c.CreateDay(); Sell(d, index); d.OfferDeal();
            Assert.That(d.Earned, Is.EqualTo(personal)); Assert.That(d.FirmRevenue, Is.EqualTo(gross));
            d.EndDay(); Assert.That(c.CompleteDay(d), Is.True); Assert.That(c.CompleteDay(d), Is.False);
            Assert.That(c.PersonalSavings, Is.EqualTo(personal));
        }
        [TestCase("vera")] [TestCase("denis")] [TestCase("oksana")]
        public void NewClientsNeedTheirOwnObjectionAnswer(string id)
        {
            var c = WorkCareerState.FromIntro(Intro()); while (Array.IndexOf(ClientCatalog.Appointments(c.NextDayNumber), id) < 0) Finish(c);
            var d = c.CreateDay(); int i = Array.IndexOf(ClientCatalog.Appointments(d.DayNumber), id); d.SelectClient(i); d.StartCall();
            d.Say("Какая цель? Есть риск убытка. Комиссия 2%. Срок год. Персональный отчёт."); d.OfferDeal();
            d.Say(ClientConversation.AnswerIntent(id) == DialogueIntent.Reserve ? "Договор с письменными условиями." : "Оставьте резерв на ремонт.");
            d.OfferDeal(); Assert.That(d.DealClosed, Is.False);
            d.Say(ClientConversation.AnswerIntent(id) == DialogueIntent.Reserve ? "Оставьте резерв на ремонт." : "Договор с письменными условиями.");
            d.OfferDeal(); Assert.That(d.DealClosed, Is.True);
        }
        [TestCase("denis", ConversationStage.Term)] [TestCase("oksana", ConversationStage.Service)]
        public void NewSpecialRequirementsCannotBeSkipped(string id, ConversationStage required)
        {
            var c = WorkCareerState.FromIntro(Intro()); while (Array.IndexOf(ClientCatalog.Appointments(c.NextDayNumber), id) < 0) Finish(c);
            var d = c.CreateDay(); d.SelectClient(Array.IndexOf(ClientCatalog.Appointments(d.DayNumber), id)); d.StartCall();
            d.Say("Какая цель? Есть риск убытка. Комиссия 2%."); Assert.That(d.ConversationStep, Is.EqualTo(required));
            d.OfferDeal(); Assert.That(d.Current.ObjectionRaised, Is.False);
        }
        [Test] public void IdentityRatherThanDailySlotCarriesRelationsAndOutstandingReport()
        {
            var c = WorkCareerState.FromIntro(Intro()); var d = c.CreateDay(); Sell(d, 1); d.EndDay(); c.CompleteDay(d);
            d = c.CreateDay(); Assert.That(d.Clients[2].Id, Is.EqualTo("vera")); Assert.That(d.Clients[2].Returning, Is.True);
            Assert.That(d.Clients[2].Trust, Is.EqualTo(2)); Assert.That(d.Clients[1].Id, Is.EqualTo("oksana")); Assert.That(d.Clients[1].Returning, Is.False);
            d.SelectClient(2); d.StartCall(); d.Say("Какая цель? Риск убытка. Комиссия 2%."); d.OfferDeal();
            Assert.That(d.ConversationStep, Is.EqualTo(ConversationStage.Report)); Assert.That(d.DealClosed, Is.False);
        }
        [Test] public void ReportSurvivesDaysWithoutClientAndWithoutCalls()
        {
            var c = WorkCareerState.FromIntro(Intro()); var d = c.CreateDay(); Sell(d, 0); d.EndDay(); c.CompleteDay(d);
            Finish(c); Finish(c); d = c.CreateDay(); Assert.That(d.DayNumber, Is.EqualTo(7));
            Assert.That(d.Clients[0].Id, Is.EqualTo("boris")); Assert.That(d.Clients[0].Returning, Is.True);
            d.EndDay(); c.CompleteDay(d); Assert.That(c.CreateDay().Clients[0].Returning, Is.True);
        }
        [Test] public void ReviewingWithoutNewSaleClearsOldReportButNotOtherClients()
        {
            var c = WorkCareerState.FromIntro(Intro()); var d = c.CreateDay(); Sell(d, 0); Sell(d, 1); d.EndDay(); c.CompleteDay(d);
            d = c.CreateDay(); d.SelectClient(2); d.StartCall(); d.Say("Обсудим отчёт по прошлой сделке."); d.EndCall(); d.EndDay(); c.CompleteDay(d);
            Assert.That(c.Clients[3].awaitingReport, Is.False); Assert.That(c.Clients[0].awaitingReport, Is.True);
            Assert.That(c.Clients[3].relationship, Is.Zero);
        }
        [Test] public void ClosedAndUncalledClientsHaveDifferentRelationshipEffects()
        {
            var c = WorkCareerState.FromIntro(Intro()); var d = c.CreateDay(); d.StartCall(); d.EndCall(); d.EndDay(); c.CompleteDay(d);
            Assert.That(c.Clients[0].relationship, Is.EqualTo(-1)); Assert.That(c.Clients[3].relationship, Is.Zero);
        }
        [Test] public void IntroAndOwnerCannotSettleWorkDay()
        {
            var c = WorkCareerState.FromIntro(Intro()); var d = c.CreateDay(); d.EndDay();
            Assert.That(new EmploymentState().CompleteDay(d), Is.False); Assert.That(new CareerState().CompleteDay(d), Is.False);
            var intro = new BrokerDay(mode: CareerMode.Employee); intro.EndDay(); Assert.That(c.CompleteDay(intro), Is.False);
        }
        [Test] public void EmployeeRestrictionsAlsoApplyToWorkMode()
        {
            var d = WorkCareerState.FromIntro(Intro()).CreateDay(); d.AssignTask(0, WorkKind.Leads, 0); d.BuyCoffee(); d.BuyGoldenPhone(); d.AdvanceTime(301);
            Assert.That(d.Cash, Is.Zero); Assert.That(d.PendingEvent, Is.Null); Assert.That(d.Employees[0].Work, Is.EqualTo(WorkKind.Idle));
            Assert.That(d.CoffeeBought || d.HasGoldenPhone, Is.False);
        }
        [Test] public void AThousandDaysKeepOnlySevenReportsAndMoneyPassesOldPrototypeLimit()
        {
            var c = WorkCareerState.FromIntro(Intro());
            for (int n = 0; n < 1000; n++) { Finish(c, true); Assert.That(WorkCareerState.TryRestore(c.Capture(), out var loaded), Is.True); c = loaded; }
            Assert.That(c.CompletedDays, Is.EqualTo(1000)); Assert.That(c.NextDayNumber, Is.EqualTo(1004));
            Assert.That(c.PersonalSavings, Is.EqualTo(111000)); Assert.That(c.TotalDeals, Is.EqualTo(3000));
            Assert.That(c.Capture().reports.Length, Is.EqualTo(7)); Assert.That(c.Capture().checkpoint.completedWorkDays, Is.EqualTo(993));
            Assert.That(c.CreateDay().OpeningCash, Is.EqualTo(c.PersonalSavings));
        }
        [Test] public void CapturesClientListsAndLastReportAreDefensiveCopies()
        {
            var c = WorkCareerState.FromIntro(Intro()); Finish(c, true);
            c.Capture().origin.clients[0].id = "bad"; c.Capture().reports[0].sold[0] = false;
            c.Clients[0].relationship = -1; c.LastReport.savings = -5;
            Assert.That(WorkCareerState.TryRestore(c.Capture(), out _), Is.True); Assert.That(c.Clients[0].relationship, Is.EqualTo(1));
        }
        [Test] public void RestartUsesOriginalTransferEvenAfterCheckpointPruning()
        {
            var c = WorkCareerState.FromIntro(Intro(true)); for (int n = 0; n < 12; n++) Finish(c, true);
            var reset = c.RestartFromOrigin(); Assert.That(reset.CompletedDays, Is.Zero); Assert.That(reset.PersonalSavings, Is.EqualTo(330));
            Assert.That(reset.Clients[0].relationship, Is.EqualTo(2)); Assert.That(reset.CreateDay().Clients[0].Returning, Is.True);
            Assert.That(c.CompletedDays, Is.EqualTo(12));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)] [TestCase(12)] [TestCase(13)] [TestCase(14)] [TestCase(15)]
        public void InvalidLedgerIsRejected(int mutation)
        {
            var c = WorkCareerState.FromIntro(Intro()); Finish(c, true); var s = c.Capture();
            switch (mutation)
            {
                case 0: s.schemaVersion = 99; break; case 1: s.mode = "employee-intro"; break; case 2: s.origin = null; break;
                case 3: s.checkpoint = null; break; case 4: s.reports = null; break; case 5: s.reports[0] = null; break;
                case 6: s.reports[0].day++; break; case 7: s.reports[0].clientIds[0] = "inna"; break;
                case 8: s.reports[0].personalEarned++; break; case 9: s.reports[0].savings++; break;
                case 10: s.reports[0].closed[0] = false; break; case 11: s.reports[0].reportReviewed[0] = false; break;
                case 12: s.checkpoint.clients[0].id = "unknown"; break; case 13: s.origin.clients[0].relationship = 9; break;
                case 14: s.checkpoint.clients[0].deals = int.MaxValue; break; case 15: s.checkpoint.savings = int.MaxValue; break;
            }
            Assert.That(WorkCareerState.TryRestore(s, out _), Is.False);
        }
        [Test] public void OutstandingReportAndRelationshipsSurviveCheckpointPruningWithoutNewCalls()
        {
            var c = WorkCareerState.FromIntro(Intro()); var d = c.CreateDay(); Sell(d, 0); d.EndDay(); c.CompleteDay(d);
            for (int i = 0; i < 40; i++) Finish(c);
            Assert.That(WorkCareerState.TryRestore(c.Capture(), out var loaded), Is.True);
            Assert.That(loaded.Clients[0].awaitingReport, Is.True); Assert.That(loaded.Clients[0].relationship, Is.EqualTo(1));
            Assert.That(loaded.Clients[0].deals, Is.EqualTo(1)); Assert.That(loaded.TotalDeals, Is.EqualTo(1));
        }
        [Test] public void WorkDayRequiresCanonicalAppointmentsAndDisallowsOwnerMoneyFeatures()
        {
            Assert.Throws<ArgumentException>(() => new BrokerDay(4, mode: CareerMode.EmployeeWork));
            Assert.Throws<ArgumentException>(() => new BrokerDay(4, mode: CareerMode.EmployeeWork, clientIds: new[] { "inna", "vera", "denis" }));
            Assert.Throws<ArgumentException>(() => new BrokerDay(4, mode: CareerMode.EmployeeWork, carryDebt: 1, clientIds: ClientCatalog.Appointments(4)));
            Assert.Throws<ArgumentException>(() => new BrokerDay(4, mode: CareerMode.EmployeeWork, goldenPhone: true, clientIds: ClientCatalog.Appointments(4)));
            Assert.Throws<ArgumentException>(() => new BrokerDay(4, mode: CareerMode.Employee));
        }
        [Test] public void CatalogAndAppointmentArraysCannotMutateFutureDays()
        {
            ClientCatalog.Ids[0] = "bad"; ClientCatalog.Appointments(4)[0] = "bad";
            Assert.That(ClientCatalog.Ids[0], Is.EqualTo("boris")); Assert.That(ClientCatalog.Appointments(4)[0], Is.EqualTo("boris"));
            Assert.Throws<ArgumentException>(() => ClientCatalog.Create("bad"));
        }
        [TestCase(false)] [TestCase(true)]
        public void BrokenPrunedLedgerIsRejected(bool changeTotals)
        {
            var c = WorkCareerState.FromIntro(Intro()); for (int n = 0; n < 12; n++) Finish(c, true); var save = c.Capture();
            if (changeTotals) save.checkpoint.clients[0].deals++; else save.reports[3].day++;
            Assert.That(WorkCareerState.TryRestore(save, out _), Is.False);
        }
        [Test] public void FullDayStillSettlesAfterSaveReloadWithPendingReports()
        {
            var c = WorkCareerState.FromIntro(Intro(true)); Finish(c, true);
            Assert.That(WorkCareerState.TryRestore(c.Capture(), out var restored), Is.True);
            Assert.That(restored.PersonalSavings, Is.EqualTo(434)); Assert.That(restored.NextDayNumber, Is.EqualTo(5));
            Assert.That(restored.CreateDay().Clients[2].Returning, Is.True);
        }
    }
}
