using System;
using System.Collections.Generic;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class CityRoadsTests
    {
        private static bool Near(RoadPoint p, float x, float z) => Math.Abs(p.X - x) < 1e-3f && Math.Abs(p.Z - z) < 1e-3f;
        private static bool Overlap(CityRect a, CityRect b) => a.MinX < b.MaxX && b.MinX < a.MaxX && a.MinZ < b.MaxZ && b.MinZ < a.MaxZ;

        [TestCase(0, 35)] [TestCase(-78, 30)] [TestCase(78, 40)] [TestCase(-50, 0)] [TestCase(50, 0)] [TestCase(0, -20)]
        [TestCase(-24.75f, 27.35f)] [TestCase(-31.5f, 27.9f)] [TestCase(-17.8f, 27.9f)] [TestCase(-56, 28)] [TestCase(-45.2f, 28.3f)]
        [TestCase(-45, -15)] [TestCase(53.9f, -23.9f)] [TestCase(0, -24.5f)] [TestCase(-2, -50)] [TestCase(0, -92)] [TestCase(60, -98.25f)]
        [TestCase(-60, -85.75f)] [TestCase(88, -90)] [TestCase(-5.2f, -24.6f)] [TestCase(5.5f, -82.5f)]
        public void RingRoadsAreDrivable(float x, float z) { Assert.That(CityRoads.IsDrivable(x, z), Is.True); }
        [TestCase(0, 0)] [TestCase(0, 28.5f)] [TestCase(0, 41.5f)] [TestCase(85, 35)] [TestCase(-33, 27)] [TestCase(-18, 26.5f)]
        [TestCase(-24.75f, 25.9f)] [TestCase(-45, 27)] [TestCase(-57.5f, 28)] [TestCase(-58, 0)] [TestCase(-8, -24.5f)] [TestCase(-44, -14)]
        [TestCase(0, -15.5f)] [TestCase(30, 20)] [TestCase(5, -50)] [TestCase(20, -92)] [TestCase(0, -101)] [TestCase(20, -83.3f)] [TestCase(-96, -90)]
        [TestCase(-6.5f, -25.5f)]
        public void SquaresKerbsTunnelsAndTheOfficeAreNot(float x, float z) { Assert.That(CityRoads.IsDrivable(x, z), Is.False); }

        [Test] public void TheRingIsOneConnectedRoadFromTheLayBy()
        {
            const float cell = 0.25f, minX = -96, minZ = -102;
            int nx = (int)(192 / cell), nz = (int)(146 / cell), total = 0, reached = 0;
            var road = new bool[nx, nz]; var seen = new bool[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                    if (road[i, j] = CityRoads.IsDrivable(minX + (i + 0.5f) * cell, minZ + (j + 0.5f) * cell)) total++;
            var queue = new Queue<(int, int)>();
            int si = (int)((CityRoads.ParkX - minX) / cell), sj = (int)((CityRoads.ParkZ - minZ) / cell);
            Assert.That(road[si, sj], Is.True);
            queue.Enqueue((si, sj)); seen[si, sj] = true;
            while (queue.Count > 0)
            {
                var (i, j) = queue.Dequeue(); reached++;
                for (int di = -1; di <= 1; di++)
                    for (int dj = -1; dj <= 1; dj++)
                    {
                        int a = i + di, b = j + dj;
                        if (a < 0 || b < 0 || a >= nx || b >= nz || seen[a, b] || !road[a, b]) continue;
                        seen[a, b] = true; queue.Enqueue((a, b));
                    }
            }
            Assert.That(reached, Is.EqualTo(total));
            // A loop round the office quarter with the Southern drive and the highway: every piece is part of it.
            foreach (var rect in CityRoads.Drivable)
                Assert.That(seen[(int)((rect.CenterX - minX) / cell), (int)((rect.CenterZ - minZ) / cell)], Is.True);
        }
        [Test] public void RoadsKeepClearOfWalkwaysExceptTheZebra()
        {
            // The asphalt and a strip round it, for the kerb, never reach the walkable plan; walkers cross only on the zebra.
            for (float x = -95; x <= 95; x += 0.2f)
                for (float z = -102; z <= 43; z += 0.2f)
                {
                    if (Math.Abs(x) <= CityLife.ZebraZone.MaxX || CityRoads.Ring.Outside(x, z, out _, out _) > 0.25f) continue;
                    Assert.That(CityDistrictPlan.CanWalk(x, z), Is.False, $"({x:F1}, {z:F1})");
                }
        }
        [Test] public void PropsAndSkylineStandOffTheRoads()
        {
            const float kerb = 2 * CityRoads.KerbHalf;
            foreach (var item in CityScenery.Props)
            {
                if (item.Overhead) continue;
                var near = CityRect.Around(item.X, item.Z, item.HalfX + kerb, item.HalfZ + kerb);
                foreach (var piece in CityRoads.Ring.Pieces) Assert.That(piece.Overlaps(near), Is.False, $"{item.Kind} at ({item.X}, {item.Z})");
            }
            foreach (var obstacle in CityScenery.Obstacles)
                foreach (var piece in CityRoads.Ring.Pieces) Assert.That(piece.Overlaps(obstacle), Is.False);
            foreach (var block in CityScenery.Skyline)
            {
                var foot = CityRect.Around(block.X, block.Z, block.Width / 2 + kerb, block.Depth / 2 + kerb);
                foreach (var piece in CityRoads.Ring.Pieces) Assert.That(piece.Overlaps(foot), Is.False);
            }
        }
        [Test] public void RoadsideBuildingsStandBehindThePavementsAndClearOfTheCity()
        {
            const float behind = 2 * CityRoads.KerbHalf + CityRoads.PavementWidth + CityRoads.FrontageGap - 0.01f;
            var frontage = CityRoads.Frontage;
            Assert.That(frontage.Count, Is.EqualTo(22));
            var others = new List<CityRect>();
            foreach (var block in CityScenery.Skyline) others.Add(CityRect.Around(block.X, block.Z, block.Width / 2, block.Depth / 2));
            foreach (var tower in new CityBlockPlan().Buildings) others.Add(CityRect.Around(tower.X, tower.Z, tower.Width / 2, tower.Depth / 2));
            var district = new CityRect(CityDistrictPlan.MinX, CityDistrictPlan.MinZ, CityDistrictPlan.MaxX, CityDistrictPlan.MaxZ);
            for (int i = 0; i < frontage.Count; i++)
            {
                var b = frontage[i]; var foot = b.Footprint;
                Assert.That(b.Front, Is.GreaterThanOrEqualTo(5)); Assert.That(b.Depth, Is.GreaterThanOrEqualTo(5)); Assert.That(b.Height, Is.InRange(8, 40));
                Assert.That(b.Style, Is.InRange(0, 2)); Assert.That(Math.Abs(b.Yaw) % 90, Is.Zero);
                var grown = new CityRect(foot.MinX - behind, foot.MinZ - behind, foot.MaxX + behind, foot.MaxZ + behind);
                foreach (var piece in CityRoads.Ring.Pieces) Assert.That(piece.Overlaps(grown), Is.False, $"building {i}");
                Assert.That(Overlap(foot, district), Is.False, $"building {i}");
                foreach (var other in others) Assert.That(Overlap(foot, other), Is.False, $"building {i}");
                for (int j = 0; j < i; j++) Assert.That(Overlap(foot, frontage[j].Footprint), Is.False, $"buildings {j} and {i}");
                // The front (the model's −Z turned by the yaw) faces the nearest road.
                double yaw = b.Yaw * Math.PI / 180;
                float fx = -(float)Math.Sin(yaw), fz = -(float)Math.Cos(yaw);
                float ahead = CityRoads.Ring.Outside(b.X + fx * 10, b.Z + fz * 10, out _, out _), here = CityRoads.Ring.Outside(b.X, b.Z, out _, out _);
                Assert.That(ahead, Is.LessThan(here - 9), $"building {i}");
            }
        }
        [Test] public void TheChaseCameraStopsShortOfTheBuildings()
        {
            const float margin = 0.4f;
            Assert.That(CityRoads.Solids.Count, Is.GreaterThan(CityRoads.Frontage.Count));
            foreach (var solid in CityRoads.Solids)
                foreach (var piece in CityRoads.Drivable) Assert.That(Overlap(solid, piece), Is.False, solid + " over " + piece);
            // Down the west street nothing is in the way; toward a west tower the way ends its margin short of the facade.
            Assert.That(CityRoads.ClearFraction(CityRoads.WestX, -18, CityRoads.WestX, 20, margin), Is.EqualTo(1));
            RoadsideBuilding west = null;
            foreach (var building in CityRoads.Frontage) if (building.Yaw < 0) { west = building; break; }
            Assert.That(west, Is.Not.Null);
            float facade = west.Footprint.MaxX + margin;
            Assert.That(CityRoads.ClearFraction(CityRoads.WestX, west.Z, CityRoads.WestX - 12, west.Z, margin),
                Is.EqualTo((CityRoads.WestX - facade) / 12).Within(1e-4f));
            // Behind the south street the office block, north of the boulevard the towers; a start beside a solid is not
            // trapped by it.
            Assert.That(CityRoads.ClearFraction(0, CityRoads.SouthZ, 0, 0, margin),
                Is.EqualTo((CityRoads.OfficeBlock.MinZ - margin - CityRoads.SouthZ) / -CityRoads.SouthZ).Within(1e-4f));
            Assert.That(CityRoads.ClearFraction(-37.5f, 38, -37.5f, 48, margin), Is.EqualTo((44.5f - margin - 38) / 10).Within(1e-4f));
            Assert.That(CityRoads.ClearFraction(0, 0, 0, -6, margin), Is.EqualTo(1));
            Assert.That(CityRoads.ClearFraction(70, 35, 90, 35, margin), Is.EqualTo((CityScenery.TunnelPortalX - margin - 70) / 20).Within(1e-4f));
            // The highway runs on into its tube; the embankments beside the mouth stop the camera.
            Assert.That(CityRoads.ClearFraction(70, CityRoads.HighwayZ, 90, CityRoads.HighwayZ, margin), Is.EqualTo(1));
            Assert.That(CityRoads.ClearFraction(70, -76, 90, -76, margin), Is.EqualTo((CityScenery.TunnelPortalX - margin - 70) / 20).Within(1e-4f));
            Assert.Throws<ArgumentException>(() => CityRoads.ClearFraction(float.NaN, 0, 0, 0, margin));
        }
        [Test] public void RoadsideLampsStandOnTheOuterPavementsWithArmsOverTheRoad()
        {
            Assert.That(CityRoads.Lamps.Count, Is.EqualTo(21));
            foreach (var lamp in CityRoads.Lamps)
            {
                float off = CityRoads.Ring.Outside(lamp.X, lamp.Z, out _, out _);
                Assert.That(off, Is.InRange(2 * CityRoads.KerbHalf + 0.2f, 2 * CityRoads.KerbHalf + CityRoads.PavementWidth - 0.5f), $"({lamp.X}, {lamp.Z})");
                double yaw = lamp.Yaw * Math.PI / 180;
                float arm = CityRoads.Ring.Outside(lamp.X + 0.5f * (float)Math.Sin(yaw), lamp.Z + 0.5f * (float)Math.Cos(yaw), out _, out _);
                Assert.That(arm, Is.EqualTo(off - 0.5f).Within(1e-3f), $"({lamp.X}, {lamp.Z})");
            }
        }
        [Test] public void KerbCornersPointingIntoTheRoadAreTheCutEnds()
        {
            var expected = new[]
            {
                (-54f, 25.675f), (-57f, 28.675f), (-44.6f, 28.675f), (-46f, 27.275f), (46f, 27.275f), (44.6f, 28.675f), (57f, 28.675f), (54f, 25.675f),
                (-46f, -13f), (-43f, -16f), (43f, -16f), (46f, -13f), (-32.575f, 28.675f), (-16.925f, 28.675f),
                (-4f, -26f), (-6f, -24f), (6f, -24f), (4f, -26f), (-7f, -83.5f), (-4f, -80.5f), (4f, -80.5f), (7f, -83.5f),
                (-10f, -92.5f), (-10f, -91.5f), (10f, -92.5f), (10f, -91.5f),
            };
            var corners = CityRoads.Ring.InnerCorners;
            Assert.That(corners.Count, Is.EqualTo(expected.Length));
            foreach (var (x, z) in expected) Assert.That(new List<RoadPoint>(corners).Exists(p => Near(p, x, z)), Is.True, $"({x}, {z})");
            // A plain L-turn has one; a rectangle none.
            Assert.That(new DriveArea(new CityRect(-10, 0, 10, 8), new CityRect(-4, -10, 4, 0)).InnerCorners.Count, Is.EqualTo(2));
            Assert.That(new DriveArea(new CityRect(-10, 0, 10, 8)).InnerCorners.Count, Is.Zero);
            Assert.Throws<ArgumentException>(() => new DriveArea(new CityRect[0]));
        }
        [Test] public void CutsAreSlantedAtFortyFiveDegreesOnTheKerbOpenings()
        {
            Assert.That(CityRoads.Cuts.Count, Is.EqualTo(12));
            foreach (var cut in CityRoads.Cuts)
                Assert.That(Math.Abs(Math.Abs(Math.Sin(2 * cut.Heading)) - 1), Is.LessThan(1e-4), $"{cut.Heading}");
            // The scene paves each cut triangle and sets its kerb on the hypotenuse: road up to it, kerb beyond.
            Assert.That(CityRoads.KerbCuts.Count, Is.EqualTo(CityRoads.Cuts.Count));
            foreach (var cut in CityRoads.KerbCuts)
            {
                Assert.That(cut.Start.Z, Is.EqualTo(cut.Z)); Assert.That(cut.End.X, Is.EqualTo(cut.X));
                Assert.That(Math.Abs(cut.Start.X - cut.X), Is.EqualTo(cut.Leg).Within(1e-4f));
                Assert.That(CityRoads.IsDrivable(cut.X + cut.Sx * cut.Leg * 0.3f, cut.Z + cut.Sz * cut.Leg * 0.3f), Is.True, $"({cut.X}, {cut.Z})");
                Assert.That(CityRoads.IsDrivable(cut.X + cut.Sx * cut.Leg * 0.7f, cut.Z + cut.Sz * cut.Leg * 0.7f), Is.False, $"({cut.X}, {cut.Z})");
            }
            Assert.Throws<ArgumentException>(() => new KerbCut(0, 0, 0, 1, 1));
            Assert.Throws<ArgumentException>(() => new KerbCut(0, 0, 1, 1, 0));
            Assert.That(CityRoads.SouthKerbCuts.Count, Is.EqualTo(3));
            float last = float.MinValue;
            foreach (var gap in CityRoads.SouthKerbCuts)
            {
                Assert.That(gap.MinX, Is.GreaterThan(last)); last = gap.MaxX;
                Assert.That(CityRoads.IsDrivable(gap.CenterX, CityRoads.BoulevardMinZ - 0.1f), Is.True);
                Assert.That(CityRoads.IsDrivable(gap.MinX - 0.1f, CityRoads.BoulevardMinZ - 0.1f), Is.False);
                Assert.That(CityRoads.IsDrivable(gap.MaxX + 0.1f, CityRoads.BoulevardMinZ - 0.1f), Is.False);
            }
        }
        [Test] public void BoulevardLanesFitTheRoadAndTheParkedCarLeavesThemFree()
        {
            for (int lane = 0; lane < CityTraffic.LaneCount; lane++)
            {
                float z = CityTraffic.LaneZ(lane);
                Assert.That(z - CityTraffic.LaneBand, Is.GreaterThan(CityRoads.BoulevardMinZ)); Assert.That(z + CityTraffic.LaneBand, Is.LessThan(CityRoads.BoulevardMaxZ));
            }
            var car = new PlayerCar();
            for (int lane = 0; lane < CityTraffic.LaneCount; lane++) Assert.That(CityTraffic.InLane(car.Footprint, lane), Is.False);
            for (int i = 0; i < 4; i++)
            {
                car.Footprint.Corner(i, out float x, out float z);
                Assert.That(CityRoads.Bay.Contains(x, z), Is.True); Assert.That(CityRoads.ParkingZone.Contains(x, z), Is.True);
            }
            car.Place(0, CityTraffic.LaneZ(0), 0);
            Assert.That(CityTraffic.InLane(car.Footprint, 0), Is.True); Assert.That(CityTraffic.InLane(car.Footprint, 1), Is.False);
            car.Place(0, (CityTraffic.LaneZ(1) + CityTraffic.LaneZ(2)) / 2, 0);
            Assert.That(CityTraffic.InLane(car.Footprint, 1), Is.True); Assert.That(CityTraffic.InLane(car.Footprint, 2), Is.True);
            car.Place(0, CityTraffic.LaneZ(3), (float)Math.PI / 2);
            Assert.That(CityTraffic.InLane(car.Footprint, 2), Is.True); Assert.That(CityTraffic.InLane(car.Footprint, 3), Is.True);
        }
        [Test] public void TheParkingDestinationAndTheExitLieOnTheSquareBehindTheRailing()
        {
            var parking = CityDistrictPlan.Find("parking");
            Assert.That(parking, Is.Not.Null); Assert.That(parking.Name, Is.EqualTo("Парковка у сквера"));
            Assert.That(parking.Description, Does.Contain("E у машины")); Assert.That(parking.Description, Does.Contain("Выйти можно только здесь"));
            Assert.That(Math.Abs(parking.X - CityRoads.ParkX), Is.LessThan(1)); Assert.That(parking.Z, Is.LessThan(CityRoads.BayRailZ));
            Assert.That(CityRoads.BayRailZ, Is.InRange(25.8f, CityRoads.BayMinZ));
            for (float x = -29; x <= -20.5f; x += 0.25f)
            {
                Assert.That(CityDistrictPlan.CanWalk(x, CityRoads.ExitZ), Is.True);
                Assert.That(CityRoads.IsDrivable(x, CityRoads.ExitZ + 0.5f), Is.False);
            }
            Assert.That(CityRoads.ParkingZone.Contains(CityRoads.ParkX, CityRoads.ParkZ), Is.True);
            Assert.That(CityRoads.ParkingZone.MaxZ, Is.LessThan(CityTraffic.LaneZ(0) - CityTraffic.LaneBand + 0.5f));
        }
        [Test] public void TheCarIsTheSedanOfTheBoulevard()
        {
            var shape = CarModels.Sedan;
            Assert.That(2 * PlayerCar.HalfLength, Is.EqualTo(shape.Length).Within(1e-4f));
            Assert.That(2 * PlayerCar.HalfWidth, Is.EqualTo(shape.Width).Within(1e-4f));
            Assert.That(PlayerCar.ToFrontAxle, Is.EqualTo(shape.FrontAxleX).Within(1e-4f));
            Assert.That(PlayerCar.ToRearAxle, Is.EqualTo(-shape.RearAxleX).Within(1e-4f));
            Assert.That(PlayerCar.WheelRadius, Is.EqualTo(shape.WheelRadius).Within(1e-4f));
            Assert.That(new CityTraffic().Cars, Has.Some.Matches<CityCar>(c => c.Style == CarStyle.Sedan), "the player drives one of the boulevard bodies");
            Assert.That(CarModels.Paints[PlayerCar.Paint].Name, Is.EqualTo("красный"));
            Assert.That(PlayerCar.Paint, Is.Not.EqualTo(CarModels.TaxiPaint));
        }
        [Test] public void OrientedRectanglesMeasureAndSeparate()
        {
            var r = new OrientedRect(1, 2, (float)Math.PI / 2, 2, 1);
            Assert.That(r.Contains(1, 3.9f), Is.True); Assert.That(r.Contains(1.9f, 2), Is.True); Assert.That(r.Contains(2.1f, 2), Is.False);
            Assert.That(r.Contains(2.1f, 2, 0.2f), Is.True); Assert.That(r.Distance(1, 7), Is.EqualTo(3).Within(1e-4f));
            var b = r.Bounds; Assert.That(b.MinX, Is.EqualTo(0).Within(1e-4f)); Assert.That(b.MaxZ, Is.EqualTo(4).Within(1e-4f));
            r.Corner(0, out float fx, out float fz); Assert.That(fx, Is.EqualTo(0).Within(1e-4f)); Assert.That(fz, Is.EqualTo(4).Within(1e-4f));
            r.Local(0, 4, out float along, out float side); Assert.That(along, Is.EqualTo(2).Within(1e-4f)); Assert.That(side, Is.EqualTo(1).Within(1e-4f));
            var other = new OrientedRect(2.5f, 2, 0, 1, 1);
            Assert.That(r.Penetration(other, out float nx, out float nz), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(nx, Is.EqualTo(-1).Within(1e-4f)); Assert.That(nz, Is.EqualTo(0).Within(1e-4f));
            Assert.That(r.Overlaps(new CityRect(5, 5, 6, 6)), Is.False);
            var edge = OrientedRect.OnEdge(0, 0, 4, 0, 2);
            Assert.That(edge.Contains(2, 1.9f), Is.True); Assert.That(edge.Contains(2, -0.1f), Is.False);
            Assert.That(r.Outside(1, 7, out float tx, out float tz), Is.EqualTo(3).Within(1e-4f)); Assert.That(tz, Is.EqualTo(-1).Within(1e-5f));
            Assert.That(r.Outside(1.5f, 2.5f, out tx, out tz), Is.Zero); Assert.That(tx, Is.Zero);
            // A huge piece stays exact at its edges: 0 inside, and the way back runs straight across the edge.
            var half = new DriveArea(new CityRect(-3000, -3000, 3000, 0));
            Assert.That(half.Outside(0.3f, -0.93f, out _, out _), Is.Zero);
            Assert.That(half.Outside(12.7f, 0.25f, out tx, out tz), Is.EqualTo(0.25f).Within(1e-3f));
            Assert.That(tx, Is.EqualTo(0).Within(1e-6f)); Assert.That(tz, Is.EqualTo(-1).Within(1e-6f));
            Assert.Throws<ArgumentException>(() => OrientedRect.OnEdge(1, 1, 1, 1, 1));
            Assert.Throws<ArgumentException>(() => new OrientedRect(0, 0, 0, -1, 1));
        }
    }
}
