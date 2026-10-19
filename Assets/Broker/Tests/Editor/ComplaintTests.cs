using System;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class ComplaintTests
    {
        public static void Settle(WorkCareerState c, BrokerDay d)
        { d.EndDay(); Assert.That(c.CompleteDay(d), Is.True); }
        public static WorkCareerState WithComplaint(string id = "boris")
        {
            var c = WorkCareerState.FromIntro(WorkCareerTests.Intro());
            while (Array.IndexOf(ClientCatalog.Appointments(c.NextDayNumber), id) < 0) Settle(c, c.CreateDay());
            var d = c.CreateDay(); d.SelectClient(Array.IndexOf(ClientCatalog.Appointments(d.DayNumber), id)); d.StartCall();
            d.Say("Какая у вас цель?"); d.Say("Гарантирую прибыль."); d.EndCall(); Settle(c, d); return c;
        }
        private static BrokerDay NextContact(WorkCareerState c, string id)
        {
            while (Array.IndexOf(ClientCatalog.Appointments(c.NextDayNumber), id) < 0) Settle(c, c.CreateDay());
            var d = c.CreateDay(); d.SelectClient(Array.IndexOf(ClientCatalog.Appointments(d.DayNumber), id)); d.StartCall(); return d;
        }
        [TestCase("boris")] [TestCase("inna")] [TestCase("artem")] [TestCase("vera")] [TestCase("denis")] [TestCase("oksana")]
        public void GuaranteePersistsByIdentityAndBlocksNextSaleForEveryClient(string id)
        {
            var c = WithComplaint(id); int cash = c.PersonalSavings;
            Assert.That(c.Clients[ClientCatalog.Index(id)].complaintPending, Is.True);
            Assert.That(WorkCareerState.TryRestore(c.Capture(), out c), Is.True);
            var d = NextContact(c, id); Assert.That(d.ConversationStep, Is.EqualTo(ConversationStage.Complaint));
            d.Say("Какая цель? Есть риск убытка. Комиссия 2%. Срок год. Персональный отчёт.");
            Assert.That(d.Current.ComplaintRiskExplained, Is.True); Assert.That(d.Current.ComplaintAcknowledged, Is.False);
            Assert.That(d.OfferDeal(), Does.Contain("претензию")); Assert.That(d.Current.ObjectionRaised, Is.False);
            Assert.That(d.Cash, Is.EqualTo(cash)); Assert.That(d.DealClosed, Is.False);
        }
        [TestCase(true)] [TestCase(false)]
        public void AdmissionAndRiskCanArriveInEitherOrderAndRestoreRelationshipWithoutSale(bool admissionFirst)
        {
            var c = WithComplaint(); var d = NextContact(c, "boris"); int trust = d.Trust;
            d.Say(admissionFirst ? "Признаю ошибку" : "Есть риск убытка"); Assert.That(d.Current.ComplaintResolved, Is.False);
            d.Say(admissionFirst ? "Есть риск убытка" : "Признаю ошибку"); Assert.That(d.Current.ComplaintResolved, Is.True);
            Assert.That(d.Trust, Is.EqualTo(trust + 1)); d.Say("Признаю ошибку. Есть риск убытка.");
            Assert.That(d.Trust, Is.EqualTo(trust + 1)); Settle(c, d);
            Assert.That(c.Clients[0].relationship, Is.Zero); Assert.That(c.Clients[0].complaintPending, Is.False);
            Assert.That(c.PersonalSavings, Is.Zero); Assert.That(c.RecentReports[c.RecentReports.Length - 1].complaintResolved[0], Is.True);
        }
        [TestCase("Извините")] [TestCase("Не признаю ошибку. Есть риск убытка.")]
        [TestCase("Признаю ошибку. Риска нет.")] [TestCase("Признаю ошибку. Без риска.")]
        public void ApologyDenialOrNewGuaranteeCannotResolveComplaint(string text)
        {
            var c = WithComplaint(); var d = NextContact(c, "boris"); d.Say(text);
            Assert.That(d.Current.ComplaintResolved, Is.False); Assert.That(d.DealClosed, Is.False);
        }
        [Test] public void NewGuaranteeAfterRepairReopensAndCannotBeRepairedAgainOnSameCall()
        {
            var c = WithComplaint("artem"); var d = NextContact(c, "artem");
            d.Say("Признаю ошибку. Есть риск убытка."); Assert.That(d.Current.ComplaintResolved, Is.True);
            d.Say("Гарантирую прибыль"); Assert.That(d.Current.ComplaintResolved, Is.False);
            d.Say("Признаю ошибку. Есть риск убытка. Какая цель? Комиссия 2%. Персональный отчёт."); d.OfferDeal();
            Assert.That(d.Current.ComplaintResolved, Is.False); Assert.That(d.DealClosed, Is.False); Settle(c, d);
            Assert.That(c.Clients[2].complaintPending, Is.True);
        }
        [Test] public void ComplaintRepairDoesNotEraseSeparateReportOrReplaceNewDealConditions()
        {
            var c = WorkCareerState.FromIntro(WorkCareerTests.Intro()); var d = c.CreateDay();
            d.StartCall(); d.Say("Какая цель?"); d.Say("Гарантирую прибыль.");
            d.Say("Есть риск убытка. Комиссия 2%."); d.OfferDeal(); d.Say("Оставьте резерв"); d.OfferDeal();
            Assert.That(d.DealClosed, Is.True); Settle(c, d);
            Assert.That(c.Clients[0].complaintPending && c.Clients[0].awaitingReport, Is.True);
            d = NextContact(c, "boris"); d.Say("Признаю ошибку. Есть риск убытка.");
            Assert.That(d.ConversationStep, Is.EqualTo(ConversationStage.Report)); Assert.That(d.OfferDeal(), Does.Contain("отчёт"));
            d.Say("Обсудим отчёт по прошлой сделке."); Assert.That(d.ConversationStep, Is.EqualTo(ConversationStage.Goal));
            d.Say("Какая цель? Комиссия 2%."); d.OfferDeal(); Assert.That(d.DealClosed, Is.False);
            d.Say("Оставьте резерв"); d.OfferDeal(); Assert.That(d.DealClosed, Is.True); Settle(c, d);
            Assert.That(c.Clients[0].complaintPending, Is.False); Assert.That(c.Clients[0].awaitingReport, Is.True);
            Assert.That(c.PersonalSavings, Is.EqualTo(60)); Assert.That(c.Clients[0].relationship, Is.EqualTo(2));
        }
        [Test] public void PendingComplaintSurvivesFortyUncalledDaysAndCheckpointPruning()
        {
            var c = WithComplaint(); for (int n = 0; n < 40; n++) Settle(c, c.CreateDay());
            Assert.That(WorkCareerState.TryRestore(c.Capture(), out c), Is.True);
            Assert.That(c.Clients[0].complaintPending, Is.True); Assert.That(c.Clients[0].relationship, Is.EqualTo(-1));
            var reset = c.RestartFromOrigin(); Assert.That(reset.Clients[0].complaintPending, Is.False);
        }
        [TestCase("Не гарантирую прибыль")] [TestCase("Прибыль не гарантирована")]
        [TestCase("Не могу гарантировать доход. Есть риск убытка.")]
        public void SafeDenialOfGuaranteesDoesNotCreateComplaint(string text)
        {
            var c = WorkCareerState.FromIntro(WorkCareerTests.Intro()); var d = c.CreateDay(); d.StartCall(); d.Say(text);
            Assert.That(d.Current.FalseGuaranteeMade, Is.False); Settle(c, d); Assert.That(c.Clients[0].complaintPending, Is.False);
        }
        [TestCase(CareerMode.Owner)] [TestCase(CareerMode.Employee)]
        public void OldModesKeepExistingImmediatePenaltyWithoutComplaintMemory(CareerMode mode)
        {
            var d = new BrokerDay(mode: mode); d.StartCall(); d.Say("Гарантирую прибыль");
            Assert.That(d.Reputation, Is.EqualTo(40)); Assert.That(d.Current.FalseGuaranteeMade || d.Current.ComplaintPending, Is.False);
            Assert.Throws<ArgumentException>(() => new BrokerDay(mode: mode, complaints: new bool[3]));
        }
        [Test] public void EndedClosedOrOversizedInputCannotCreateOrResolveComplaint()
        {
            var c = WithComplaint(); var d = NextContact(c, "boris"); d.Say(new string('x', 501) + "Признаю ошибку");
            Assert.That(d.Current.ComplaintAcknowledged, Is.False); d.EndCall(); d.Say("Признаю ошибку. Есть риск убытка.");
            Assert.That(d.Current.ComplaintResolved, Is.False); d.EndDay(); d.Say("Гарантирую прибыль");
            Assert.That(d.Current.FalseGuaranteeMade, Is.False);
        }
        [Test] public void DefensiveCopiesCannotErasePendingComplaintOrLedgerFlags()
        {
            var c = WithComplaint(); c.Clients[0].complaintPending = false; c.RecentReports[0].guaranteed[0] = false;
            c.LastReport.complaintResolved[0] = true; c.Capture().reports[0].guaranteed[0] = false;
            Assert.That(c.Clients[0].complaintPending, Is.True); Assert.That(c.RecentReports[0].guaranteed[0], Is.True);
            Assert.That(WorkCareerState.TryRestore(c.Capture(), out _), Is.True);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void InvalidComplaintLedgerIsRejected(int change)
        {
            var c = WithComplaint(); var s = c.Capture();
            switch (change) {
                case 0: s.reports[0].guaranteed = null; break;
                case 1: s.reports[0].complaintResolved = new bool[2]; break;
                case 2: s.reports[0].complaintResolved[0] = true; break;
                case 3: s.reports[0].closed[0] = false; break;
                case 4: s.origin.clients[0].complaintPending = true; break;
                case 5: s.schemaVersion = 1; break;
            }
            Assert.That(WorkCareerState.TryRestore(s, out _), Is.False);
        }
        [Test] public void LegacyVersionIsUpgradedWithoutInventingComplaintsOrMutatingInput()
        {
            var c = WorkCareerState.FromIntro(WorkCareerTests.Intro(true));
            for (int n = 0; n < 10; n++) { var d = c.CreateDay(); WorkCareerTests.Sell(d, 0); Settle(c, d); }
            var s = c.Capture(); s.schemaVersion = 1; foreach (var r in s.reports) { r.guaranteed = null; r.complaintResolved = null; }
            Assert.That(WorkCareerState.TryRestore(s, out var loaded), Is.True); Assert.That(s.schemaVersion, Is.EqualTo(1));
            Assert.That(s.reports[0].guaranteed, Is.Null); Assert.That(loaded.Capture().schemaVersion, Is.EqualTo(2));
            Assert.That(loaded.PersonalSavings, Is.EqualTo(c.PersonalSavings)); Assert.That(loaded.TotalDeals, Is.EqualTo(c.TotalDeals));
            foreach (var client in loaded.Clients) Assert.That(client.complaintPending, Is.False);
        }
        [TestCase("Признаю свою ошибку", true)] [TestCase("Я ошибся", true)] [TestCase("Я ошиблась", true)]
        [TestCase("Я был неправ", true)] [TestCase("Я была неправа", true)]
        [TestCase("Я не признаю ошибку", false)] [TestCase("Это не моя ошибка", false)]
        [TestCase("Не я ошибся", false)] [TestCase("Извините", false)]
        public void AdmissionHasExplicitPositiveAndNegativePhraseRules(string phrase, bool expected)
        { Assert.That(DialogueRules.Has(DialogueRules.Analyze(phrase), DialogueIntent.AdmitError), Is.EqualTo(expected)); }
        [Test] public void MalformedLegacyArraysFailWithoutThrowing()
        {
            var s = WithComplaint().Capture(); s.schemaVersion = 1; s.reports[0].guaranteed = null;
            s.reports[0].complaintResolved = null; s.reports[0].sold = null;
            Assert.That(WorkCareerState.TryRestore(s, out _), Is.False);
        }
    }
}
