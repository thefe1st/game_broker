using NUnit.Framework;

namespace Broker.Tests
{
    internal static class NegotiationTestSteps
    {
        // Qualifying no longer pays immediately: first proposal opens a personal objection.
        public static void AnswerObjection(Broker.Core.BrokerDay day)
        {
            day.OfferDeal();
            Assert.That(day.Current.ObjectionRaised, Is.True, day.NextConversationStep);
            Assert.That(day.DealClosed, Is.False);
            day.Say(day.Current.Id == "boris" ? "Не вкладывайте всю сумму, оставьте резерв."
                : day.Current.Id == "inna" ? "Комиссия и условия будут в письменном договоре."
                : "У вас будет личный менеджер. Пишите мне, буду на связи.");
            Assert.That(day.Current.ObjectionResolved, Is.True, day.NextConversationStep);
        }
    }
}
