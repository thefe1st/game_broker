using System;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class CityPresentationTests
    {
        [Test] public void CityPlanIsDeterministicAndBounded()
        {
            var a = new CityBlockPlan(); var b = new CityBlockPlan();
            Assert.That(a.Buildings.Count, Is.EqualTo(18));
            for (int i = 0; i < a.Buildings.Count; i++)
            {
                var x = a.Buildings[i]; var y = b.Buildings[i];
                Assert.That(x.X, Is.EqualTo(y.X)); Assert.That(x.Z, Is.EqualTo(y.Z)); Assert.That(x.Height, Is.EqualTo(y.Height));
                Assert.That(x.Height, Is.InRange(12, 36)); Assert.That(x.Width, Is.InRange(8, 10));
                Assert.That(x.Style, Is.InRange(0, 2));
                Assert.That(x.Z - x.Depth / 2, Is.GreaterThan(CityBlockPlan.PlazaMaxZ));
            }
        }
        [Test] public void BuildingFootprintsDoNotOverlap()
        {
            var list = new CityBlockPlan().Buildings;
            for (int i = 0; i < list.Count; i++) for (int j = i + 1; j < list.Count; j++)
                Assert.That(Math.Abs(list[i].X - list[j].X) >= (list[i].Width + list[j].Width) / 2
                    || Math.Abs(list[i].Z - list[j].Z) >= (list[i].Depth + list[j].Depth) / 2, Is.True);
        }
        [Test] public void SpawnIsInsideWalkablePlazaAndAboveGround()
        {
            Assert.That(CityBlockPlan.SpawnX, Is.InRange(CityBlockPlan.PlazaMinX + 2, CityBlockPlan.PlazaMaxX - 2));
            Assert.That(CityBlockPlan.SpawnZ, Is.InRange(CityBlockPlan.PlazaMinZ + 2, CityBlockPlan.PlazaMaxZ - 2));
            Assert.That(CityBlockPlan.SpawnY, Is.GreaterThan(CityBlockPlan.GroundY));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void VisualTrafficIsBoundedAndLoops(int lane)
        {
            for (int t = 0; t < 1000; t++) Assert.That(CityBlockPlan.TrafficX(t, lane), Is.InRange(-50, 50));
            Assert.That(CityBlockPlan.TrafficX(0, lane), Is.EqualTo(CityBlockPlan.TrafficX(lane % 2 == 0 ? 20 : 50.0 / 3, lane)).Within(0.001));
        }
        [Test] public void InvalidTrafficInputsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CityBlockPlan.TrafficX(double.NaN, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CityBlockPlan.TrafficX(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CityBlockPlan.TrafficX(0, 4));
        }
        [Test] public void StreetPortalsAreNotMistakenForEndOfDayOrDeskViews()
        {
            var nav = new OfficeNavigation();
            Assert.That(nav.Open(OfficeStation.Street), Is.False); Assert.That(nav.Open(OfficeStation.ReturnOffice), Is.False);
            Assert.That(nav.IsExploring, Is.True); Assert.That(nav.RunsClock, Is.True);
        }
        [Test] public void CompactHintsFollowActualConversationState()
        {
            var day = new BrokerDay(); Assert.That(day.ConversationStep, Is.EqualTo(ConversationStage.AwaitingCall));
            day.StartCall(); Assert.That(day.ConversationStep, Is.EqualTo(ConversationStage.Goal));
            day.Say("Какая цель?"); Assert.That(day.ConversationStep, Is.EqualTo(ConversationStage.Risk));
            day.Say("Есть риск"); Assert.That(day.ConversationStep, Is.EqualTo(ConversationStage.Fee));
            day.Say("Комиссия 2%"); Assert.That(day.ConversationStep, Is.EqualTo(ConversationStage.Proposal));
            day.OfferDeal(); Assert.That(day.ConversationStep, Is.EqualTo(ConversationStage.Objection));
            day.Say("Оставьте резерв"); Assert.That(day.ConversationStep, Is.EqualTo(ConversationStage.Ready));
            day.OfferDeal(); Assert.That(day.ConversationStep, Is.EqualTo(ConversationStage.Closed));
            day.EndDay(); Assert.That(day.ConversationStep, Is.EqualTo(ConversationStage.DayEnded));
        }
        [Test] public void AllCompactHintsAreShortAndNonempty()
        {
            foreach (ConversationStage stage in Enum.GetValues(typeof(ConversationStage)))
                foreach (string id in new[] { "boris", "inna", "artem" })
                { string hint = BrokerPresentation.ShortHint(stage, id); Assert.That(hint.Length, Is.InRange(4, 36)); }
        }
    }
}
