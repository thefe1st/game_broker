using System;
using System.Collections.Generic;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class CityDistrictTests
    {
        [Test] public void EightDestinationsAreUniqueWalkableAndImmutable()
        {
            Assert.That(CityDistrictPlan.Destinations.Count, Is.EqualTo(8)); var ids = new HashSet<string>();
            foreach (var d in CityDistrictPlan.Destinations) { Assert.That(ids.Add(d.Id), Is.True); Assert.That(CityDistrictPlan.CanWalk(d.X, d.Z), Is.True); }
            Assert.That(CityDistrictPlan.Find("unknown"), Is.Null);
            Assert.Throws<NotSupportedException>(() => ((IList<CityDestination>)CityDistrictPlan.Destinations)[0] = null);
        }
        [Test] public void AllDestinationsConnectByPedestrianGridWithoutCrossingRoadOrFacades()
        {
            var seen = new HashSet<(int x, int z)>(); var queue = new Queue<(int x, int z)>(); queue.Enqueue((0, 16)); seen.Add((0, 16));
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (var delta in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                { var next = (x: p.x + delta.Item1, z: p.z + delta.Item2); if (CityDistrictPlan.CanWalk(next.x, next.z) && seen.Add(next)) queue.Enqueue(next); }
            }
            foreach (var d in CityDistrictPlan.Destinations) Assert.That(seen.Contains(((int)d.X, (int)d.Z)), Is.True, d.Id);
            Assert.That(seen.Count, Is.GreaterThan(2000));
        }
        [TestCase(10, 35)] [TestCase(3, 35)] [TestCase(0, 0)] [TestCase(-30, -6)] [TestCase(30, -6)] [TestCase(45, 16)] [TestCase(0, -13)]
        [TestCase(12, 56)] [TestCase(24, -9.8f)] [TestCase(5, 60.2f)] [TestCase(-20, 50)] [TestCase(0, 76.5f)]
        public void RoadOfficeVoidFacadesAndWallsAreNotWalkable(float x, float z)
        { Assert.That(CityDistrictPlan.CanWalk(x, z), Is.False); }
        [TestCase(0, 35)] [TestCase(-1.9f, 29)] [TestCase(0, 43)] [TestCase(0, 55.5f)] [TestCase(0, 58)] [TestCase(24, -5)] [TestCase(22.7f, -2.2f)]
        [TestCase(0, 60.2f)] [TestCase(-8, 70)] [TestCase(11, 74)]
        public void ZebraPavementStepsPodiumCafeAndHallAreWalkable(float x, float z)
        { Assert.That(CityDistrictPlan.CanWalk(x, z), Is.True); }
        [Test] public void FloorRisesFromThePlazaOverTheZebraAndStepsToThePodium()
        {
            Assert.That(CityDistrictPlan.FloorY(0, 16), Is.Zero); Assert.That(CityDistrictPlan.FloorY(0, 35), Is.EqualTo(CityDistrictPlan.RoadFloorY));
            Assert.That(CityDistrictPlan.FloorY(0, 48), Is.EqualTo(CityDistrictPlan.NorthFloorY));
            Assert.That(CityDistrictPlan.FloorY(0, 66), Is.EqualTo(CityDistrictPlan.PodiumFloorY));
            float previous = CityDistrictPlan.NorthFloorY;
            for (float z = CityDistrictPlan.StepsMinZ; z <= CityDistrictPlan.PodiumMinZ; z += 0.1f)
            { float y = CityDistrictPlan.FloorY(0, z); Assert.That(y, Is.GreaterThanOrEqualTo(previous - 1e-5f)); previous = y; }
            Assert.That(CityDistrictPlan.PodiumFloorY - CityDistrictPlan.NorthFloorY, Is.LessThan((CityDistrictPlan.PodiumMinZ - CityDistrictPlan.StepsMinZ) * 0.6f), "steps stay walkable");
            Assert.That(CityDistrictPlan.Road.Contains(0, 35), Is.True); Assert.That(CityDistrictPlan.Road.Contains(0, 27), Is.False);
        }
        [Test] public void MapDistanceAndCardinalDirectionDoNotChangeDayState()
        {
            var day = new BrokerDay(); var west = CityDistrictPlan.Find("park"); float seconds = day.RemainingSeconds;
            Assert.That(CityDistrictPlan.Distance(0, 16, west), Is.EqualTo(31)); Assert.That(CityDistrictPlan.Direction(0, 16, west), Is.EqualTo("Запад"));
            Assert.That(CityDistrictPlan.Direction(-31, 16, west), Does.Contain("Вы у"));
            Assert.That(CityDistrictPlan.Direction(0, 16, CityDistrictPlan.Find("exchange")), Is.EqualTo("Север · через переход"));
            Assert.That(CityDistrictPlan.Direction(0, 49, CityDistrictPlan.Find("cafe")), Is.EqualTo("Юг · через переход"));
            Assert.That(CityDistrictPlan.Direction(0, 49, CityDistrictPlan.Find("exchange")), Is.EqualTo("Север"));
            Assert.That(day.Cash, Is.Zero); Assert.That(day.RemainingSeconds, Is.EqualTo(seconds));
            Assert.Throws<ArgumentException>(() => CityDistrictPlan.Distance(float.NaN, 0, west));
            Assert.Throws<ArgumentException>(() => CityDistrictPlan.Distance(0, 0, null));
            Assert.That(CityDistrictPlan.IsPedestrianArea(float.PositiveInfinity, 16), Is.False);
        }
        [TestCase(OfficeStation.CityMap, OfficeView.CityMap)] [TestCase(OfficeStation.CityCompanies, OfficeView.CityCompanies)]
        [TestCase(OfficeStation.CityExchange, OfficeView.CityExchange)] [TestCase(OfficeStation.CityCafe, OfficeView.CityCafe)]
        public void NewInformationStationsPauseWithoutBecomingCityErrands(OfficeStation station, OfficeView view)
        {
            var nav = new OfficeNavigation(); Assert.That(nav.Open(station), Is.True); Assert.That(nav.View, Is.EqualTo(view));
            Assert.That(nav.RunsClock, Is.False); nav.Close(); Assert.That(nav.RunsClock, Is.True);
        }
        [Test] public void CrosswalkButtonIsNotAPanel()
        {
            var nav = new OfficeNavigation(); Assert.That(nav.Open(OfficeStation.CrosswalkButton), Is.False);
            Assert.That(nav.IsExploring, Is.True); Assert.That(nav.RunsClock, Is.True);
        }
    }
}
