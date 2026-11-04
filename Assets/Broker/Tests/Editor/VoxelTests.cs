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
        // Grid corner of vertex i (pivot and voxel size undone).
        private static (float x, float y, float z) Grid(VoxelModel model, VoxelMesh mesh, int i)
            => (mesh.Positions[i * 3] / model.VoxelSize + model.PivotX, mesh.Positions[i * 3 + 1] / model.VoxelSize + model.PivotY,
                mesh.Positions[i * 3 + 2] / model.VoxelSize + model.PivotZ);
        // The vertex at grid corner (x, y, z) of a face with normal (nx, ny, nz); −1 when no such face has a corner there.
        private static int VertexAt(VoxelModel model, VoxelMesh mesh, int nx, int ny, int nz, int x, int y, int z)
        {
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                if (mesh.Normals[i * 3] != nx || mesh.Normals[i * 3 + 1] != ny || mesh.Normals[i * 3 + 2] != nz) continue;
                var g = Grid(model, mesh, i);
                if (Math.Abs(g.x - x) < 1e-4f && Math.Abs(g.y - y) < 1e-4f && Math.Abs(g.z - z) < 1e-4f) return i;
            }
            return -1;
        }
        private static int Uses(VoxelMesh mesh, int vertex) { int n = 0; foreach (int i in mesh.Triangles) if (i == vertex) n++; return n; }
        // Upward faces on the grid plane y.
        private static int TopQuads(VoxelModel model, int y)
        {
            var mesh = VoxelMesher.Build(model); int n = 0;
            for (int q = 0; q < mesh.QuadCount; q++) if (mesh.Normals[q * 12 + 1] > 0.5f && Math.Abs(Grid(model, mesh, q * 4).y - y) < 1e-4f) n++;
            return n;
        }
        // Every shade is one of the four corner levels, and a quad longer than one voxel along an edge (corners 0→1 or 0→3)
        // keeps the same shade along it: the interpolation must not smear a corner over the merged run.
        private static void AssertMergesKeepShading(VoxelMesh mesh, float voxel, string name)
        {
            Assert.That(mesh.Shades.Length, Is.EqualTo(mesh.VertexCount), name);
            int foreign = 0, smeared = 0;
            foreach (byte shade in mesh.Shades) if (Array.IndexOf(VoxelMesher.CornerShade, shade) < 0) foreign++;
            float Edge(int a, int b)
            {
                float dx = mesh.Positions[b * 3] - mesh.Positions[a * 3], dy = mesh.Positions[b * 3 + 1] - mesh.Positions[a * 3 + 1], dz = mesh.Positions[b * 3 + 2] - mesh.Positions[a * 3 + 2];
                return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }
            var s = mesh.Shades;
            for (int v = 0; v < mesh.VertexCount; v += 4)
            {
                if (Edge(v, v + 1) > voxel * 1.01f && (s[v] != s[v + 1] || s[v + 3] != s[v + 2])) smeared++;
                if (Edge(v, v + 3) > voxel * 1.01f && (s[v] != s[v + 3] || s[v + 1] != s[v + 2])) smeared++;
            }
            Assert.That(foreign, Is.Zero, name + ": shades outside the corner levels");
            Assert.That(smeared, Is.Zero, name + ": merged quads that change shade along their length");
        }

        [Test] public void SingleVoxelBuildsSixOutwardQuadsAroundBottomCentrePivot()
        {
            var model = new VoxelModel(1, 1, 1, 0.5f); model.Set(0, 0, 0, VoxelPalette.Concrete);
            var mesh = VoxelMesher.Build(model);
            Assert.That(mesh.QuadCount, Is.EqualTo(6)); Assert.That(mesh.VertexCount, Is.EqualTo(24));
            Assert.That(mesh.Triangles.Length, Is.EqualTo(36)); Assert.That(mesh.GlowTriangles, Is.Empty);
            AssertOutwardWinding(mesh);
            Assert.That(mesh.Shades.Length, Is.EqualTo(mesh.VertexCount)); Assert.That(mesh.Shades, Is.All.EqualTo((byte)255), "nothing shades a lone voxel");
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
        [Test] public void CornerShadingDarkensInnerCornersAndMergesOnlyAlongEvenShading()
        {
            // A 2×2 floor with an L of walls on it: the free floor cell sits in an inner corner.
            var corner = new VoxelModel(2, 2, 2, 1); corner.Box(0, 0, 0, 2, 1, 2, VoxelPalette.Stone);
            corner.Set(0, 1, 0, VoxelPalette.Stone); corner.Set(0, 1, 1, VoxelPalette.Stone); corner.Set(1, 1, 0, VoxelPalette.Stone);
            var mesh = VoxelMesher.Build(corner);
            AssertOutwardWinding(mesh); AssertMergesKeepShading(mesh, 1, "corner");
            int inner = VertexAt(corner, mesh, 0, 1, 0, 1, 1, 1), open = VertexAt(corner, mesh, 0, 1, 0, 2, 1, 2);
            int alongX = VertexAt(corner, mesh, 0, 1, 0, 2, 1, 1), alongZ = VertexAt(corner, mesh, 0, 1, 0, 1, 1, 2);
            Assert.That(new[] { inner, open, alongX, alongZ }, Has.None.EqualTo(-1), "the free floor cell has an upward face");
            Assert.That(mesh.Shades[inner], Is.EqualTo(VoxelMesher.CornerShade[0]), "both walls touch it: darkest level");
            Assert.That(mesh.Shades[alongX], Is.EqualTo(VoxelMesher.CornerShade[2]), "one wall touches it");
            Assert.That(mesh.Shades[alongZ], Is.EqualTo(VoxelMesher.CornerShade[2]), "one wall touches it");
            Assert.That(mesh.Shades[open], Is.EqualTo(VoxelMesher.CornerShade[3]), "nothing touches it");
            // The diagonal joins the two half-lit corners, so the dark corner fades within one triangle instead of along a streak.
            Assert.That(Uses(mesh, inner), Is.EqualTo(1)); Assert.That(Uses(mesh, open), Is.EqualTo(1));
            Assert.That(Uses(mesh, alongX), Is.EqualTo(2)); Assert.That(Uses(mesh, alongZ), Is.EqualTo(2));
            Assert.That(VoxelMesher.CornerShade, Is.Ordered.Ascending); Assert.That(VoxelMesher.CornerShade[3], Is.EqualTo(255));

            // An open slab merges into one top quad; a wall along its edge shades the row beside it, which then
            // merges only along the wall and splits at the two ends, where the wall's corner is missing.
            var slab = new VoxelModel(4, 3, 4, 1); slab.Box(0, 0, 0, 4, 1, 4, VoxelPalette.Stone);
            Assert.That(TopQuads(slab, 1), Is.EqualTo(1));
            slab.Box(0, 1, 0, 4, 2, 1, VoxelPalette.Stone);
            Assert.That(TopQuads(slab, 1), Is.EqualTo(4), "far rows, the shaded middle of the near row, its two ends");
            AssertMergesKeepShading(VoxelMesher.Build(slab), 1, "slab");
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
            yield return ("desk", VoxelProps.Desk(3.6f, 0.85f, 1.25f, VoxelPalette.Wood, VoxelPalette.WoodDark));
            yield return ("chair", VoxelProps.OfficeChair(VoxelPalette.Navy)); yield return ("keyboard", VoxelProps.Keyboard());
            yield return ("mouse", VoxelProps.Mouse()); yield return ("coffee", VoxelProps.CoffeeMachine());
            foreach (ShopStyle style in Enum.GetValues(typeof(ShopStyle)))
            {
                yield return ("shop " + style, VoxelProps.Shop(style, 9)); yield return ("shop back " + style, VoxelProps.Shop(style, 9, true));
            }
            foreach (var b in new CityBlockPlan().Buildings) yield return ("tower", VoxelProps.Tower(b.Width, b.Depth, b.Height, (TowerStyle)b.Style, 11));
            foreach (var b in CityRoads.Frontage) yield return ("ring tower", VoxelProps.Tower(b.Front, b.Depth, b.Height, (TowerStyle)b.Style, 40));
            yield return ("office block", VoxelProps.Tower(31, 19.5f, 7.5f, TowerStyle.Plaster, 5, false, false, true));
            foreach (PersonStyle style in Enum.GetValues(typeof(PersonStyle)))
            {
                var f = VoxelProps.Person(style);
                yield return ("body " + style, f.Body); yield return ("head " + style, f.Head); yield return ("arm " + style, f.LeftArm);
            }
            foreach (WalkerStyle style in Enum.GetValues(typeof(WalkerStyle)))
            {
                var w = VoxelProps.Walker(style);
                yield return ("torso " + style, w.Torso); yield return ("head walker " + style, w.Head); yield return ("arm walker " + style, w.Arm);
                yield return ("leg " + style, w.Leg); yield return ("seated " + style, w.SeatedLegs);
            }
            yield return ("podium", VoxelProps.ExchangePodium()); yield return ("hall", VoxelProps.ExchangeHall());
            yield return ("roof", VoxelProps.ExchangeRoof()); yield return ("column", VoxelProps.ExchangeColumn());
            yield return ("quote board", VoxelProps.QuoteBoard()); yield return ("rostral column", VoxelProps.RostralColumn());
            yield return ("bull", VoxelProps.ExchangeStatue(true)); yield return ("bear", VoxelProps.ExchangeStatue(false));
            yield return ("kiosk", VoxelProps.Kiosk()); yield return ("lawn", VoxelProps.Lawn(3)); yield return ("hedge", VoxelProps.Hedge(9.2f, 4));
            yield return ("cafe", VoxelProps.CafeShell()); yield return ("counter", VoxelProps.CafeCounter()); yield return ("shelf", VoxelProps.CafeShelf(5));
            yield return ("table", VoxelProps.CafeTable()); yield return ("cafe chair", VoxelProps.CafeChair()); yield return ("menu", VoxelProps.MenuBoard());
            yield return ("pendant lamp", VoxelProps.PendantLamp()); yield return ("signal post", VoxelProps.SignalPost());
            for (int lit = -1; lit <= 2; lit++) yield return ("car signal " + lit, VoxelProps.CarSignalHead(lit));
            for (int lit = -1; lit <= 1; lit++) yield return ("walk signal " + lit, VoxelProps.PedSignalHead(lit));
            yield return ("button", VoxelProps.CrosswalkButton(false)); yield return ("button waiting", VoxelProps.CrosswalkButton(true));
            yield return ("tunnel portal", VoxelProps.TunnelPortal());
        }
        // Parts hung from a joint or a ceiling: their pivot is at the top.
        private static bool Hanging(string name) => name.StartsWith("head") || name.StartsWith("arm") || name.StartsWith("leg") || name.StartsWith("seated") || name.StartsWith("pendant");
        [Test] public void EveryPropMeshesIntoAClosedOutwardSurfaceUnderTheIndexLimit()
        {
            foreach (var (name, model) in AllProps())
            {
                Assert.That(model.Filled, Is.GreaterThan(0), name);
                var mesh = VoxelMesher.Build(model);
                Assert.That(mesh.VertexCount, Is.GreaterThan(0).And.LessThan(65000), name);
                Assert.That(mesh.Positions.Length, Is.EqualTo(mesh.Normals.Length), name);
                AssertOutwardWinding(mesh); AssertMergesKeepShading(mesh, model.VoxelSize, name);
                float minY = float.MaxValue; for (int i = 1; i < mesh.Positions.Length; i += 3) minY = Math.Min(minY, mesh.Positions[i]);
                if (!Hanging(name)) Assert.That(minY, Is.EqualTo(0).Within(1e-5), name + " stands on its pivot");
            }
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
        // The backs that face the ring road get windows only when asked; the rest of the model stays as it was.
        [Test] public void BacksSeenFromTheRingHaveWindowsOnlyWhenAsked()
        {
            const int panes = 2 * 3 * 4 * 6;
            foreach (ShopStyle style in Enum.GetValues(typeof(ShopStyle)))
            {
                var plain = VoxelProps.Shop(style, 9); var windowed = VoxelProps.Shop(style, 9, true);
                Assert.That(BackPanes(plain, plain.SizeZ - 1, -1), Is.Zero, "plain back " + style);
                Assert.That(BackPanes(windowed, windowed.SizeZ - 1, -1), Is.EqualTo(panes), "windowed back " + style);
                for (int z = 0; z < plain.SizeZ - 2; z++) for (int y = 0; y < plain.SizeY; y++) for (int x = 0; x < plain.SizeX; x++)
                    if (windowed.Get(x, y, z) != plain.Get(x, y, z)) Assert.Fail("shop " + style + " changed in front of its back wall");
            }
            Assert.That(BackPanes(VoxelProps.CafeShell(), 0, 1), Is.EqualTo(panes), "café back");
            var office = VoxelProps.Tower(31, 19.5f, 7.5f, TowerStyle.Plaster, 5, false, false, true);
            var blank = VoxelProps.Tower(31, 19.5f, 7.5f, TowerStyle.Plaster, 5, false, false);
            Assert.That(BackPanes(office, office.SizeZ - 2, -1), Is.GreaterThan(0), "office back");
            Assert.That(BackPanes(blank, blank.SizeZ - 2, -1), Is.Zero, "blank tower back");
        }
        // Glass behind the open cells of a facade layer z, one voxel along inward: the window area of that face.
        private static int BackPanes(VoxelModel m, int z, int inward)
        {
            int n = 0;
            for (int y = 0; y < m.SizeY; y++) for (int x = 0; x < m.SizeX; x++)
            {
                if (m.Get(x, y, z) != VoxelPalette.Empty) continue;
                byte pane = m.Get(x, y, z + inward);
                if (pane == VoxelPalette.Glass || pane == VoxelPalette.GlassDark || pane == VoxelPalette.GlassSky || pane == VoxelPalette.WindowLit) n++;
            }
            return n;
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
        private static int TopRow(VoxelModel m)
        {
            for (int y = m.SizeY - 1; y >= 0; y--) for (int z = 0; z < m.SizeZ; z++) for (int x = 0; x < m.SizeX; x++) if (m.Get(x, y, z) != VoxelPalette.Empty) return y;
            return -1;
        }
        private static int Glowing(VoxelModel m)
        {
            int n = 0;
            for (int y = 0; y < m.SizeY; y++) for (int z = 0; z < m.SizeZ; z++) for (int x = 0; x < m.SizeX; x++) if (VoxelPalette.Glows(m.Get(x, y, z))) n++;
            return n;
        }
        [Test] public void WalkersMatchTheStaffHeightSwingFromTheirJointsAndLookDifferent()
        {
            var looks = new HashSet<string>();
            foreach (WalkerStyle style in Enum.GetValues(typeof(WalkerStyle)))
            {
                var w = VoxelProps.Walker(style);
                float top = w.NeckY(false) + (TopRow(w.Head) + 1 - w.Head.PivotY) * w.Head.VoxelSize;
                Assert.That(top, Is.InRange(1.8f, 2.0f), style.ToString());
                Assert.That(w.HipY, Is.EqualTo(w.Leg.Height).Within(1e-5)); Assert.That(w.Leg.PivotY, Is.EqualTo(w.Leg.SizeY));
                Assert.That(w.Arm.PivotY, Is.EqualTo(w.Arm.SizeY)); Assert.That(w.SeatedLegs.PivotY, Is.EqualTo(w.SeatedLegs.SizeY));
                Assert.That(w.NeckY(false), Is.EqualTo(w.HipY + w.Torso.Height).Within(1e-5));
                Assert.That(w.NeckY(true), Is.LessThan(w.NeckY(false)));
                Assert.That(w.ShoulderX, Is.GreaterThan(w.Torso.Width / 2)); Assert.That(w.HipX * 2, Is.LessThan(w.Torso.Width));
                // The face is the z = 1 layer: two eyes; only brims and glasses stand in front of it.
                Assert.That(w.Head.Get(2, 4, 1), Is.EqualTo(VoxelPalette.Ink), style.ToString()); Assert.That(w.Head.Get(4, 4, 1), Is.EqualTo(VoxelPalette.Ink));
                // Under the thighs of a seated figure the 0.4 m café seat fits.
                for (int y = 0; y * VoxelProps.PersonVoxel < 0.37f; y++) for (int z = 5; z < w.SeatedLegs.SizeZ; z++) for (int x = 0; x < w.SeatedLegs.SizeX; x++)
                    Assert.That(w.SeatedLegs.Get(x, y, z), Is.EqualTo(VoxelPalette.Empty), style + " seat");
                var look = new System.Text.StringBuilder();
                foreach (var part in new[] { w.Torso, w.Head, w.Arm, w.Leg })
                    for (int c = 1; c < VoxelPalette.Count; c++) look.Append(Count(part, (byte)c)).Append(',');
                Assert.That(looks.Add(look.ToString()), Is.True, style + " looks like another style");
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => VoxelProps.Walker((WalkerStyle)99));
        }
        [Test] public void ExchangeModelsFitThePlanOfTheQuarter()
        {
            var podium = VoxelProps.ExchangePodium();
            Assert.That(podium.Width, Is.EqualTo(2 * CityDistrictPlan.PodiumHalfWidth).Within(0.01f));
            Assert.That(podium.Depth, Is.EqualTo(CityDistrictPlan.PodiumMaxZ - CityDistrictPlan.StepsMinZ).Within(0.01f));
            float baseY = CityBlockPlan.TerrainTopY - CityBlockPlan.GroundY, v = podium.VoxelSize;
            int Column(VoxelModel m, int x, int z) { for (int y = m.SizeY - 1; y >= 0; y--) if (m.Get(x, y, z) != VoxelPalette.Empty) return y + 1; return 0; }
            int middle = podium.SizeX / 2, back = podium.SizeZ / 2;
            Assert.That(baseY + Column(podium, middle, back) * v, Is.EqualTo(CityDistrictPlan.PodiumFloorY).Within(0.01f), "podium top");
            int stepsEnd = (int)Math.Round((CityDistrictPlan.PodiumMinZ - CityDistrictPlan.StepsMinZ) / v);
            float previous = CityDistrictPlan.NorthFloorY;
            for (int z = 0; z < stepsEnd; z += 5)
            {
                float tread = baseY + Column(podium, middle, z) * v;
                Assert.That(tread - previous, Is.InRange(0.15f, 0.21f), "riser at " + z);
                previous = tread;
            }
            Assert.That(CityDistrictPlan.PodiumFloorY - previous, Is.InRange(0.15f, 0.21f), "last riser");
            int stairEdge = (int)Math.Round(CityDistrictPlan.StepsHalfWidth / v);
            Assert.That(Column(podium, middle - stairEdge, 0), Is.LessThan(Column(podium, middle - stairEdge - 1, 0)), "cheek beside the steps");
            Assert.That(baseY + Column(podium, middle - stairEdge - 1, 0) * v, Is.EqualTo(CityDistrictPlan.PodiumFloorY).Within(0.01f));

            var hall = VoxelProps.ExchangeHall(); var walls = CityDistrictPlan.Footprints[3]; var room = CityDistrictPlan.Openings[2]; var door = CityDistrictPlan.Openings[3];
            Assert.That(hall.Width, Is.EqualTo(walls.Width).Within(0.01f)); Assert.That(hall.Depth, Is.EqualTo(walls.Depth).Within(0.01f));
            int Hx(float x) => (int)Math.Floor((x - walls.MinX) / hall.VoxelSize); int Hz(float z) => (int)Math.Floor((z - walls.MinZ) / hall.VoxelSize);
            for (int y = 0; y < 15; y++)
            {
                Assert.That(hall.Get(Hx(door.CenterX), y, 0), Is.EqualTo(VoxelPalette.Empty), "door");
                Assert.That(hall.Get(Hx(door.MinX) - 1, y, 0), Is.Not.EqualTo(VoxelPalette.Empty), "door frame");
                Assert.That(hall.Get(Hx(room.CenterX), y, Hz(room.CenterZ)), Is.EqualTo(VoxelPalette.Empty), "hall");
                Assert.That(hall.Get(Hx(room.MinX), y, Hz(room.CenterZ)), Is.EqualTo(VoxelPalette.Empty), "inner face of the west wall");
                Assert.That(hall.Get(Hx(room.MinX) - 1, y, Hz(room.CenterZ) + 1), Is.Not.EqualTo(VoxelPalette.Empty), "west wall");
            }
            var column = VoxelProps.ExchangeColumn(); var roof = VoxelProps.ExchangeRoof();
            Assert.That(column.Height, Is.EqualTo(hall.Height).Within(0.01f), "columns carry the roof with the walls");
            foreach (var item in CityScenery.Props)
                if (item.Kind == SceneryKind.ExchangeColumn)
                {
                    Assert.That(item.BaseY, Is.EqualTo(CityDistrictPlan.PodiumFloorY).Within(1e-4f));
                    Assert.That(Math.Abs(item.X) + column.Width / 2, Is.LessThanOrEqualTo(roof.Width / 2 - roof.VoxelSize + 0.01f));
                    Assert.That(item.Z - column.Depth / 2, Is.GreaterThanOrEqualTo(CityScenery.ExchangeRoofZ + roof.VoxelSize - 0.01f));
                    Assert.That(item.Z + column.Depth / 2, Is.LessThanOrEqualTo(CityScenery.ExchangeRoofZ + roof.Depth - roof.VoxelSize + 0.01f));
                }
            var board = VoxelProps.QuoteBoard();
            Assert.That(board.Width, Is.LessThan(room.Width)); Assert.That(Glowing(board), Is.GreaterThan(0), "ticker strip");
            Assert.That(Glowing(VoxelProps.RostralColumn()), Is.GreaterThan(0), "brazier flame");
        }
        [Test] public void TheCafeShellOpensOntoItsRoomAndTheSignalsLightOneLampAtATime()
        {
            var cafe = VoxelProps.CafeShell(); var block = CityDistrictPlan.Footprints[2]; var room = CityDistrictPlan.Openings[0]; var door = CityDistrictPlan.Openings[1];
            Assert.That(cafe.Width, Is.EqualTo(block.Width).Within(0.01f)); Assert.That(cafe.SizeZ * cafe.VoxelSize, Is.EqualTo(block.Depth + 2).Within(0.01f));
            int Cx(float x) => (int)Math.Floor((x - block.MinX) / cafe.VoxelSize); int Cz(float z) => (int)Math.Floor((z - block.MinZ) / cafe.VoxelSize);
            for (int y = 0; y < 10; y++)
            {
                Assert.That(cafe.Get(Cx(room.MinX + 0.1f), y, Cz(room.MinZ + 0.1f)), Is.EqualTo(VoxelPalette.Empty), "room corner");
                Assert.That(cafe.Get(Cx(room.MaxX - 0.1f), y, Cz(room.MaxZ - 0.1f)), Is.EqualTo(VoxelPalette.Empty), "room corner");
                Assert.That(cafe.Get(Cx(room.MinX - 0.1f), y, Cz(room.CenterZ)), Is.Not.EqualTo(VoxelPalette.Empty), "west wall");
                Assert.That(cafe.Get(Cx(door.CenterX), y, Cz(block.MaxZ - 0.1f)), Is.EqualTo(VoxelPalette.Empty), "door");
                Assert.That(cafe.Get(Cx(door.MinX - 0.1f), y, Cz(block.MaxZ - 0.1f)), Is.Not.EqualTo(VoxelPalette.Empty), "door jamb");
            }
            Assert.That(cafe.Get(Cx(room.CenterX), 12, Cz(room.CenterZ)), Is.Not.EqualTo(VoxelPalette.Empty), "ceiling at 3 m");
            for (int lit = -1; lit <= 2; lit++) Assert.That(Glowing(VoxelProps.CarSignalHead(lit)), Is.EqualTo(lit < 0 ? 0 : 4), "car lamp " + lit);
            Assert.That(Glowing(VoxelProps.PedSignalHead(-1)), Is.Zero);
            Assert.That(Glowing(VoxelProps.PedSignalHead(0)), Is.EqualTo(7)); Assert.That(Glowing(VoxelProps.PedSignalHead(1)), Is.EqualTo(7));
            Assert.That(Glowing(VoxelProps.CrosswalkButton(true)), Is.GreaterThan(0)); Assert.That(Glowing(VoxelProps.CrosswalkButton(false)), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => VoxelProps.CarSignalHead(3)); Assert.Throws<ArgumentOutOfRangeException>(() => VoxelProps.PedSignalHead(2));
            var portal = VoxelProps.TunnelPortal(); int mouthTop = 0;
            while (portal.Get(portal.SizeX / 2, mouthTop, 0) == VoxelPalette.Empty) mouthTop++;
            Assert.That(mouthTop * portal.VoxelSize, Is.EqualTo(CityScenery.TunnelHeight).Within(0.01f));
            int left = portal.SizeX / 2; while (portal.Get(left - 1, 0, 0) == VoxelPalette.Empty) left--;
            Assert.That((portal.SizeX / 2 - left) * 2 * portal.VoxelSize, Is.EqualTo(CityScenery.TunnelMaxZ - CityScenery.TunnelMinZ).Within(0.01f));
            Assert.That(VoxelProps.Hedge(9.2f, 1).Width, Is.EqualTo(9.25f).Within(0.01f));
            Assert.Throws<ArgumentOutOfRangeException>(() => VoxelProps.Hedge(0, 1));
        }
        // Smooth surfaces in the manner of Schedule I: no pixel noise, seamless tiles, each close to its palette colour.
        [Test] public void SurfacesAreSmoothSeamlessAndKeepTheirPaletteColour()
        {
            int n = VoxelSurfaces.Size;
            Assert.That(n, Is.EqualTo(128));
            foreach (string key in VoxelSurfaces.Keys)
            {
                byte[] a = VoxelSurfaces.Rgba(key), b = VoxelSurfaces.Rgba(key);
                Assert.That(a.Length, Is.EqualTo(n * n * 4)); Assert.That(a, Is.EqualTo(b), key + " is deterministic");
                double r = 0, g = 0, bl = 0, step = 0, edge = 0, luma = 0, luma2 = 0;
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    int i = (y * n + x) * 4, right = (y * n + (x + 1) % n) * 4, up = (((y + 1) % n) * n + x) * 4;
                    Assert.That(a[i + 3], Is.EqualTo(255));
                    r += a[i]; g += a[i + 1]; bl += a[i + 2];
                    double l = 0.2126 * a[i] + 0.7152 * a[i + 1] + 0.0722 * a[i + 2]; luma += l; luma2 += l * l;
                    double d = 0;
                    for (int k = 0; k < 3; k++) d += Math.Abs(a[i + k] - a[right + k]) + Math.Abs(a[i + k] - a[up + k]);
                    step += d / 6; if (x == n - 1 || y == n - 1) edge += d / 6;
                }
                int count = n * n;
                // Neighbouring texels, also across the tile edge, are nearly equal: no pixel-art speckle, no seam where tiles meet.
                Assert.That(step / count, Is.LessThan(4), key + " is smooth");
                Assert.That(edge / (2 * n - 1), Is.LessThan(3 * step / count + 2), key + " tiles without a seam");
                VoxelPalette.Rgb(VoxelSurfaces.BaseColor(key), out byte br, out byte bg, out byte bb);
                double near = key == "windows" ? 25 : 18;
                Assert.That(Math.Abs(r / count - br), Is.LessThan(near), key + " red");
                Assert.That(Math.Abs(g / count - bg), Is.LessThan(near), key + " green");
                Assert.That(Math.Abs(bl / count - bb), Is.LessThan(near), key + " blue");
                double spread = Math.Sqrt(Math.Max(0, luma2 / count - (luma / count) * (luma / count)));
                if (key == "asphalt" || key == "plaster" || key == "grass" || key == "concrete") Assert.That(spread, Is.LessThan(8), key + " stays even");
                VoxelSurfaces.Color(key, 5, 7, out float r0, out float g0, out float b0);
                VoxelSurfaces.Color(key, 5 + n, 7 - 2 * n, out float r1, out float g1, out float b1);
                Assert.That(r1, Is.EqualTo(r0)); Assert.That(g1, Is.EqualTo(g0)); Assert.That(b1, Is.EqualTo(b0));
            }
            // The distant facades still show lit windows.
            byte[] windows = VoxelSurfaces.Rgba("windows"); VoxelPalette.Rgb(VoxelPalette.WindowLit, out byte lr, out byte lg, out byte lb);
            int lit = 0;
            for (int i = 0; i < windows.Length; i += 4) if (Math.Abs(windows[i] - lr) < 24 && Math.Abs(windows[i + 1] - lg) < 24 && Math.Abs(windows[i + 2] - lb) < 24) lit++;
            Assert.That(lit, Is.GreaterThan(200), "lit windows");
            Assert.That(VoxelSurfaces.Has("marble"), Is.False);
            Assert.Throws<ArgumentException>(() => VoxelSurfaces.Rgba("marble"));
            Assert.Throws<ArgumentException>(() => VoxelSurfaces.BaseColor("marble"));
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
            var blocks = new List<CityRect>(); foreach (var s in solid) if (!s.Overhead) blocks.Add(s.Footprint);
            blocks.Add(CityScenery.MapStand); blocks.Add(CityScenery.CompanyStand); blocks.Add(CityScenery.Elevator);
            // The player's capsule (radius 0.3) must fit between every two props on the way to each destination.
            bool Free(float x, float z)
            {
                if (!CityDistrictPlan.CanWalk(x, z)) return false;
                foreach (var b in blocks) if (b.Distance(x, z) < 0.32) return false;
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
                Assert.That(Math.Abs(block.X) - block.Width / 2 > CityScenery.EmbankmentEndX || block.Z + block.Depth / 2 < CityScenery.EmbankmentMinZ - 1
                    || block.Z - block.Depth / 2 > CityScenery.EmbankmentMaxZ + 1, Is.True, "skyline leaves the tunnel embankments free");
                foreach (var corner in new[] { (-44f, -12f), (44f, -12f), (-44f, 28f), (44f, 28f), (-44f, 76f), (44f, 76f) })
                {
                    double dx = Math.Abs(block.X - corner.Item1) + block.Width / 2, dz = Math.Abs(block.Z - corner.Item2) + block.Depth / 2;
                    Assert.That(Math.Sqrt(dx * dx + dz * dz), Is.LessThan(CityScenery.FarClip - 5));
                }
                Assert.That(Math.Abs(block.X) + block.Width / 2, Is.LessThan(CityScenery.TerrainSize / 2));
                Assert.That(Math.Abs(block.Z - CityScenery.TerrainCenterZ) + block.Depth / 2, Is.LessThan(CityScenery.TerrainSize / 2));
            }
        }
        [Test] public void TheHazeTurnsTheSkylineIntoSilhouettesBeforeTheFarClip()
        {
            Assert.That(CityScenery.FogStart, Is.GreaterThan(0).And.LessThan(CityScenery.FogEnd));
            Assert.That(CityScenery.FogEnd, Is.LessThan(CityScenery.FarClip), "geometry is fully hazed before it is clipped");
            int full = 0;
            foreach (var block in CityScenery.Skyline)
            {
                // Linear fog at the point of the footprint nearest to the spawn point.
                double dx = Math.Max(0, Math.Abs(block.X - CityBlockPlan.SpawnX) - block.Width / 2), dz = Math.Max(0, Math.Abs(block.Z - CityBlockPlan.SpawnZ) - block.Depth / 2);
                double fog = (Math.Sqrt(dx * dx + dz * dz) - CityScenery.FogStart) / (CityScenery.FogEnd - CityScenery.FogStart);
                Assert.That(fog, Is.GreaterThan(0.25), "skyline block at " + block.X + ", " + block.Z + " stands too clear of the haze");
                if (fog >= 1) full++;
            }
            Assert.That(full, Is.GreaterThan(0), "the farthest ring melts into the haze completely");
        }
    }
}
