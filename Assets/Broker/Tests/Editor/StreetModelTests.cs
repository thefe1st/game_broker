using System;
using System.Collections.Generic;
using System.Linq;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    // The low-poly trees, conifers, shrubs, planters, lawns, street lamps, benches, bins, signals, bollards, cones, works,
    // refuse containers and information stands that replace the voxel models: sound meshes inside the voxel models' boxes,
    // crowns over the walkers' heads, seats, kerbs, soil, the lamp glass, the signals' clamps and lit lenses and the stands'
    // label band where the scene expects them, and the whole street cheaper than the voxels it replaces.
    public sealed class StreetModelTests
    {
        private const int Wall = (int)BuildingPart.Wall, Trim = (int)BuildingPart.Trim, Glass = (int)BuildingPart.Glass, Night = (int)BuildingPart.Night,
            Glow = (int)BuildingPart.Glow, Metal = (int)BuildingPart.Metal;
        private static readonly int[] All = { Wall, Trim, Glass, Night, Glow, Metal };
        // The raised beds of the western square plant these seeds.
        private static readonly int[] ParkSeeds = { 21, 22 };
        private static readonly byte[] FlowerColours = { VoxelPalette.FlowerPink, VoxelPalette.FlowerYellow, VoxelPalette.FlowerWhite, VoxelPalette.CarRed };
        private static readonly (float length, bool back)[] Benches = { (2.4f, true), (3, false), (1.2f, true), (5, false) };
        // The models with a glowing part: the lamp glass, the lit lenses, the «wait» lamp, the works lamp and the marker.
        private static readonly string[] Lit = { "street lamp", "car signal 0", "car signal 1", "car signal 2", "walk signal 0", "walk signal 1",
            "waiting button", "works barrier", "map stand" };
        // The car head's lenses from the top, lit and dark; the walk head's figures, standing over walking.
        private static readonly byte[] LensLit = { VoxelPalette.SignalRed, VoxelPalette.SignalAmber, VoxelPalette.SignalGreen };
        private static readonly byte[] LensDark = { VoxelPalette.BrickDark, VoxelPalette.WoodDark, VoxelPalette.PineDark };
        private static readonly byte[] FigureLit = { VoxelPalette.SignalRed, VoxelPalette.SignalGreen }, FigureDark = { VoxelPalette.BrickDark, VoxelPalette.PineDark };
        private static readonly StandStyle[] Stands = { StandStyle.Map, StandStyle.Companies };

        private static IEnumerable<(string name, PolyMesh mesh, bool furniture)> Models()
        {
            for (int k = 0; k < StreetModels.TreeKinds; k++) yield return ("tree " + k, StreetModels.Tree(k), false);
            foreach (int seed in ParkSeeds) yield return ("park tree " + seed, StreetModels.Tree(seed), false);
            for (int k = 0; k < StreetModels.ConiferKinds; k++) yield return ("conifer " + k, StreetModels.Conifer(k), false);
            yield return ("shrub", StreetModels.Shrub(2, 1, 1.25f, 21, true), false);
            yield return ("bare shrub", StreetModels.Shrub(1.5f, 0.8f, 1, 3, false), false);
            for (int k = 0; k < 3; k++) yield return ("planter " + k, StreetModels.Planter(k), false);
            for (int k = 0; k < 2; k++) yield return ("lawn " + k, StreetModels.Lawn(k), false);
            yield return ("street lamp", StreetModels.StreetLamp(), true);
            foreach (var (length, back) in Benches) yield return ((back ? "bench " : "plaza bench ") + length, StreetModels.Bench(length, back), true);
            yield return ("bin", StreetModels.Bin(), true);
            yield return ("signal post", StreetModels.SignalPost(), true);
            for (int lit = -1; lit <= 2; lit++) yield return ("car signal " + lit, StreetModels.CarSignal(lit), true);
            for (int lit = -1; lit <= 1; lit++) yield return ("walk signal " + lit, StreetModels.WalkSignal(lit), true);
            foreach (bool waiting in new[] { false, true }) yield return ((waiting ? "waiting" : "idle") + " button", StreetModels.CrossingButton(waiting), true);
            yield return ("bollard", StreetModels.Bollard(), true);
            yield return ("cone", StreetModels.Cone(), true);
            yield return ("works barrier", StreetModels.WorksBarrier(), true);
            yield return ("refuse container", StreetModels.Dumpster(), true);
            foreach (var style in Stands) yield return (StandName(style), StreetModels.InfoStand(style), true);
        }
        private static string StandName(StandStyle style) => style == StandStyle.Map ? "map stand" : "company stand";

        private struct Tri { public V3 A, B, C; public int Part, Vertex; }
        private static List<Tri> Tris(PolyMesh mesh, params int[] parts)
        {
            var list = new List<Tri>();
            foreach (int part in parts.Length > 0 ? parts : All)
            {
                var t = mesh.Triangles[part];
                for (int i = 0; i < t.Length; i += 3)
                    list.Add(new Tri { A = mesh.Position(t[i]), B = mesh.Position(t[i + 1]), C = mesh.Position(t[i + 2]), Part = part, Vertex = t[i] });
            }
            return list;
        }
        private static (V3 min, V3 max) Bounds(IEnumerable<Tri> tris)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue, z1 = float.MinValue;
            foreach (var t in tris)
                foreach (var p in new[] { t.A, t.B, t.C })
                {
                    x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); z0 = Math.Min(z0, p.Z); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); z1 = Math.Max(z1, p.Z);
                }
            return (new V3(x0, y0, z0), new V3(x1, y1, z1));
        }
        private static (V3 min, V3 max) Bounds(PolyMesh mesh, params int[] parts) => Bounds(Tris(mesh, parts));
        // Inside the box and standing on its floor (y = 0).
        private static void AssertInside(PolyMesh mesh, V3 min, V3 max, string name)
        {
            const float e = 1e-3f;
            var (lo, hi) = Bounds(mesh);
            Assert.That(lo.X, Is.GreaterThanOrEqualTo(min.X - e), name + " left"); Assert.That(hi.X, Is.LessThanOrEqualTo(max.X + e), name + " right");
            Assert.That(lo.Z, Is.GreaterThanOrEqualTo(min.Z - e), name + " front"); Assert.That(hi.Z, Is.LessThanOrEqualTo(max.Z + e), name + " back");
            Assert.That(hi.Y, Is.LessThanOrEqualTo(max.Y + e), name + " top"); Assert.That(lo.Y, Is.EqualTo(0).Within(e), name + " stands on its pivot");
        }
        private static bool IsColour(PolyMesh mesh, int vertex, byte colour)
            => Math.Abs(mesh.Uvs[vertex * 2] - VoxelPalette.U(colour)) < 1e-5f && Math.Abs(mesh.Uvs[vertex * 2 + 1] - VoxelPalette.V(colour)) < 1e-5f;
        private static bool IsFlower(PolyMesh mesh, Tri tri) => tri.Part == Trim && FlowerColours.Any(c => IsColour(mesh, tri.Vertex, c));
        // Möller–Trumbore: the distance along a unit ray to the triangle, if it meets it.
        private static bool Meets(Tri tri, V3 origin, V3 dir, out float distance)
        {
            distance = 0;
            V3 e1 = tri.B - tri.A, e2 = tri.C - tri.A, p = V3.Cross(dir, e2);
            float det = V3.Dot(e1, p);
            if (Math.Abs(det) < 1e-9f) return false;
            float inv = 1 / det; V3 s = origin - tri.A;
            float a = V3.Dot(s, p) * inv; if (a < 0 || a > 1) return false;
            V3 q = V3.Cross(s, e1);
            float c = V3.Dot(dir, q) * inv; if (c < 0 || a + c > 1) return false;
            distance = V3.Dot(e2, q) * inv; return distance > 1e-5f;
        }
        private static Tri Hit(List<Tri> tris, V3 origin, V3 dir, out float distance, string name)
        {
            Tri hit = default; distance = float.MaxValue; bool any = false;
            foreach (var t in tris)
                if (Meets(t, origin, dir, out float d) && d < distance) { hit = t; distance = d; any = true; }
            Assert.That(any, Is.True, name + ": the ray meets the model");
            return hit;
        }
        private static float Down(List<Tri> tris, float x, float z, string name) { Hit(tris, new V3(x, 50, z), new V3(0, -1, 0), out float d, name); return 50 - d; }
        // The colour of the first surface a ray meets.
        private static bool Sees(PolyMesh mesh, List<Tri> tris, V3 origin, V3 dir, byte colour, string name) => IsColour(mesh, Hit(tris, origin, dir, out _, name).Vertex, colour);
        // Distance from the vertical axis of the first surface met by a ray from 2 m out along dir (a hair off the axis line).
        private static float Reach(List<Tri> tris, float y, V3 dir, string name)
        {
            var side = new V3(dir.Z, 0, -dir.X) * 0.004f;
            Hit(tris, dir * 2 + side + new V3(0, y, 0), dir * -1, out float d, name);
            return 2 - d;
        }
        private static void AssertSame(PolyMesh a, PolyMesh b, string name)
        {
            Assert.That(a.Positions, Is.EqualTo(b.Positions), name); Assert.That(a.Normals, Is.EqualTo(b.Normals), name);
            Assert.That(a.Uvs, Is.EqualTo(b.Uvs), name); Assert.That(a.Shades, Is.EqualTo(b.Shades), name);
            for (int p = 0; p < BuildingModels.PartCount; p++) Assert.That(a.Triangles[p], Is.EqualTo(b.Triangles[p]), name + " part " + p);
        }
        private static int VoxelTriangles(VoxelModel model) { var mesh = VoxelMesher.Build(model); return (mesh.Triangles.Length + mesh.GlowTriangles.Length) / 3; }

        [Test] public void MeshesAreWellFormedAndDeterministic()
        {
            var again = Models().ToList();
            int index = 0;
            foreach (var (name, mesh, furniture) in Models())
            {
                int n = mesh.VertexCount;
                Assert.That(n, Is.GreaterThan(0).And.LessThan(65000), name + ": 16-bit indices");
                Assert.That(mesh.Normals.Length, Is.EqualTo(n * 3), name); Assert.That(mesh.Uvs.Length, Is.EqualTo(n * 2), name);
                Assert.That(mesh.Shades.Length, Is.EqualTo(n), name); Assert.That(mesh.Triangles.Length, Is.EqualTo(BuildingModels.PartCount), name);
                int badNormal = 0, dark = 0, outside = 0, degenerate = 0, flipped = 0;
                for (int i = 0; i < n; i++)
                {
                    if (Math.Abs(mesh.Normal(i).Length - 1) > 1e-3f) badNormal++;
                    if (mesh.Shades[i] <= 100) dark++;
                }
                foreach (var tris in mesh.Triangles)
                {
                    Assert.That(tris.Length % 3, Is.Zero, name);
                    for (int t = 0; t < tris.Length; t += 3)
                    {
                        if (tris[t] < 0 || tris[t] >= n || tris[t + 1] < 0 || tris[t + 1] >= n || tris[t + 2] < 0 || tris[t + 2] >= n) { outside++; continue; }
                        V3 a = mesh.Position(tris[t]), b = mesh.Position(tris[t + 1]), c = mesh.Position(tris[t + 2]);
                        var face = V3.Cross(b - a, c - a);
                        if (face.Length <= 0) { degenerate++; continue; }
                        for (int k = 0; k < 3; k++) if (V3.Dot(face, mesh.Normal(tris[t + k])) <= 0) { flipped++; break; }
                    }
                }
                Assert.That(badNormal, Is.Zero, name + ": unit normals"); Assert.That(dark, Is.Zero, name + ": the shade keeps some light");
                Assert.That(outside, Is.Zero, name + ": indices in range"); Assert.That(degenerate, Is.Zero, name + ": no degenerate triangles");
                Assert.That(flipped, Is.Zero, name + ": normals on the outer side of their triangles");
                // Greenery shows its leaves or grass; the furniture is palette trim only, its look never shows.
                if (furniture) Assert.That(mesh.Triangles[Wall], Is.Empty, name + ": no wall material");
                else Assert.That(mesh.Triangles[Wall].Length, Is.GreaterThan(0), name + " shows its leaves or grass");
                Assert.That(mesh.Triangles[Glass].Length + mesh.Triangles[Night].Length + mesh.Triangles[Metal].Length, Is.Zero, name + ": no glass, night windows or brass");
                if (Lit.Contains(name)) Assert.That(mesh.Triangles[Glow].Length, Is.GreaterThan(0), name + " glows");
                else Assert.That(mesh.Triangles[Glow], Is.Empty, name + ": nothing glows");
                AssertSame(mesh, again[index++].mesh, name + " is deterministic");
            }
        }

        [Test] public void TreesStandInsideTheVoxelTreesWithCrownsOverTheWalkers()
        {
            foreach (int seed in Enumerable.Range(0, StreetModels.TreeKinds).Concat(ParkSeeds))
            {
                var tree = StreetModels.Tree(seed); string name = "tree " + seed;
                AssertInside(tree, new V3(-2, 0, -2), new V3(2, 6.5f, 2), name);
                var (crownMin, crownMax) = Bounds(tree, Wall);
                Assert.That(crownMin.Y, Is.GreaterThanOrEqualTo(StreetModels.CrownBase), name + ": walkers pass under the crown");
                Assert.That(crownMax.Y, Is.GreaterThan(5), name + ": as tall as the voxel tree");
                Assert.That(crownMax.X - crownMin.X, Is.GreaterThan(2.6f), name + ": a full crown"); Assert.That(crownMax.Z - crownMin.Z, Is.GreaterThan(2.6f), name + ": a full crown");
                Assert.That(Bounds(tree, Trim).max.Y, Is.GreaterThan(crownMin.Y + 0.5f), name + ": the trunk and the limbs reach into the crown");
                var tris = Tris(tree);
                // A bark trunk at the pivot, leaves over it.
                var trunk = Hit(tris, new V3(-5, 1.3f, 0), new V3(1, 0, 0), out float d, name);
                Assert.That(5 - d, Is.InRange(0.08f, 0.2f), name + ": trunk radius");
                Assert.That(trunk.Part == Trim && IsColour(tree, trunk.Vertex, VoxelPalette.Bark), name + ": bark");
                Assert.That(Hit(tris, new V3(0, 50, 0), new V3(0, -1, 0), out _, name).Part, Is.EqualTo(Wall), name + ": seen from above, leaves");
            }
            // Each kind a shape and a green of its own on the one leaves surface.
            var kinds = Enumerable.Range(0, StreetModels.TreeKinds).Select(StreetModels.Tree).ToList();
            for (int i = 0; i < kinds.Count; i++)
                for (int j = i + 1; j < kinds.Count; j++) Assert.That(kinds[i].Positions, Is.Not.EqualTo(kinds[j].Positions), "trees " + i + " and " + j);
            var looks = Enumerable.Range(0, StreetModels.TreeKinds).Select(StreetModels.TreeLook).ToList();
            Assert.That(looks.Select(l => l.Surface).Distinct(), Is.EqualTo(new[] { "leaves" }));
            Assert.That(looks.Select(l => l.Tint).Distinct().Count(), Is.EqualTo(StreetModels.TreeKinds));
            Assert.That(StreetModels.TreeLook(-3), Is.EqualTo(StreetModels.TreeLook(2)), "negative seeds wrap");
            Assert.That(StreetModels.TreeLook(StreetModels.TreeKinds + 2), Is.EqualTo(StreetModels.TreeLook(2)));
        }

        [Test] public void ConifersStandInsideTheVoxelConifersAndNarrowUpward()
        {
            for (int k = 0; k < StreetModels.ConiferKinds; k++)
            {
                var tree = StreetModels.Conifer(k); string name = "conifer " + k;
                AssertInside(tree, new V3(-1.5f, 0, -1.5f), new V3(1.5f, 7.5f, 1.5f), name);
                var (leafMin, leafMax) = Bounds(tree, Wall);
                Assert.That(leafMin.Y, Is.InRange(0.8f, 1.3f), name + ": a bare foot of trunk under the lowest tier");
                Assert.That(leafMax.Y, Is.GreaterThan(7), name + ": a point near the top of the voxel conifer");
                float Reach(Func<float, bool> band) => Tris(tree, Wall).SelectMany(t => new[] { t.A, t.B, t.C }).Where(p => band(p.Y))
                    .Max(p => (float)Math.Sqrt(p.X * p.X + p.Z * p.Z));
                Assert.That(Reach(y => y < 2), Is.GreaterThan(1.2f), name + ": a wide lowest tier");
                Assert.That(Reach(y => y > 6), Is.LessThan(0.6f), name + ": a narrow top");
                var trunk = Hit(Tris(tree), new V3(-5, 0.5f, 0), new V3(1, 0, 0), out float d, name);
                Assert.That(5 - d, Is.InRange(0.08f, 0.17f), name + ": trunk radius");
                Assert.That(IsColour(tree, trunk.Vertex, VoxelPalette.BarkDark), name + ": dark bark");
            }
            // A darker, cooler green than any broadleaf tree.
            var look = StreetModels.ConiferLook;
            Assert.That(look.Surface, Is.EqualTo("leaves"));
            for (int k = 0; k < StreetModels.TreeKinds; k++)
            {
                uint tree = StreetModels.TreeLook(k).Tint;
                Assert.That(look.Tint >> 16, Is.LessThan(tree >> 16), "red under tree " + k); Assert.That(look.Tint >> 8 & 0xFF, Is.LessThan(tree >> 8 & 0xFF), "green under tree " + k);
            }
            Assert.That((look.Tint & 0xFF) - (look.Tint >> 16), Is.GreaterThan(60), "cool");
        }

        [Test] public void ShrubsFillTheirBoxesAndFlowerWhenAsked()
        {
            foreach (int seed in ParkSeeds)
            {
                var shrub = StreetModels.Shrub(2, 1, 1.25f, seed, true); string name = "shrub " + seed;
                AssertInside(shrub, new V3(-1, 0, -0.625f), new V3(1, 1, 0.625f), name);
                var (min, max) = Bounds(shrub, Wall);
                Assert.That(max.X - min.X, Is.GreaterThan(1.6f), name + " width"); Assert.That(max.Z - min.Z, Is.GreaterThan(0.9f), name + " depth");
                Assert.That(max.Y, Is.GreaterThan(0.8f), name + " height");
                Assert.That(Down(Tris(shrub), 0, 0, name), Is.GreaterThan(0.8f), name + ": domed over its middle");
                // Flowers: small trim heads in pink, yellow and white over the leaves, none under them.
                var flowers = Tris(shrub, Trim);
                Assert.That(flowers.Count, Is.GreaterThan(0), name + " flowers");
                Assert.That(flowers.All(t => IsFlower(shrub, t)), name + ": flower colours");
                Assert.That(FlowerColours.Count(c => flowers.Any(t => IsColour(shrub, t.Vertex, c))), Is.GreaterThanOrEqualTo(2), name + ": mixed flowers");
                Assert.That(Bounds(flowers).min.Y, Is.GreaterThan(0.1f), name + ": flowers on the leaves");
            }
            var bare = StreetModels.Shrub(2, 1, 1.25f, 21, false);
            Assert.That(bare.Triangles[Trim], Is.Empty, "no flowers unless asked");
            Assert.That(bare.TriangleCount, Is.LessThan(StreetModels.Shrub(2, 1, 1.25f, 21, true).TriangleCount));
            Assert.That(StreetModels.Shrub(2, 1, 1.25f, 21, false).Positions, Is.Not.EqualTo(StreetModels.Shrub(2, 1, 1.25f, 22, false).Positions), "seeded bumps");
            Assert.That(StreetModels.ShrubLook.Surface, Is.EqualTo("leaves"));
            foreach (var (w, h, d) in new[] { (0.2f, 1f, 1f), (2f, 0.1f, 1f), (2f, 1f, 0.2f), (float.NaN, 1f, 1f), (2f, float.PositiveInfinity, 1f), (7f, 1f, 1f), (2f, 1f, 7f) })
                Assert.Throws<ArgumentOutOfRangeException>(() => StreetModels.Shrub(w, h, d, 0, true), w + " × " + h + " × " + d);
        }

        [Test] public void PlantersHoldAFloweringShrubOverTheirSoil()
        {
            float hx = StreetModels.PlanterWidth / 2, hz = StreetModels.PlanterDepth / 2;
            for (int k = 0; k < 3; k++)
            {
                var planter = StreetModels.Planter(k); string name = "planter " + k;
                AssertInside(planter, new V3(-hx, 0, -hz), new V3(hx, 1.625f, hz), name);
                var tris = Tris(planter);
                foreach (var (x, z) in new[] { (0f, -hz + 0.05f), (0f, hz - 0.05f), (-hx + 0.05f, 0f), (hx - 0.05f, 0f) })
                    Assert.That(Down(tris, x, z, name), Is.EqualTo(StreetModels.PlanterTop).Within(1e-3f), name + ": the capping all round");
                var soil = Hit(tris, new V3(hx - 0.13f, 50, hz - 0.13f), new V3(0, -1, 0), out float d, name);
                Assert.That(50 - d, Is.EqualTo(StreetModels.PlanterSoil).Within(1e-3f), name + ": soil in the corner the shrub leaves");
                Assert.That(IsColour(planter, soil.Vertex, VoxelPalette.Soil), name + ": soil colour");
                Assert.That(Down(tris, 0, 0, name), Is.GreaterThan(StreetModels.PlanterSoil + 0.7f), name + ": the shrub over the middle");
                Assert.That(tris.Count(t => IsFlower(planter, t)), Is.GreaterThan(0), name + " flowers");
                Assert.That(Bounds(planter, Wall).min.Y, Is.EqualTo(StreetModels.PlanterSoil).Within(1e-3f), name + ": the shrub grows from the soil");
            }
        }

        [Test] public void LawnsKeepTheirKerbAndGrassHeightsUnderLowFlowers()
        {
            float hx = StreetModels.LawnWidth / 2, hz = StreetModels.LawnDepth / 2;
            for (int k = 0; k < 2; k++)
            {
                var lawn = StreetModels.Lawn(k); string name = "lawn " + k;
                AssertInside(lawn, new V3(-hx, 0, -hz), new V3(hx, 0.5f, hz), name);
                var tris = Tris(lawn);
                foreach (var (x, z) in new[] { (0f, -hz + 0.06f), (0f, hz - 0.06f), (-hx + 0.06f, 0f), (hx - 0.06f, 0f), (hx - 0.06f, hz - 0.06f) })
                    Assert.That(Down(tris, x, z, name), Is.EqualTo(StreetModels.LawnKerb).Within(1e-3f), name + ": the kerb all round");
                var grass = Tris(lawn, Wall);
                Assert.That(grass.All(t => Math.Abs(t.A.Y - StreetModels.LawnGrass) < 1e-4f && Math.Abs(t.B.Y - StreetModels.LawnGrass) < 1e-4f
                    && Math.Abs(t.C.Y - StreetModels.LawnGrass) < 1e-4f), name + ": mown flat");
                var (grassMin, grassMax) = Bounds(grass);
                Assert.That(grassMin.X, Is.LessThan(-hx + 0.2f)); Assert.That(grassMax.X, Is.GreaterThan(hx - 0.2f));
                Assert.That(grassMin.Z, Is.LessThan(-hz + 0.2f)); Assert.That(grassMax.Z, Is.GreaterThan(hz - 0.2f));
                // Clumps of flowers stand on the grass, low enough to look over, in at least three colours.
                var flowers = tris.Where(t => IsFlower(lawn, t)).ToList();
                var (low, high) = Bounds(flowers);
                Assert.That(low.Y, Is.GreaterThan(StreetModels.LawnGrass), name + ": flowers over the grass");
                Assert.That(high.Y, Is.InRange(StreetModels.LawnKerb, 0.5f), name + ": flowers just over the kerb");
                Assert.That(FlowerColours.Count(c => flowers.Any(t => IsColour(lawn, t.Vertex, c))), Is.GreaterThanOrEqualTo(3), name + ": mixed clumps");
                Assert.That(Bounds(lawn, Trim).min.Y, Is.EqualTo(0).Within(1e-3f), name + ": the kerb stands on the pavement");
            }
            Assert.That(StreetModels.Lawn(0).Positions, Is.Not.EqualTo(StreetModels.Lawn(1).Positions), "two lawns, two patterns");
            Assert.That(StreetModels.LawnLook.Surface, Is.EqualTo("grass"));
        }

        [Test] public void TheStreetLampHangsItsGlassUnderTheHead()
        {
            var lamp = StreetModels.StreetLamp(); const string name = "street lamp";
            AssertInside(lamp, new V3(-0.25f, 0, -0.25f), new V3(0.25f, StreetModels.LampHeight, StreetModels.LampReach), name);
            var glow = Tris(lamp, Glow);
            Assert.That(glow.Count, Is.EqualTo(2), "one pane of glass");
            foreach (var t in glow)
            {
                Assert.That(new[] { t.A.Y, t.B.Y, t.C.Y }, Has.All.EqualTo(StreetModels.LampGlassY).Within(1e-4f), "a level pane");
                Assert.That(lamp.Normal(t.Vertex).Y, Is.LessThan(-0.999f), "facing the pavement");
                Assert.That(IsColour(lamp, t.Vertex, VoxelPalette.LampWarm), "warm light");
            }
            var (min, max) = Bounds(glow);
            Assert.That(min.Z, Is.GreaterThan(0.6f), "over the reach"); Assert.That(max.Z, Is.LessThan(StreetModels.LampReach));
            var tris = Tris(lamp); float cz = (min.Z + max.Z) / 2;
            Assert.That(Hit(tris, new V3(0, 1, cz), new V3(0, 1, 0), out float d, name).Part, Is.EqualTo(Glow), "looking up from under the head, the glass");
            Assert.That(d, Is.EqualTo(StreetModels.LampGlassY - 1).Within(1e-3f));
            Assert.That(Down(tris, 0, cz, name), Is.GreaterThan(StreetModels.LampGlassY + 0.2f), "the head over the glass");
            // A slim pole on a wider foot.
            Hit(tris, new V3(-2, 2.5f, 0), new V3(1, 0, 0), out d, name); Assert.That(2 - d, Is.InRange(0.04f, 0.09f), "pole radius");
            Hit(tris, new V3(-2, 0.03f, 0), new V3(1, 0, 0), out d, name); Assert.That(2 - d, Is.InRange(0.17f, 0.21f), "foot radius");
        }

        [Test] public void BenchesSeatFlatAlongTheirLengthOnIronFrames()
        {
            foreach (var (length, back) in Benches)
            {
                var bench = StreetModels.Bench(length, back); string name = (back ? "bench " : "plaza bench ") + length;
                float half = length / 2, front = back ? -0.35f : -0.3f;
                AssertInside(bench, new V3(-half, 0, front), new V3(half, back ? 0.9f : 0.5f, -front), name);
                var tris = Tris(bench);
                // Four wooden slats over the half-metre seat, flat from end to end.
                foreach (float x in new[] { -half + 0.05f, -half / 2, 0, half / 2, half - 0.05f })
                    for (int i = 0; i < 4; i++)
                    {
                        var slat = Hit(tris, new V3(x, 50, front + 0.0625f + 0.125f * i), new V3(0, -1, 0), out float d, name);
                        Assert.That(50 - d, Is.EqualTo(StreetModels.SeatHeight).Within(1e-3f), name + ": seat at " + x + ", slat " + i);
                        Assert.That(IsColour(bench, slat.Vertex, VoxelPalette.Wood), name + ": wooden slats");
                    }
                if (back)
                {
                    // Back slats behind the seat and iron armrests over the end frames.
                    var rest = Hit(tris, new V3(0, 0.66f, -5), new V3(0, 0, 1), out float d, name);
                    Assert.That(d - 5, Is.InRange(0.05f, 0.35f), name + ": the back behind the seat"); Assert.That(IsColour(bench, rest.Vertex, VoxelPalette.Wood), name + ": a wooden back");
                    var arm = Hit(tris, new V3(half - 0.1f, 50, -0.1f), new V3(0, -1, 0), out d, name);
                    Assert.That(50 - d, Is.InRange(StreetModels.SeatHeight + 0.1f, 0.75f), name + ": armrest"); Assert.That(IsColour(bench, arm.Vertex, VoxelPalette.MetalDark), name + ": iron");
                }
                else Assert.That(Bounds(bench).max.Y, Is.EqualTo(StreetModels.SeatHeight).Within(1e-3f), name + ": nothing over the seat");
                // Long benches stand on a middle frame as well.
                Hit(tris, new V3(0, -1, -0.05f), new V3(0, 1, 0), out float under, name);
                if (length > 2.6f) Assert.That(under - 1, Is.LessThan(0.2f), name + ": a middle frame");
                else Assert.That(under - 1, Is.EqualTo(StreetModels.SeatHeight - 0.045f).Within(1e-3f), name + ": no middle frame, the seat from below");
            }
            foreach (float bad in new[] { 0.7f, 0, -1, float.NaN, float.PositiveInfinity, 6.5f })
                Assert.Throws<ArgumentOutOfRangeException>(() => StreetModels.Bench(bad, true), bad.ToString());
        }

        [Test] public void TheBinIsOpenUnderItsRim()
        {
            var bin = StreetModels.Bin(); float r = StreetModels.BinRadius; const string name = "bin";
            AssertInside(bin, new V3(-r, 0, -r), new V3(r, 0.9f, r), name);
            Assert.That(Bounds(bin).max.Y, Is.EqualTo(StreetModels.BinHeight).Within(1e-3f), "the rim");
            var tris = Tris(bin);
            var opening = Hit(tris, new V3(0, 50, 0), new V3(0, -1, 0), out float d, name);
            Assert.That(50 - d, Is.InRange(0.75f, StreetModels.BinHeight - 0.02f), "the opening below the rim");
            Assert.That(IsColour(bin, opening.Vertex, VoxelPalette.Ink), "dark inside");
            Assert.That(IsColour(bin, Hit(tris, new V3(-2, 0.43f, 0), new V3(1, 0, 0), out _, name).Vertex, VoxelPalette.Metal), "a pale band");
            Assert.That(IsColour(bin, Hit(tris, new V3(-2, 0.2f, 0), new V3(1, 0, 0), out _, name).Vertex, VoxelPalette.MetalDark), "a dark drum");
        }

        [Test] public void SignalHeadsHangOnTheClampsOfTheirPost()
        {
            var post = StreetModels.SignalPost(); var pole = Tris(post); const string name = "signal post";
            AssertInside(post, new V3(-0.3f, 0, -0.3f), new V3(0.3f, StreetModels.SignalHeight, 0.3f), name);
            Assert.That(Bounds(post).max.Y, Is.EqualTo(StreetModels.SignalHeight).Within(1e-3f), "the cap");
            float band = (StreetModels.SignalBandBottom + StreetModels.SignalBandTop) / 2;
            Assert.That(Sees(post, pole, new V3(-2, band, 0.004f), new V3(1, 0, 0), VoxelPalette.CarYellow, name), "a yellow band at the walkers' eyes");
            Assert.That(Sees(post, pole, new V3(-2, StreetModels.SignalBandBottom - 0.2f, 0.004f), new V3(1, 0, 0), VoxelPalette.MetalDark, name), "a dark pole");
            // BuildSignals hangs the heads and the button SignalClamp + their depth from the pole's axis, on either side
            // along X or Z: their backs end there, and every bracket's end meets a clamp reaching SignalClamp out.
            var sides = new[] { new V3(1, 0, 0), new V3(-1, 0, 0), new V3(0, 0, 1), new V3(0, 0, -1) };
            var held = new List<(float bottom, float top)>();
            foreach (var (head, mesh, pivot, back) in new[]
            {
                ("car signal", StreetModels.CarSignal(-1), StreetModels.CarSignalY, StreetModels.CarSignalBack),
                ("walk signal", StreetModels.WalkSignal(-1), StreetModels.WalkSignalY, StreetModels.WalkSignalBack),
                ("button", StreetModels.CrossingButton(false), StreetModels.ButtonY, StreetModels.ButtonBack),
            })
            {
                Assert.That(Bounds(mesh).max.Z, Is.EqualTo(back).Within(1e-4f), head + ": its back");
                var ends = Tris(mesh).Where(t => Math.Abs(t.A.Z - back) + Math.Abs(t.B.Z - back) + Math.Abs(t.C.Z - back) < 1e-4f).ToList();
                Assert.That(ends, Is.Not.Empty, head + " touches the post");
                foreach (var end in ends)
                {
                    float bottom = pivot + Math.Min(end.A.Y, Math.Min(end.B.Y, end.C.Y)), top = pivot + Math.Max(end.A.Y, Math.Max(end.B.Y, end.C.Y));
                    held.Add((bottom, top));
                    bool clamped = false;
                    for (float y = bottom + 0.0025f; y < top && !clamped; y += 0.005f)
                        clamped = sides.All(dir => Reach(pole, y, dir, name) >= StreetModels.SignalClamp - 0.003f);
                    Assert.That(clamped, head + ": a clamp behind its end at " + bottom + "–" + top);
                }
            }
            // And no clamp holds nothing.
            for (float y = 0.4025f; y < StreetModels.SignalHeight - 0.15f; y += 0.005f)
                if (Reach(pole, y, sides[1], name) > 0.085f)
                    Assert.That(held.Any(h => y >= h.bottom - 1e-3f && y <= h.top + 1e-3f), "a clamp at " + y + " behind a head or the button");
        }

        [Test] public void EachSignalLightsOneLampAndKeepsTheOthersDark()
        {
            var ahead = new V3(0, 0, 1);
            for (int lit = -1; lit <= 2; lit++)
            {
                var head = StreetModels.CarSignal(lit); var tris = Tris(head); string name = "car signal " + lit;
                AssertInside(head, new V3(-0.2f, 0, -0.2f), new V3(0.2f, 1.2f, StreetModels.CarSignalBack), name);
                for (int i = 0; i < 3; i++)
                {
                    float y = StreetModels.CarLensTop - StreetModels.CarLensStep * i;
                    var lens = Hit(tris, new V3(0.01f, y + 0.01f, -5), ahead, out _, name);
                    Assert.That(lens.Part, Is.EqualTo(lit == i ? Glow : Trim), name + ": lens " + i);
                    Assert.That(IsColour(head, lens.Vertex, lit == i ? LensLit[i] : LensDark[i]), name + ": the colour of lens " + i);
                    // Looking down in front of the lens, its dark hood.
                    var hood = Hit(tris, new V3(0, y + 0.2f, -0.15f), new V3(0, -1, 0), out float d, name);
                    Assert.That(IsColour(head, hood.Vertex, VoxelPalette.Ink) && d < 0.1f, name + ": a hood over lens " + i);
                }
                var glow = Tris(head, Glow);
                if (lit < 0) { Assert.That(glow, Is.Empty, name + ": all dark"); continue; }
                var (min, max) = Bounds(glow); float cy = StreetModels.CarLensTop - StreetModels.CarLensStep * lit, r = StreetModels.CarLensRadius + 1e-3f;
                Assert.That(min.X >= -r && max.X <= r && min.Y >= cy - r && max.Y <= cy + r, name + ": only the lit lens glows");
            }
            for (int lit = -1; lit <= 1; lit++)
            {
                var head = StreetModels.WalkSignal(lit); var tris = Tris(head); string name = "walk signal " + lit;
                AssertInside(head, new V3(-0.25f, 0, -0.15f), new V3(0.25f, 1.1f, StreetModels.WalkSignalBack), name);
                for (int i = 0; i < 2; i++)
                {
                    float y = i == 0 ? StreetModels.WalkStandY : StreetModels.WalkGoY;
                    var figure = Hit(tris, new V3(0.005f, y + 0.02f, -5), ahead, out _, name);
                    Assert.That(figure.Part, Is.EqualTo(lit == i ? Glow : Trim), name + ": figure " + i);
                    Assert.That(IsColour(head, figure.Vertex, lit == i ? FigureLit[i] : FigureDark[i]), name + ": the colour of figure " + i);
                    Assert.That(Sees(head, tris, new V3(0.13f, y - 0.15f, -5), ahead, VoxelPalette.CarBlack, name), name + ": a dark window round figure " + i);
                    var visor = Hit(tris, new V3(0, y + 0.28f, -0.1f), new V3(0, -1, 0), out float d, name);
                    Assert.That(IsColour(head, visor.Vertex, VoxelPalette.Ink) && d < 0.06f, name + ": a visor over window " + i);
                }
                var glow = Tris(head, Glow);
                if (lit < 0) { Assert.That(glow, Is.Empty, name + ": all dark"); continue; }
                float wy = lit == 0 ? StreetModels.WalkStandY : StreetModels.WalkGoY;
                var (min, max) = Bounds(glow);
                Assert.That(min.Y >= wy - 0.21f && max.Y <= wy + 0.21f, name + ": only the lit figure glows");
            }
            foreach (bool waiting in new[] { false, true })
            {
                var button = StreetModels.CrossingButton(waiting); var tris = Tris(button); string name = (waiting ? "waiting" : "idle") + " button";
                AssertInside(button, new V3(-0.125f, 0, -0.075f), new V3(0.125f, 0.4f, StreetModels.ButtonBack), name);
                Assert.That(Sees(button, tris, new V3(0.08f, 0.05f, -5), ahead, VoxelPalette.CarYellow, name), name + ": a yellow box");
                Assert.That(Sees(button, tris, new V3(0.005f, 0.145f, -5), ahead, VoxelPalette.MetalLight, name), name + ": a pale button");
                var lamp = Hit(tris, new V3(0.005f, StreetModels.ButtonLampY + 0.005f, -5), ahead, out _, name);
                Assert.That(lamp.Part, Is.EqualTo(waiting ? Glow : Trim), name + ": the «wait» lamp");
                Assert.That(IsColour(button, lamp.Vertex, waiting ? VoxelPalette.SignalAmber : VoxelPalette.WoodDark), name + ": its colour");
            }
            foreach (int bad in new[] { -2, 3 }) Assert.Throws<ArgumentOutOfRangeException>(() => StreetModels.CarSignal(bad), "car " + bad);
            foreach (int bad in new[] { -2, 2 }) Assert.Throws<ArgumentOutOfRangeException>(() => StreetModels.WalkSignal(bad), "walk " + bad);
        }

        [Test] public void BollardsAndConesKeepTheirBandsAndTheConeItsDarkHollow()
        {
            var across = new V3(1, 0, 0); var left = new V3(-1, 0, 0);
            var bollard = StreetModels.Bollard(); var tris = Tris(bollard); const string post = "bollard";
            AssertInside(bollard, new V3(-0.15f, 0, -0.15f), new V3(0.15f, StreetModels.BollardHeight, 0.15f), post);
            Assert.That(Down(tris, 0.01f, 0.01f, post), Is.EqualTo(StreetModels.BollardHeight).Within(0.01f), "the top of the cap");
            Assert.That(Sees(bollard, tris, new V3(0.01f, 50, 0.01f), new V3(0, -1, 0), VoxelPalette.Metal, post), "a pale cap");
            foreach (var (y, colour) in new[] { (0.3f, VoxelPalette.MetalDark), (0.65f, VoxelPalette.CarYellow), (0.74f, VoxelPalette.MetalDark) })
                Assert.That(Sees(bollard, tris, new V3(-2, y, 0.004f), across, colour, post), post + " at " + y);
            Assert.That(Reach(tris, 0.015f, left, post), Is.GreaterThan(Reach(tris, 0.3f, left, post) + 0.015f), "a flared foot");

            var cone = StreetModels.Cone(); tris = Tris(cone); const string name = "cone";
            AssertInside(cone, new V3(-0.25f, 0, -0.25f), new V3(0.25f, 0.8f, 0.25f), name);
            Assert.That(Bounds(cone).max.Y, Is.EqualTo(StreetModels.ConeHeight).Within(1e-3f), "the tip");
            foreach (var (y, colour) in new[] { (0.15f, VoxelPalette.Amber), (0.27f, VoxelPalette.CarWhite), (0.38f, VoxelPalette.Amber), (0.48f, VoxelPalette.CarWhite),
                (0.6f, VoxelPalette.Amber) })
                Assert.That(Sees(cone, tris, new V3(-2, y, 0.004f), across, colour, name), name + " at " + y);
            Assert.That(Reach(tris, 0.6f, left, name), Is.LessThan(Reach(tris, 0.15f, left, name) - 0.05f), "narrowing upward");
            Assert.That(Sees(cone, tris, new V3(0.2f, 50, 0.2f), new V3(0, -1, 0), VoxelPalette.Tire, name), "a black rubber foot");
            Assert.That(Down(tris, 0.2f, 0.2f, name), Is.LessThan(0.05f), "a low foot");
            // Looking into the open tip: its dark floor, not the inside of the cone.
            var hollow = Hit(tris, new V3(0.004f, 50, 0.004f), new V3(0, -1, 0), out float d, name);
            Assert.That(50 - d, Is.EqualTo(StreetModels.ConeHeight - StreetModels.ConeHollow).Within(1e-3f), "the floor of the hollow");
            Assert.That(IsColour(cone, hollow.Vertex, VoxelPalette.Ink), "dark inside the tip");
        }

        [Test] public void TheWorksBarrierStripesBothFacesOfItsBoardsAndCapsItsLamp()
        {
            var barrier = StreetModels.WorksBarrier(); var tris = Tris(barrier); const string name = "works barrier";
            AssertInside(barrier, new V3(-1.1f, 0, -0.2f), new V3(1.1f, 1.1f, 0.2f), name);
            Assert.That(StreetModels.BarrierBoards.Count, Is.EqualTo(2), "two boards");
            float w = StreetModels.StripeWidth; int behind = 0;
            foreach (var (bottom, top) in StreetModels.BarrierBoards)
            {
                // The middle of every stripe across the board, k = ⌊(x + y) / StripeWidth⌋: red when even, white when odd.
                float y = (bottom + top) / 2;
                for (int k = (int)Math.Ceiling((y - 1.05f) / w - 0.5f); (k + 0.5f) * w - y < 1.05f; k++)
                {
                    float x = (k + 0.5f) * w - y; string at = name + " at " + x + ", " + y;
                    byte colour = (k % 2 + 2) % 2 == 0 ? VoxelPalette.CarRed : VoxelPalette.CarWhite;
                    var front = Hit(tris, new V3(x, y, -5), new V3(0, 0, 1), out float d, at);
                    Assert.That(d, Is.EqualTo(5 + StreetModels.BoardFront).Within(1e-3f), at + ": the board in front");
                    Assert.That(IsColour(barrier, front.Vertex, colour), at + ": the stripe in front");
                    // From behind, beside the posts, the same stripe on the back of the board.
                    if (Math.Abs(Math.Abs(x) - 0.9f) < 0.05f) continue;
                    var back = Hit(tris, new V3(x, y, 5), new V3(0, 0, -1), out d, at);
                    Assert.That(d, Is.EqualTo(5 - StreetModels.BoardBack).Within(1e-3f), at + ": the board from behind");
                    Assert.That(IsColour(barrier, back.Vertex, colour), at + ": the stripe behind");
                    behind++;
                }
            }
            Assert.That(behind, Is.GreaterThan(12));
            // The amber lamp on the left post, closed underneath: from below beside the post, its dark cap.
            var glow = Tris(barrier, Glow); var (lo, hi) = Bounds(glow);
            Assert.That(glow.All(t => IsColour(barrier, t.Vertex, VoxelPalette.SignalAmber)), "an amber lamp");
            Assert.That(lo.Y, Is.GreaterThanOrEqualTo(StreetModels.BarrierLampY), "over the post"); Assert.That((lo.X + hi.X) / 2, Is.EqualTo(-0.9f).Within(1e-3f), "the left post");
            float from = StreetModels.BarrierLampY - 0.05f;
            var cap = Hit(tris, new V3(-0.9f, from, StreetModels.BarrierLampRadius * 0.92f), new V3(0, 1, 0), out float up, name);
            Assert.That(cap.Part, Is.EqualTo(Trim), "the lamp's glass stays out of sight from below");
            Assert.That(IsColour(barrier, cap.Vertex, VoxelPalette.Ink), "a dark cap");
            Assert.That(from + up, Is.InRange(StreetModels.BarrierLampY, StreetModels.BarrierLampY + 0.03f), "under the lamp");
        }

        [Test] public void TheRefuseContainerStandsOnCastorsUnderTwoLids()
        {
            var bin = StreetModels.Dumpster(); var tris = Tris(bin); const string name = "refuse container";
            AssertInside(bin, new V3(-0.6f, 0, -0.8f), new V3(0.6f, 1.2f, 0.8f), name);
            foreach (float x in new[] { -0.4f, 0.4f })
                foreach (float z in new[] { -0.6f, 0.6f })
                {
                    var wheel = Hit(tris, new V3(x, -1, z + 0.004f), new V3(0, 1, 0), out float d, name);
                    Assert.That(d - 1, Is.LessThan(0.01f), name + ": a castor on the pavement at " + x + ", " + z);
                    Assert.That(IsColour(bin, wheel.Vertex, VoxelPalette.Tire), name + ": rubber castors");
                }
            foreach (float s in new[] { -1f, 1f })
            {
                Assert.That(Sees(bin, tris, new V3(2 * s, 0.6f, 0.3f), new V3(-s, 0, 0), VoxelPalette.CarGreen, name), "a green body");
                var lid = Hit(tris, new V3(0, 50, 0.4f * s), new V3(0, -1, 0), out float d, name);
                Assert.That(50 - d, Is.InRange(StreetModels.DumpsterLidY + 0.05f, 1.11f), "a raised lid"); Assert.That(IsColour(bin, lid.Vertex, VoxelPalette.MetalDark), "dark lids");
            }
            Assert.That(Down(tris, 0, 0, name), Is.EqualTo(StreetModels.DumpsterLidY).Within(1e-3f), "the lids meet over the rim");
            var handle = Hit(tris, new V3(0, 0.8f, -5), new V3(0, 0, 1), out float far, name);
            Assert.That(IsColour(bin, handle.Vertex, VoxelPalette.Metal), "a handle at the front"); Assert.That(5 - far, Is.GreaterThan(0.76f), "in front of the body");
        }

        [Test] public void StandsKeepAPlainBandForTheLabelOverTheirContent()
        {
            float hw = StreetModels.StandHalfWidth, face = StreetModels.StandFace; var ahead = new V3(0, 0, 1);
            byte[] content = { VoxelPalette.StoneLight, VoxelPalette.Stone, VoxelPalette.GrassMid, VoxelPalette.GlassSky, VoxelPalette.CarWhite, VoxelPalette.SignalRed,
                VoxelPalette.GrassLight, VoxelPalette.CarRed };
            Assert.That(StreetModels.StandLabelY, Is.InRange(StreetModels.StandLabelBottom + 0.15f, StreetModels.StandLabelTop - 0.15f), "the sign inside its band");
            foreach (var style in Stands)
            {
                var stand = StreetModels.InfoStand(style); var tris = Tris(stand); string name = StandName(style);
                AssertInside(stand, new V3(-hw, 0, -StreetModels.StandHalfDepth), new V3(hw, StreetModels.StandHeight, StreetModels.StandHalfDepth), name);
                // The label band: the plain navy face from side to side, nothing in front of it.
                foreach (float x in new[] { -0.95f, -0.5f, 0, 0.5f, 0.95f })
                    foreach (float y in new[] { StreetModels.StandLabelBottom, StreetModels.StandLabelY, StreetModels.StandLabelTop })
                    {
                        var panel = Hit(tris, new V3(x, y, -5), ahead, out float d, name);
                        Assert.That(d, Is.EqualTo(5 + face).Within(1e-3f), name + ": nothing over the label band at " + x + ", " + y);
                        Assert.That(IsColour(stand, panel.Vertex, VoxelPalette.Navy), name + ": a navy panel at " + x + ", " + y);
                    }
                // The content on the face inside the frame, within its band.
                var drawn = tris.Where(t => content.Any(c => IsColour(stand, t.Vertex, c))).ToList();
                Assert.That(drawn, Is.Not.Empty, name + " shows its content");
                var (lo, hi) = Bounds(drawn);
                Assert.That(lo.Y >= StreetModels.StandContentBottom - 1e-3f && hi.Y <= StreetModels.StandContentTop + 1e-3f, name + ": the content within its band");
                Assert.That(lo.X >= -0.9f - 1e-3f && hi.X <= 0.9f + 1e-3f && hi.Z <= face + 1e-4f, name + ": on the face inside the frame");
                // A dark wooden frame in front of the face, the brass strip over the panel, a roof, stone feet, a closed back.
                Assert.That(Sees(stand, tris, new V3(hw - 0.05f, StreetModels.StandLabelY, -5), ahead, VoxelPalette.WoodDark, name), name + ": the frame");
                Assert.That(Sees(stand, tris, new V3(0.3f, (StreetModels.StandBrassBottom + StreetModels.StandPanelTop) / 2, -5), ahead, VoxelPalette.Brass, name), name + ": brass");
                Assert.That(Sees(stand, tris, new V3(0.3f, 50, 0), new V3(0, -1, 0), VoxelPalette.RoofDark, name), name + ": a roof");
                Assert.That(Down(tris, 0.3f, 0, name), Is.EqualTo(StreetModels.StandHeight).Within(0.03f), name + ": the ridge");
                foreach (float s in new[] { -1f, 1f })
                    Assert.That(Sees(stand, tris, new V3(2 * s, 0.05f, 0.08f), new V3(-s, 0, 0), VoxelPalette.StoneDark, name), name + ": stone feet");
                Assert.That(Sees(stand, tris, new V3(0.3f, StreetModels.StandLabelY, 5), new V3(0, 0, -1), VoxelPalette.Navy, name), name + ": a closed back");
            }
            // The map: the plan covers its band; the glowing marker in a white ring on the plaza's green square, water in a corner.
            var map = StreetModels.InfoStand(StandStyle.Map); var plan = Tris(map);
            for (float x = -0.85f; x < 0.86f; x += 0.17f)
                for (float y = StreetModels.StandContentBottom + 0.02f; y < StreetModels.StandContentTop; y += 0.1f)
                    Assert.That(IsColour(map, Hit(plan, new V3(x, y, -5), ahead, out _, "map").Vertex, VoxelPalette.Navy), Is.False, "the plan at " + x + ", " + y);
            float mx = StreetModels.StandMarkerX, my = StreetModels.StandMarkerY, mr = StreetModels.StandMarkerRadius;
            var marker = Hit(plan, new V3(mx + 0.005f, my + 0.005f, -5), ahead, out _, "map");
            Assert.That(marker.Part, Is.EqualTo(Glow), "the marker glows"); Assert.That(IsColour(map, marker.Vertex, VoxelPalette.SignalRed), "a red marker");
            var (gl, gh) = Bounds(map, Glow);
            Assert.That(gl.X >= mx - mr - 1e-3f && gh.X <= mx + mr + 1e-3f && gl.Y >= my - mr - 1e-3f && gh.Y <= my + mr + 1e-3f, "only the marker glows");
            Assert.That(Sees(map, plan, new V3(mx + mr + 0.008f, my, -5), ahead, VoxelPalette.CarWhite, "map"), "a white ring");
            Assert.That(Sees(map, plan, new V3(mx + 0.12f, my, -5), ahead, VoxelPalette.GrassMid, "map"), "the green square round it");
            Assert.That(Sees(map, plan, new V3(0.6f, StreetModels.StandContentBottom + 0.1f, -5), ahead, VoxelPalette.GlassSky, "map"), "water");
            // The chart: eight bars over the base line, two of them red, the panel between them.
            var chart = StreetModels.InfoStand(StandStyle.Companies); var bars = Tris(chart);
            var runs = new List<byte>(); byte last = VoxelPalette.Navy;
            for (float x = -0.86f; x < 0.9f; x += 0.01f)
            {
                var t = Hit(bars, new V3(x, StreetModels.StandContentBottom + 0.1f, -5), ahead, out _, "chart");
                byte c = IsColour(chart, t.Vertex, VoxelPalette.CarRed) ? VoxelPalette.CarRed : IsColour(chart, t.Vertex, VoxelPalette.GrassLight) ? VoxelPalette.GrassLight : VoxelPalette.Navy;
                if (c != last && c != VoxelPalette.Navy) runs.Add(c);
                last = c;
            }
            Assert.That(runs.Count, Is.EqualTo(8), "eight bars"); Assert.That(runs.Count(c => c == VoxelPalette.CarRed), Is.EqualTo(2), "two of them falling");
        }

        [Test] public void LooksUseKnownSurfacesAndTheStreetCostsLessThanItsVoxels()
        {
            var looks = Enumerable.Range(0, StreetModels.TreeKinds).Select(StreetModels.TreeLook)
                .Concat(new[] { StreetModels.ConiferLook, StreetModels.ShrubLook, StreetModels.LawnLook, StreetModels.FurnitureLook });
            foreach (var look in looks) Assert.That(VoxelSurfaces.Has(look.Surface), look.ToString());
            foreach (var (name, mesh, budget) in new[] { ("tree", StreetModels.Tree(1), 1000), ("conifer", StreetModels.Conifer(0), 600),
                ("shrub", StreetModels.Shrub(2, 1, 1.25f, 21, true), 1000), ("planter", StreetModels.Planter(0), 1300), ("lawn", StreetModels.Lawn(1), 3600),
                ("street lamp", StreetModels.StreetLamp(), 400), ("bench", StreetModels.Bench(2.4f, true), 500), ("plaza bench", StreetModels.Bench(3, false), 500),
                ("bin", StreetModels.Bin(), 400), ("signal post", StreetModels.SignalPost(), 500), ("car signal", StreetModels.CarSignal(0), 450),
                ("walk signal", StreetModels.WalkSignal(1), 450), ("button", StreetModels.CrossingButton(true), 200), ("bollard", StreetModels.Bollard(), 250),
                ("cone", StreetModels.Cone(), 300), ("works barrier", StreetModels.WorksBarrier(), 600), ("refuse container", StreetModels.Dumpster(), 650),
                ("map stand", StreetModels.InfoStand(StandStyle.Map), 600), ("company stand", StreetModels.InfoStand(StandStyle.Companies), 650) })
                Assert.That(mesh.TriangleCount, Is.LessThan(budget), name);
            // Every tree, lamp, bench, bin, planter, lawn, bollard, cone, works barrier and refuse container the district places,
            // both signal posts with the heads and the button they show and the plaza's stands, against the voxel models they
            // replace.
            long poly = 0, voxel = 0; int placed = 0;
            void Add(PolyMesh mesh, VoxelModel model) { poly += mesh.TriangleCount; voxel += VoxelTriangles(model); placed++; }
            foreach (var item in CityScenery.Props)
                switch (item.Kind)
                {
                    case SceneryKind.RoundTree: Add(StreetModels.Tree(item.Seed % StreetModels.TreeKinds), VoxelProps.RoundTree(item.Seed % 5)); break;
                    case SceneryKind.Conifer: Add(StreetModels.Conifer(item.Seed % StreetModels.ConiferKinds), VoxelProps.Conifer(item.Seed % 3)); break;
                    case SceneryKind.StreetLamp: Add(StreetModels.StreetLamp(), VoxelProps.StreetLamp()); break;
                    case SceneryKind.Bin: Add(StreetModels.Bin(), VoxelProps.Bin()); break;
                    case SceneryKind.Planter: Add(StreetModels.Planter(item.Seed % 3), VoxelProps.Planter(item.Seed % 3)); break;
                    case SceneryKind.PlazaBench: Add(StreetModels.Bench(3, false), VoxelProps.Bench(30, false)); break;
                    case SceneryKind.Bench: Add(StreetModels.Bench(2.4f, true), VoxelProps.Bench(24, true)); break;
                    case SceneryKind.Lawn: Add(StreetModels.Lawn(item.Seed % 2), VoxelProps.Lawn(item.Seed % 2)); break;
                    case SceneryKind.Bollard: Add(StreetModels.Bollard(), VoxelProps.Bollard()); break;
                    case SceneryKind.Cone: Add(StreetModels.Cone(), VoxelProps.Cone()); break;
                    case SceneryKind.WorksBarrier: Add(StreetModels.WorksBarrier(), VoxelProps.ConstructionBarrier()); break;
                    case SceneryKind.Dumpster: Add(StreetModels.Dumpster(), VoxelProps.Dumpster()); break;
                }
            foreach (var _ in CityRoads.Lamps) Add(StreetModels.StreetLamp(), VoxelProps.StreetLamp());
            for (int post = 0; post < 2; post++)
            {
                Add(StreetModels.SignalPost(), VoxelProps.SignalPost()); Add(StreetModels.CarSignal(0), VoxelProps.CarSignalHead(0));
                Add(StreetModels.WalkSignal(0), VoxelProps.PedSignalHead(0)); Add(StreetModels.CrossingButton(false), VoxelProps.CrosswalkButton(false));
            }
            foreach (var style in Stands) Add(StreetModels.InfoStand(style), VoxelProps.InfoStand(style));
            Assert.That(placed, Is.GreaterThan(40));
            Assert.That(poly, Is.LessThan(voxel * 0.8), "the low-poly street is cheaper than the voxel one: " + poly + " against " + voxel);
        }
    }
}
