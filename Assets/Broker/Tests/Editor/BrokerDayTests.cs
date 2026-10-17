using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class BrokerDayTests
    {
        private static BrokerDay ReadyToClose()
        {
            var day = new BrokerDay();
            day.StartCall();
            day.Say("Какая у вас цель?");
            day.Say("Объясню риск убытка.");
            day.Say("Комиссия 2%.");
            return day;
        }

        [Test] public void CannotSellBeforeStartingCall()
        {
            var day = new BrokerDay(); day.OfferDeal();
            Assert.That(day.Cash, Is.Zero);
            Assert.That(day.DealClosed, Is.False);
        }
        [Test] public void CannotSellWithoutDiscoveryAndDisclosures()
        {
            var day = new BrokerDay(); day.StartCall(); day.OfferDeal();
            Assert.That(day.Cash, Is.Zero);
            day.Say("Какая цель?"); day.OfferDeal();
            Assert.That(day.Cash, Is.Zero);
            day.Say("Есть риск."); day.OfferDeal();
            Assert.That(day.Cash, Is.Zero);
        }
        [Test] public void QualifiedDealPaysExactlyOnce()
        {
            var day = ReadyToClose(); day.OfferDeal(); day.OfferDeal();
            Assert.That(day.Cash, Is.EqualTo(BrokerDay.DealCommission));
            Assert.That(day.DealClosed, Is.True);
            Assert.That(day.RentCovered, Is.True);
            Assert.That(day.Call, Is.EqualTo(CallStatus.Closed));
        }
        [Test] public void GuaranteeLosesClientAndBlocksSale()
        {
            var day = new BrokerDay(); day.StartCall();
            day.Say("Гарантированная прибыль!");
            day.Say("Какая цель? Есть риск. Комиссия 2%."); day.OfferDeal();
            Assert.That(day.Trust, Is.Zero);
            Assert.That(day.Call, Is.EqualTo(CallStatus.Closed));
            Assert.That(day.Cash, Is.Zero);
        }
        [Test] public void RepeatedDiscoveryDoesNotFarmTrust()
        {
            var day = new BrokerDay(); day.StartCall();
            for (int i = 0; i < 10; i++) day.Say("Какая цель?");
            Assert.That(day.Trust, Is.EqualTo(2));
        }
        [Test] public void TimeExpiryClosesDayAndBlocksSale()
        {
            var day = ReadyToClose(); day.AdvanceTime(10000); day.OfferDeal();
            Assert.That(day.RemainingSeconds, Is.Zero);
            Assert.That(day.DayEnded, Is.True);
            Assert.That(day.Cash, Is.Zero);
        }
        [Test] public void EndDayIsIdempotentAndReportsDebt()
        {
            var day = new BrokerDay(); day.EndDay(); day.EndDay();
            Assert.That(day.BalanceAfterRent, Is.EqualTo(-BrokerDay.Rent));
            Assert.That(day.RentCovered, Is.False);
        }
        [Test] public void InvalidTimeDoesNotCorruptClock()
        {
            var day = new BrokerDay();
            day.AdvanceTime(-1); day.AdvanceTime(float.NaN); day.AdvanceTime(float.PositiveInfinity);
            Assert.That(day.RemainingSeconds, Is.EqualTo(BrokerDay.DurationSeconds));
        }
        [Test] public void NewDayResetsState()
        {
            var oldDay = ReadyToClose(); oldDay.OfferDeal(); oldDay.EndDay();
            var fresh = new BrokerDay();
            Assert.That(fresh.Cash, Is.Zero);
            Assert.That(fresh.DayEnded, Is.False);
            Assert.That(fresh.Call, Is.EqualTo(CallStatus.NotStarted));
        }
    }
}
