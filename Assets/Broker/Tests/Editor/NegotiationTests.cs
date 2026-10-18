using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class NegotiationTests
    {
        private static BrokerDay Prepared(int client = 0)
        {
            var day = new BrokerDay(); day.SelectClient(client); day.StartCall();
            day.Say("Какая цель? Есть риск. Комиссия 2%. Срок год. Персональный отчёт."); return day;
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void FirstQualifiedOfferRaisesIndividualObjectionWithoutPaying(int client)
        {
            var day = Prepared(client); string reply = day.OfferDeal();
            Assert.That(day.Current.ObjectionRaised, Is.True); Assert.That(day.Current.ObjectionResolved, Is.False);
            Assert.That(day.Call, Is.EqualTo(CallStatus.Active)); Assert.That(day.Cash, Is.Zero);
            StringAssert.Contains(ClientConversation.Objection(day.Current.Id), reply);
            Assert.That(day.NextConversationStep, Is.EqualTo(ClientConversation.Hint(day.Current.Id)));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void IndividualAnswerAllowsExactlyOneCommission(int client)
        {
            var day = Prepared(client); NegotiationTestSteps.AnswerObjection(day);
            day.OfferDeal(); day.OfferDeal(); Assert.That(day.Earned, Is.EqualTo(day.Current.Commission));
            Assert.That(day.DealsCount, Is.EqualTo(1)); Assert.That(day.Call, Is.EqualTo(CallStatus.Closed));
        }
        [Test] public void GuessingAnswerBeforeObjectionDoesNotSkipConversation()
        {
            var day = Prepared(); day.Say("Оставьте резерв."); day.OfferDeal();
            Assert.That(day.Current.ObjectionResolved, Is.False); Assert.That(day.Cash, Is.Zero);
        }
        [Test] public void RepeatedOfferNeitherPaysNorClosesCall()
        {
            var day = Prepared(); int trust = day.Trust;
            for (int i = 0; i < 8; i++) day.OfferDeal();
            Assert.That(day.Cash, Is.Zero); Assert.That(day.Call, Is.EqualTo(CallStatus.Active)); Assert.That(day.Trust, Is.EqualTo(trust));
        }
        [Test] public void DifferentClientsDoNotAcceptEachOthersAnswer()
        {
            var day = Prepared(1); day.OfferDeal(); day.Say("Оставьте резерв.");
            Assert.That(day.Current.ObjectionResolved, Is.False); day.Say("Условия в письменном договоре.");
            Assert.That(day.Current.ObjectionResolved, Is.True);
        }
        [Test] public void AnswerCannotFarmTrust()
        {
            var day = Prepared(); NegotiationTestSteps.AnswerObjection(day); int trust = day.Trust;
            for (int i = 0; i < 10; i++) day.Say("Оставьте резерв.");
            Assert.That(day.Trust, Is.EqualTo(trust)); Assert.That(day.Cash, Is.Zero);
        }
        [Test] public void PressureLosesOnlyOneTrustPerCallAndNoMoney()
        {
            var day = Prepared(); int trust = day.Trust, reputation = day.Reputation;
            for (int i = 0; i < 4; i++) day.Say("Последний шанс! Решайте сейчас!");
            Assert.That(day.Trust, Is.EqualTo(trust - 1)); Assert.That(day.Reputation, Is.EqualTo(reputation)); Assert.That(day.Cash, Is.Zero);
        }
        [Test] public void RespectCanRecoverOnlyActualPressurePenaltyOnce()
        {
            var day = Prepared(); int trust = day.Trust;
            day.Say("Решение за вами"); Assert.That(day.Trust, Is.EqualTo(trust));
            day.Say("Последний шанс"); day.Say("Решение за вами");
            Assert.That(day.Trust, Is.EqualTo(trust));
            for (int i = 0; i < 4; i++) { day.Say("Последний шанс"); day.Say("Решение за вами"); }
            Assert.That(day.Trust, Is.EqualTo(trust));
        }
        [Test] public void PressureAtZeroCannotCreateFreeRecoveryTrust()
        {
            var day = new BrokerDay(); day.SelectClient(1); day.StartCall();
            day.Say("Последний шанс"); day.Say("Решение за вами");
            Assert.That(day.Trust, Is.Zero); Assert.That(day.Current.PressureTrustLost, Is.False);
            Assert.That(day.Current.PressureRecovered, Is.False);
        }
        [Test] public void UnresolvedNegotiationDoesNotCreateCommissionOrRelationshipBonus()
        {
            var career = new CareerState(); var day = career.CreateDay(); day.StartCall();
            day.Say("Какая цель? Есть риск. Комиссия 2%."); day.OfferDeal(); day.EndDay(); career.CompleteDay(day);
            Assert.That(day.Earned, Is.Zero); Assert.That(career.TotalDeals, Is.Zero);
            Assert.That(career.Capture().relationships[0], Is.EqualTo(-1));
        }
        [Test] public void NewNegotiationStateDoesNotChangeSaveSchemaOrFinalRestore()
        {
            var career = new CareerState();
            for (int d = 0; d < 3; d++)
            {
                var day = career.CreateDay();
                for (int i = 0; i < 3; i++)
                {
                    day.SelectClient(i); day.StartCall();
                    day.Say("Какая цель? Есть риск. Комиссия 2%. Срок год. Персональный отчет.");
                    NegotiationTestSteps.AnswerObjection(day); day.OfferDeal();
                }
                day.EndDay(); career.CompleteDay(day);
            }
            Assert.That(career.Capture().schemaVersion, Is.EqualTo(1));
            Assert.That(CareerState.TryRestore(career.Capture(), out var restored), Is.True);
            Assert.That(restored.IsFinished && restored.Won, Is.True);
            Assert.That(restored.TotalDeals, Is.EqualTo(9)); Assert.That(restored.Balance, Is.EqualTo(510));
        }
        [Test] public void PressureInSameMessageDoesNotResolveObjection()
        {
            var day = Prepared(); day.OfferDeal(); day.Say("Оставьте резерв, только сегодня!");
            Assert.That(day.Current.ObjectionResolved, Is.False); day.Say("Оставьте резерв. Решение за вами.");
            Assert.That(day.Current.ObjectionResolved, Is.True);
        }
        [Test] public void EmptyAndIrrelevantPhrasesDoNotChangeTrustOrFlags()
        {
            var day = new BrokerDay(); day.StartCall(); int trust = day.Trust;
            string a = day.Say("Здравствуйте"); string b = day.Say("Здравствуйте");
            Assert.That(a, Is.Not.EqualTo(b)); Assert.That(day.Trust, Is.EqualTo(trust));
            Assert.That(day.NeedsKnown || day.FeesExplained || day.RisksExplained, Is.False);
        }
        [Test] public void RepeatingRecognizedTopicDoesNotRepeatDiscovery()
        {
            var day = new BrokerDay(); day.StartCall(); string a = day.Say("Какая цель?"); string b = day.Say("Какая цель?");
            Assert.That(a, Is.Not.EqualTo(b)); StringAssert.Contains("уже обсудили", b); Assert.That(day.Trust, Is.EqualTo(2));
        }
        [Test] public void FalsePromiseStillStopsCallBeforeAnyDisclosures()
        {
            var day = new BrokerDay(); day.StartCall(); day.Say("Не гарантирую прибыль, но риска нет. Какая цель? Комиссия 2%.");
            Assert.That(day.Call, Is.EqualTo(CallStatus.Closed)); Assert.That(day.Reputation, Is.EqualTo(40));
            Assert.That(day.NeedsKnown || day.FeesExplained, Is.False);
        }
        [Test] public void GuaranteedReportIsNotGuaranteedProfit()
        {
            var day = new BrokerDay(); day.StartCall(); day.Say("Гарантирую персональный отчёт.");
            Assert.That(day.Reputation, Is.EqualTo(50)); Assert.That(day.Call, Is.EqualTo(CallStatus.Active));
        }
        [Test] public void EventAndEndOfDayBlockNewConversationState()
        {
            var day = Prepared(); day.AdvanceTime(60); day.OfferDeal(); day.Say("Последний шанс");
            Assert.That(day.Current.ObjectionRaised || day.Current.PressureWarned, Is.False);
            day.ResolveEvent(1); day.OfferDeal(); day.EndDay(); day.Say("Оставьте резерв");
            Assert.That(day.Current.ObjectionResolved, Is.False);
        }
        [Test] public void NewDayResetsOnlyNegotiationStateNotCampaign()
        {
            var career = new CareerState(); var day = career.CreateDay(); day.StartCall(); day.Say("Последний шанс");
            day.EndDay(); career.CompleteDay(day); var next = career.CreateDay();
            Assert.That(next.Current.PressureWarned || next.Current.ObjectionRaised || next.Current.ObjectionResolved, Is.False);
            Assert.That(next.DayNumber, Is.EqualTo(2)); Assert.That(next.CarryDebt, Is.EqualTo(340));
        }
        [Test] public void BorisReactsToJargonWithoutInventingFinancialEffects()
        {
            var day = Prepared(); int trust = day.Trust;
            StringAssert.Contains("язык мойки", day.Say("Диверсификация и волатильность")); Assert.That(day.Trust, Is.EqualTo(trust));
        }
    }
}
