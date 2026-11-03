using System;
using System.Collections.Generic;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class VoxelTests
    {
        private static void AssertOutwardWinding(VoxelMesh mesh)
        {
            var all = new List<int>(mesh.Triangles); all.AddRange(mesh.GlowTriangles);
            Assert.That(all.Count % 3, Is.Zero);
            for (int t = 0; t < all.Count; t += 3)
            {
                int a = all[t] * 3, b = all[t + 1] * 3, c = all[t + 2] * 3; var p = mesh.Positions; var n = mesh.Normals;
                float ux = p[b] - p[a], uy = p[b + 1] - p[a + 1], uz = p[b + 2] - p[a + 2];
                float vx = p[c] - p[a], vy = p[c + 1] - p[a + 1], vz = p[c + 2] - p[a + 2];
                float cx = uy * vz - uz * vy, cy = uz * vx - ux * vz, cz = ux * vy - uy * vx;
                Assert.That(cx * n[a] + cy * n[a + 1] + cz * n[a + 2], Is.GreaterThan(0), "triangle " + t / 3);
            }
        }

        [Test] public void SingleVoxelBuildsSixOutwardQuadsAroundBottomCentrePivot()
        {
            var model = new VoxelModel(1, 1, 1, 0.5f); model.Set(0, 0, 0, VoxelPalette.Concrete);
            var mesh = VoxelMesher.Build(model);
            Assert.That(mesh.QuadCount, Is.EqualTo(6)); Assert.That(mesh.VertexCount, Is.EqualTo(24));
            Assert.That(mesh.Triangles.Length, Is.EqualTo(36)); Assert.That(mesh.GlowTriangles, Is.Empty);
            AssertOutwardWinding(mesh);
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                Assert.That(Math.Abs(mesh.Positions[i * 3]), Is.EqualTo(0.25f).Within(1e-6));
                Assert.That(mesh.Positions[i * 3 + 1], Is.EqualTo(0).Or.EqualTo(0.5f).Within(1e-6));
                Assert.That(mesh.Uvs[i * 2], Is.EqualTo(VoxelPalette.U(VoxelPalette.Concrete)));
                Assert.That(mesh.Uvs[i * 2 + 1], Is.EqualTo(VoxelPalette.V(VoxelPalette.Concrete)));
            }
        }
        [Test] public void GreedyMeshingMergesEqualColoursAndCullsHiddenFaces()
        {
            var box = new VoxelModel(3, 2, 4, 1); box.Box(0, 0, 0, 3, 2, 4, VoxelPalette.BrickRed);
            Assert.That(VoxelMesher.Build(box).QuadCount, Is.EqualTo(6));
            var pair = new VoxelModel(2, 1, 1, 1); pair.Set(0, 0, 0, VoxelPalette.BrickRed); pair.Set(1, 0, 0, VoxelPalette.Plaster);
            var mesh = VoxelMesher.Build(pair);
            Assert.That(mesh.QuadCount, Is.EqualTo(10), "the shared face is hidden");
            AssertOutwardWinding(mesh);
            var hollow = new VoxelModel(3, 3, 3, 1); hollow.Box(0, 0, 0, 3, 3, 3, VoxelPalette.Stone); hollow.Set(1, 1, 1, VoxelPalette.Empty);
            Assert.That(VoxelMesher.Build(hollow).QuadCount, Is.EqualTo(6 + 6), "an enclosed cavity still has inner faces");
        }
        [Test] public void GlowingCellsUseTheirOwnSubmesh()
        {
            var lamp = new VoxelModel(2, 1, 1, 1); lamp.Set(0, 0, 0, VoxelPalette.Metal); lamp.Set(1, 0, 0, VoxelPalette.LampWarm);
            var mesh = VoxelMesher.Build(lamp);
            Assert.That(mesh.Triangles.Length, Is.EqualTo(5 * 6)); Assert.That(mesh.GlowTriangles.Length, Is.EqualTo(5 * 6));
            Assert.That(VoxelPalette.Glows(VoxelPalette.LampWarm), Is.True); Assert.That(VoxelPalette.Glows(VoxelPalette.Metal), Is.False);
        }
        [Test] public void PaletteIsAnEightByEightTextureWithoutPureBlackOrWhite()
        {
            byte[] rgba = VoxelPalette.TextureRgba(); Assert.That(rgba.Length, Is.EqualTo(64 * 4));
            for (int i = 1; i < VoxelPalette.Count; i++)
            {
                VoxelPalette.Rgb(i, out byte r, out byte g, out byte b);
                Assert.That(r + g + b, Is.GreaterThan(30).And.LessThan(255 * 3 - 10), "colour " + i);
                Assert.That(VoxelPalette.U(i), Is.GreaterThan(0).And.LessThan(1)); Assert.That(VoxelPalette.V(i), Is.GreaterThan(0).And.LessThan(1));
                Assert.That(VoxelPalette.Glows(i), Is.EqualTo(i >= 56));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => VoxelPalette.Rgb(64, out _, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VoxelModel(1, 1, 1, 1).Set(0, 0, 0, 64));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VoxelModel(257, 1, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VoxelModel(1, 1, 1, 0));
        }
        [Test] public void GeneratorsAreDeterministicAndSeedsVaryTheShape()
        {
            var a = VoxelMesher.Build(VoxelProps.RoundTree(3)); var b = VoxelMesher.Build(VoxelProps.RoundTree(3));
            Assert.That(a.Positions, Is.EqualTo(b.Positions)); Assert.That(a.Triangles, Is.EqualTo(b.Triangles));
            Assert.That(VoxelProps.RoundTree(3).Filled, Is.Not.EqualTo(VoxelProps.RoundTree(4).Filled));
            Assert.That(VoxelProps.Car(CarStyle.Sedan, 1).Filled, Is.EqualTo(VoxelProps.Car(CarStyle.Sedan, 1).Filled));
        }
        private static int Count(VoxelModel m, byte color)
        {
            int n = 0;
            for (int y = 0; y < m.SizeY; y++) for (int z = 0; z < m.SizeZ; z++) for (int x = 0; x < m.SizeX; x++) if (m.Get(x, y, z) == color) n++;
            return n;
        }
        private static IEnumerable<(string name, VoxelModel model)> AllProps()
        {
            yield return ("round tree", VoxelProps.RoundTree(1)); yield return ("conifer", VoxelProps.Conifer(2));
            yield return ("shrub", VoxelProps.Shrub(3, 8, 4, 6, true)); yield return ("planter", VoxelProps.Planter(4));
            yield return ("plant", VoxelProps.PottedPlant(5)); yield return ("lamp", VoxelProps.StreetLamp());
            yield return ("bench", VoxelProps.Bench(24, true)); yield return ("plaza bench", VoxelProps.Bench(30, false));
            yield return ("dumpster", VoxelProps.Dumpster()); yield return ("signal", VoxelProps.TrafficLight());
            yield return ("map stand", VoxelProps.InfoStand(StandStyle.Map)); yield return ("company stand", VoxelProps.InfoStand(StandStyle.Companies));
            yield return ("barrier", VoxelProps.ConstructionBarrier()); yield return ("cone", VoxelProps.Cone());
            yield return ("bollard", VoxelProps.Bollard()); yield return ("bin", VoxelProps.Bin());
            foreach (CarStyle style in Enum.GetValues(typeof(CarStyle))) yield return ("car " + style, VoxelProps.Car(style, 7));
            yield return ("desk", VoxelProps.Desk(3.6f, 0.85f, 1.25f, VoxelPalette.Wood, VoxelPalette.WoodDark));
            yield return ("chair", VoxelProps.OfficeChair(VoxelPalette.Navy)); yield return ("keyboard", VoxelProps.Keyboard());
            yield return ("mouse", VoxelProps.Mouse()); yield return ("coffee", VoxelProps.CoffeeMachine());
            foreach (ShopStyle style in Enum.GetValues(typeof(ShopStyle))) yield return ("shop " + style, VoxelProps.Shop(style, 9));
            foreach (var b in new CityBlockPlan().Buildings) yield return ("tower", VoxelProps.Tower(b.Width, b.Depth, b.Height, (TowerStyle)b.Style, 11));
            foreach (PersonStyle style in Enum.GetValues(typeof(PersonStyle)))
            {
                var f = VoxelProps.Person(style);
                yield return ("body " + style, f.Body); yield return ("head " + style, f.Head); yield return ("arm " + style, f.LeftArm);
            }
        }
        [Test] public void EveryPropMeshesIntoAClosedOutwardSurfaceUnderTheIndexLimit()
        {
            foreach (var (name, model) in AllProps())
            {
                Assert.That(model.Filled, Is.GreaterThan(0), name);
                var mesh = VoxelMesher.Build(model);
                Assert.That(mesh.VertexCount, Is.GreaterThan(0).And.LessThan(65000), name);
                Assert.That(mesh.Positions.Length, Is.EqualTo(mesh.Normals.Length), name);
                AssertOutwardWinding(mesh);
                float minY = float.MaxValue; for (int i = 1; i < mesh.Positions.Length; i += 3) minY = Math.Min(minY, mesh.Positions[i]);
                if (!name.StartsWith("head") && !name.StartsWith("arm")) Assert.That(minY, Is.EqualTo(0).Within(1e-5), name + " stands on its pivot");
            }
        }
        [Test] public void CarsHaveLightsAtBothEndsAndFourWheelsOnTheGround()
        {
            foreach (CarStyle style in Enum.GetValues(typeof(CarStyle)))
            {
                var car = VoxelProps.Car(style, 2);
                Assert.That(car.Width, Is.EqualTo(3.6f).Within(0.01f)); Assert.That(car.Depth, Is.EqualTo(1.65f).Within(0.01f));
                Assert.That(Count(car, VoxelPalette.Headlight), Is.EqualTo(4), style.ToString());
                Assert.That(Count(car, VoxelPalette.TailLight), Is.EqualTo(4), style.ToString());
                for (int x = 0; x < car.SizeX; x++) for (int z = 0; z < car.SizeZ; z++)
                {
                    if (car.Get(x, 0, z) == VoxelPalette.Empty) continue;
                    Assert.That(car.Get(x, 0, z), Is.EqualTo(VoxelPalette.Tire), "only tyres touch the road");
                }
                Assert.That(car.Get(23, car.SizeY - 1, 5), Is.EqualTo(VoxelPalette.Empty));
            }
            Assert.That(Count(VoxelProps.Car(CarStyle.Taxi, 0), VoxelPalette.SignalAmber), Is.GreaterThan(0));
        }
        [Test] public void TowersMatchTheCityPlanFootprintAndLightSomeWindows()
        {
            int lit = 0, windows = 0;
            foreach (var b in new CityBlockPlan().Buildings)
            {
                var tower = VoxelProps.Tower(b.Width, b.Depth, b.Height, (TowerStyle)b.Style, (int)b.X);
                Assert.That(tower.Width - 1, Is.EqualTo(b.Width).Within(0.51f)); Assert.That(tower.Depth - 1, Is.EqualTo(b.Depth).Within(0.51f));
                Assert.That(tower.Height, Is.GreaterThanOrEqualTo(b.Height)); Assert.That(tower.Height, Is.LessThan(b.Height + 3));
                lit += Count(tower, VoxelPalette.WindowLit);
                windows += Count(tower, VoxelPalette.Glass) + Count(tower, VoxelPalette.GlassDark) + Count(tower, VoxelPalette.GlassSky) + Count(tower, VoxelPalette.WindowLit);
            }
            Assert.That(lit / (float)windows, Is.InRange(0.04f, 0.25f));
            Assert.Throws<ArgumentOutOfRangeException>(() => VoxelProps.Tower(1, 11, 20, TowerStyle.Brick, 0));
        }
        [Test] public void FiguresStandAboutOnePointEightFiveMetresWithJointsAtNeckAndShoulders()
        {
            foreach (PersonStyle style in Enum.GetValues(typeof(PersonStyle)))
            {
                var f = VoxelProps.Person(style);
                float top = f.NeckY + (f.Head.SizeY - f.Head.PivotY) * f.Head.VoxelSize;
                Assert.That(top, Is.InRange(1.8f, 2.0f), style.ToString());
                Assert.That(f.ShoulderY, Is.EqualTo(f.Body.Height).Within(1e-5));
                Assert.That(f.ShoulderX, Is.GreaterThan(f.Body.Width / 2)); Assert.That(f.LeftArm.PivotY, Is.EqualTo(f.LeftArm.SizeY));
                Assert.That(Count(f.Head, VoxelPalette.Ink), Is.EqualTo(2), "two eyes on the −Z face");
                Assert.That(f.Head.Get(2, (int)f.Head.PivotY + 4, 0), Is.EqualTo(VoxelPalette.Ink));
            }
        }
        [Test] public void PixelSurfacesAreTileableDeterministicAndUsePaletteColours()
        {
            foreach (string key in VoxelSurfaces.Keys)
            {
                byte[] a = VoxelSurfaces.Rgba(key), b = VoxelSurfaces.Rgba(key);
                Assert.That(a.Length, Is.EqualTo(32 * 32 * 4)); Assert.That(a, Is.EqualTo(b));
                var colours = new HashSet<byte>();
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                {
                    Assert.That(VoxelSurfaces.Texel(key, x, y), Is.EqualTo(VoxelSurfaces.Texel(key, x + 32, y - 32)));
                    colours.Add(VoxelSurfaces.Texel(key, x, y));
                }
                Assert.That(colours.Count, Is.InRange(2, 6), key);
                Assert.That(colours.Contains(VoxelPalette.Empty), Is.False, key);
            }
            Assert.That(VoxelSurfaces.Has("marble"), Is.False);
            Assert.Throws<ArgumentException>(() => VoxelSurfaces.Rgba("marble"));
        }
        [Test] public void SceneryKeepsTheDistrictWalkableAndTheSkylineInsideTheFarClip()
        {
            var solid = new List<SceneryItem>();
            foreach (var item in CityScenery.Props)
            {
                Assert.That(item.Z < 29 || item.Z > 41, Is.True, "nothing stands on the boulevard");
                foreach (var b in new CityBlockPlan().Buildings)
                    Assert.That(Math.Abs(item.X - b.X) > b.Width / 2 + 0.4f || Math.Abs(item.Z - b.Z) > b.Depth / 2 + 0.4f, Is.True, item.Kind + " inside a tower");
                Assert.That(CityDistrictPlan.IsBuildingFootprint(item.X, item.Z), Is.False);
                if (item.Solid) { Assert.That(CityDistrictPlan.CanWalk(item.X, item.Z), Is.True); solid.Add(item); }
            }
            Assert.That(solid.Count, Is.GreaterThan(5));
            bool Free(float x, float z)
            {
                if (!CityDistrictPlan.CanWalk(x, z)) return false;
                foreach (var s in solid) if (Math.Abs(x - s.X) < 0.9f && Math.Abs(z - s.Z) < 0.9f) return false;
                return true;
            }
            var seen = new HashSet<(int, int)>(); var queue = new Queue<(int x, int z)>(); queue.Enqueue((0, 32)); seen.Add((0, 32));
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var next = (x: p.x + dx, z: p.z + dz);
                    if (Free(next.x * 0.5f, next.z * 0.5f) && seen.Add(next)) queue.Enqueue(next);
                }
            }
            foreach (var d in CityDistrictPlan.Destinations) Assert.That(seen.Contains(((int)(d.X * 2), (int)(d.Z * 2))), Is.True, d.Id);
            Assert.That(CityScenery.Skyline.Count, Is.GreaterThan(60));
            foreach (var block in CityScenery.Skyline)
            {
                Assert.That(Math.Abs(block.X) > 60 || block.Z < -40 || block.Z > 120, Is.True, "skyline stays outside the playable city");
                foreach (var corner in new[] { (-44f, -12f), (44f, -12f), (-44f, 28f), (44f, 28f) })
                {
                    double dx = Math.Abs(block.X - corner.Item1) + block.Width / 2, dz = Math.Abs(block.Z - corner.Item2) + block.Depth / 2;
                    Assert.That(Math.Sqrt(dx * dx + dz * dz), Is.LessThan(CityScenery.FarClip - 5));
                }
                Assert.That(Math.Abs(block.X) + block.Width / 2, Is.LessThan(CityScenery.TerrainSize / 2));
                Assert.That(Math.Abs(block.Z - CityScenery.TerrainCenterZ) + block.Depth / 2, Is.LessThan(CityScenery.TerrainSize / 2));
            }
        }
    }
}
