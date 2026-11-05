using System;
using System.Collections.Generic;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class CityHighwayTests
    {
        private static bool Overlap(CityRect a, CityRect b) => a.MinX < b.MaxX && b.MinX < a.MaxX && a.MinZ < b.MaxZ && b.MinZ < a.MaxZ;

        [Test] public void TheSouthernDriveJoinsTheSouthStreetToTheHighway()
        {
            var drive = CityRoads.SouthernDrive;
            Assert.That(drive.MaxZ, Is.EqualTo(CityRoads.SouthStreet.MinZ)); Assert.That(drive.MinZ, Is.EqualTo(CityRoads.HighwayNorthZ));
            Assert.That(drive.CenterX, Is.Zero, "on the office's axis");
            for (float z = CityRoads.SouthZ; z >= CityRoads.HighwaySouthZ + 0.1f; z -= 0.5f)
                Assert.That(CityRoads.IsDrivable(-1, z) || Math.Abs(z - CityRoads.HighwayZ) <= CityRoads.MedianHalf && !CityRoads.IsDrivable(-1, z), Is.True, $"z {z}");
            // Straight down the drive and across both carriageways through the gap in the median.
            for (float z = CityRoads.SouthZ; z >= CityRoads.HighwaySouthZ + 0.1f; z -= 0.25f) Assert.That(CityRoads.IsDrivable(0, z), Is.True, $"z {z}");
            // The median is closed beyond its gap; the carriageways reach into the tubes past the loop.
            Assert.That(CityRoads.IsDrivable(CityRoads.MedianGapHalf + 0.5f, CityRoads.HighwayZ), Is.False);
            Assert.That(CityRoads.IsDrivable(CityRoads.HighwayLoopX + 3, CityRoads.HighwayZ + 3), Is.True);
            Assert.That(CityRoads.IsDrivable(-CityRoads.HighwayLoopX - 3, CityRoads.HighwayZ - 3), Is.True);
            Assert.That(CityRoads.HighwayEndX, Is.LessThan(CityScenery.TunnelEndX), "the asphalt ends before the tube's far wall");
            Assert.That(CityRoads.HighwayLoopX - PlayerCar.HalfLength, Is.GreaterThan(CityScenery.TunnelPortalX + 4), "the car loops deep inside the tube");
            Assert.That(CityRoads.HighwayLoopX + 2.3f, Is.LessThan(CityRoads.HighwayEndX), "even a car turned across the tube loops before the asphalt ends");
            // Two lanes a way: the carriageways are as wide as two lanes and the spare half-metres.
            Assert.That(CityRoads.HighwayWestbound.Depth, Is.EqualTo(2 * CityRoads.LaneWidth + 1).Within(1e-4f));
            Assert.That(CityRoads.HighwayEastbound.Depth, Is.EqualTo(2 * CityRoads.LaneWidth + 1).Within(1e-4f));
        }
        [Test] public void TheNewQuarterStandsClearOfTheRoadsAndTheTunnels()
        {
            var frontage = CityRoads.Frontage;
            int drive = 0, row = 0;
            var banks = new List<CityRect>();
            foreach (int sign in new[] { -1, 1 })
                banks.Add(sign < 0 ? new CityRect(-CityScenery.EmbankmentEndX, CityScenery.HighwayBankMinZ, -CityScenery.TunnelPortalX, CityScenery.HighwayBankMaxZ)
                    : new CityRect(CityScenery.TunnelPortalX, CityScenery.HighwayBankMinZ, CityScenery.EmbankmentEndX, CityScenery.HighwayBankMaxZ));
            foreach (var b in frontage)
            {
                var foot = b.Footprint;
                if (foot.MaxZ < CityRoads.FrontageSouthZ - 14 && foot.MinZ > CityRoads.FrontageHighwayZ + 13) drive++;
                if (Math.Abs(foot.MinZ - CityRoads.FrontageHighwayZ) < 1e-3f) { row++; Assert.That(b.Yaw, Is.Zero, "fronts toward the highway"); }
                foreach (var bank in banks) Assert.That(Overlap(foot, bank), Is.False);
            }
            Assert.That(drive, Is.EqualTo(2)); Assert.That(row, Is.EqualTo(8));
            // Lamps of the new roads: down the drive on both sides and along the highway's town side.
            int lamps = 0;
            foreach (var lamp in CityRoads.Lamps) if (lamp.Z < CityRoads.SouthStreet.MinZ - 2) lamps++;
            Assert.That(lamps, Is.EqualTo(11));
            // The skyline gives way to the highway's embankments and stands beyond the green belt.
            foreach (var block in CityScenery.Skyline)
            {
                var foot = CityRect.Around(block.X, block.Z, block.Width / 2, block.Depth / 2);
                foreach (var bank in banks) Assert.That(Overlap(foot, bank), Is.False, $"skyline block at ({block.X}, {block.Z})");
                if (Math.Abs(block.X) < CityScenery.TunnelPortalX) Assert.That(foot.MaxZ < CityRoads.HighwaySouthZ - 8 || foot.MinZ > CityRoads.HighwayNorthZ, Is.True);
            }
        }
        [Test] public void HighwayLanesRunRightHandInsideTheCarriageways()
        {
            for (int lane = 0; lane < HighwayTraffic.LaneCount; lane++)
            {
                float z = HighwayTraffic.LaneZ(lane);
                var carriageway = HighwayTraffic.Eastbound(lane) ? CityRoads.HighwayEastbound : CityRoads.HighwayWestbound;
                Assert.That(z - HighwayTraffic.LaneBand, Is.GreaterThanOrEqualTo(carriageway.MinZ)); Assert.That(z + HighwayTraffic.LaneBand, Is.LessThanOrEqualTo(carriageway.MaxZ));
                // Right-hand: the eastbound lanes south of the median.
                Assert.That(z < CityRoads.HighwayZ, Is.EqualTo(HighwayTraffic.Eastbound(lane)));
            }
            Assert.That(HighwayTraffic.LaneZ(1) - HighwayTraffic.LaneZ(0), Is.EqualTo(CityRoads.LaneWidth).Within(1e-4f));
            Assert.Throws<ArgumentOutOfRangeException>(() => HighwayTraffic.LaneZ(4));
            Assert.That(HighwayTraffic.RoadHalf, Is.EqualTo(CityRoads.HighwayLoopX));
            var traffic = new HighwayTraffic();
            Assert.That(traffic.Cars.Count, Is.EqualTo(12));
            foreach (var car in traffic.Cars)
            {
                Assert.That(car.Z, Is.EqualTo(HighwayTraffic.LaneZ(car.Lane))); Assert.That(car.Eastbound, Is.EqualTo(HighwayTraffic.Eastbound(car.Lane)));
                float min = HighwayTraffic.Inner(car.Lane) ? HighwayTraffic.InnerMinCruise : HighwayTraffic.OuterMinCruise;
                float max = HighwayTraffic.Inner(car.Lane) ? HighwayTraffic.InnerMaxCruise : HighwayTraffic.OuterMaxCruise;
                Assert.That(car.Cruise, Is.InRange(min, max));
                Assert.That(CityRoads.IsDrivable(car.X, car.Z), Is.True);
            }
            Assert.That(HighwayTraffic.OuterMaxCruise, Is.LessThan(HighwayTraffic.InnerMinCruise), "the inner lanes are the fast ones");
            // Its own paints and the bodies of the boulevard.
            var styles = new HashSet<CarStyle>(); foreach (var car in traffic.Cars) styles.Add(car.Style);
            Assert.That(styles.Count, Is.GreaterThanOrEqualTo(4));
        }
        [Test] public void HighwayCarsKeepTheirGapsAndLoopThroughTheTunnels()
        {
            var traffic = new HighwayTraffic();
            var travelled = new float[traffic.Cars.Count]; var last = new float[traffic.Cars.Count];
            for (int i = 0; i < last.Length; i++) last[i] = traffic.Cars[i].S;
            for (int step = 0; step < 1200; step++)
            {
                traffic.Advance(0.1);
                for (int i = 0; i < traffic.Cars.Count; i++)
                {
                    var car = traffic.Cars[i];
                    Assert.That(car.S, Is.InRange(0, HighwayTraffic.RingLength)); Assert.That(Math.Abs(car.X), Is.LessThanOrEqualTo(HighwayTraffic.RoadHalf));
                    float moved = car.S - last[i]; if (moved < 0) moved += HighwayTraffic.RingLength;
                    travelled[i] += moved; last[i] = car.S;
                    for (int j = 0; j < traffic.Cars.Count; j++)
                    {
                        var other = traffic.Cars[j];
                        if (j == i || other.Lane != car.Lane) continue;
                        float gap = other.S - car.S; if (gap < 0) gap += HighwayTraffic.RingLength;
                        if (gap < HighwayTraffic.RoadHalf) Assert.That(gap - (car.Length + other.Length) / 2, Is.GreaterThan(HighwayTraffic.BumperGap - 0.01f), $"cars {i} and {j}");
                    }
                }
            }
            // Two minutes: every car has been round the highway several times at highway speed.
            foreach (float d in travelled) Assert.That(d / 120, Is.InRange(HighwayTraffic.OuterMinCruise - 1.5f, HighwayTraffic.InnerMaxCruise));
            Assert.Throws<ArgumentOutOfRangeException>(() => traffic.Advance(-1));
        }
        [Test] public void HighwayCarsBrakeForTheStandingCarAndNeverTouchIt()
        {
            var traffic = new HighwayTraffic();
            var car = new PlayerCar();
            car.Place(20, HighwayTraffic.LaneZ(0), 0);
            for (int step = 0; step < 600; step++)
            {
                traffic.Advance(0.05, car.Footprint, 0);
                foreach (var other in traffic.Cars) Assert.That(car.Footprint.Penetration(other.Footprint, out _, out _), Is.LessThanOrEqualTo(0), "never into the car");
            }
            // The first lane queues behind it; the others drive on.
            int standing = 0;
            foreach (var other in traffic.Cars)
            {
                if (other.Lane == 0) { Assert.That(other.Speed, Is.LessThan(0.1f)); Assert.That(other.Braking, Is.True); standing++; }
                else Assert.That(other.Speed, Is.GreaterThan(HighwayTraffic.OuterMinCruise - 1.5f));
            }
            Assert.That(standing, Is.EqualTo(HighwayTraffic.CarsPerLane));
            Assert.That(HighwayTraffic.InLane(car.Footprint, 0), Is.True); Assert.That(HighwayTraffic.InLane(car.Footprint, 1), Is.False);
            // Gone again: the queue drives off.
            car.Place(0, 0, 0);
            traffic.Advance(20, car.Footprint, 0);
            foreach (var other in traffic.Cars) Assert.That(other.Speed, Is.GreaterThan(HighwayTraffic.OuterMinCruise - 1.5f));
        }
        [Test] public void TheCarComesOutOfTheOtherTunnel()
        {
            var car = new PlayerCar();
            car.Place(CityRoads.HighwayLoopX - 1, HighwayTraffic.LaneZ(0), 0, 15);
            car.Advance(0.2, new DriveInput(0.3f, 0, false));
            Assert.That(car.X, Is.LessThan(-CityRoads.HighwayLoopX + 3), "out of the west tunnel"); Assert.That(car.Contacts, Is.Zero);
            Assert.That(car.Z, Is.EqualTo(HighwayTraffic.LaneZ(0)).Within(0.05f)); Assert.That(car.Kmh, Is.GreaterThan(50));
            car.Place(-CityRoads.HighwayLoopX + 1, HighwayTraffic.LaneZ(3), (float)Math.PI, 15);
            car.Advance(0.2, DriveInput.None);
            Assert.That(car.X, Is.GreaterThan(CityRoads.HighwayLoopX - 3), "westbound out of the east tunnel");
            // The boulevard's tunnels and the streets do not loop.
            float x = CityRoads.HighwayLoopX + 1, z = CityRoads.HighwayZ + 3;
            Assert.That(CityRoads.Ring.Wrap(ref x, z), Is.True); Assert.That(x, Is.EqualTo(-CityRoads.HighwayLoopX + 1).Within(1e-4f));
            x = 85; Assert.That(CityRoads.Ring.Wrap(ref x, 35), Is.False); Assert.That(x, Is.EqualTo(85));
            x = 50; Assert.That(CityRoads.Ring.Wrap(ref x, CityRoads.HighwayZ), Is.False);
            var track = new DriveArea(new CityRect(-10, -10, 10, 10)); x = 20;
            Assert.That(track.Loop.HasValue, Is.False); Assert.That(track.Wrap(ref x, 0), Is.False);
            Assert.Throws<ArgumentException>(() => new DriveArea(new CityRect(0, 0, 0, 5), OrientedRect.From(new CityRect(0, 0, 1, 1))));
        }
        [Test] public void TheCarRunsIntoAHighwayCarAndStopsAgainstIt()
        {
            var traffic = new HighwayTraffic(); var ahead = traffic.Cars[0];
            var car = new PlayerCar();
            car.Place(ahead.X - ahead.Length / 2 - PlayerCar.HalfLength - 1.5f, ahead.Z, 0, 12);
            car.Advance(0.5, DriveInput.None, null, null, traffic);
            Assert.That(car.Contacts, Is.GreaterThan(0));
            Assert.That(car.Footprint.Penetration(ahead.Footprint, out _, out _), Is.LessThan(0.02f));
            Assert.That(car.X, Is.LessThan(ahead.X));
        }
        [Test] public void CityLifeDrivesTheHighwayTraffic()
        {
            var life = new CityLife(); float before = life.Highway.Cars[0].S;
            life.Advance(2, false, 0, 0);
            float moved = life.Highway.Cars[0].S - before; if (moved < 0) moved += HighwayTraffic.RingLength;
            Assert.That(moved, Is.GreaterThan(2 * HighwayTraffic.OuterMinCruise - 4));
            // The player's car standing in a highway lane holds that lane's cars.
            life.Car.Place(0, HighwayTraffic.LaneZ(1), 0);
            life.Advance(30, false, 0, 0);
            foreach (var other in life.Highway.Cars)
                if (other.Lane == 1) Assert.That(life.Car.Footprint.Penetration(other.Footprint, out _, out _), Is.LessThanOrEqualTo(0));
        }
    }
}
