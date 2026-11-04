using System;
using System.Collections.Generic;
using System.Linq;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    // The low-poly trees, conifers, shrubs, planters, lawns, street lamps, benches and bins that replace the voxel models:
    // sound meshes inside the voxel models' boxes, crowns over the walkers' heads, seats, kerbs, soil and the lamp glass at
    // the heights the scene expects, and the whole street cheaper than the voxels it replaces.
    public sealed class StreetModelTests
    {
        private const int Wall = (int)BuildingPart.Wall, Trim = (int)BuildingPart.Trim, Glass = (int)BuildingPart.Glass, Night = (int)BuildingPart.Night,
            Glow = (int)BuildingPart.Glow, Metal = (int)BuildingPart.Metal;
        private static readonly int[] All = { Wall, Trim, Glass, Night, Glow, Metal };
        // The raised beds of the western square plant these seeds.
        private static readonly int[] ParkSeeds = { 21, 22 };
        private static readonly byte[] FlowerColours = { VoxelPalette.FlowerPink, VoxelPalette.FlowerYellow, VoxelPalette.FlowerWhite, VoxelPalette.CarRed };
        private static readonly (float length, bool back)[] Benches = { (2.4f, true), (3, false), (1.2f, true), (5, false) };

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
        }

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
                if (name != "street lamp") Assert.That(mesh.Triangles[Glow], Is.Empty, name + ": nothing glows but the lamp");
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

        [Test] public void LooksUseKnownSurfacesAndTheStreetCostsLessThanItsVoxels()
        {
            var looks = Enumerable.Range(0, StreetModels.TreeKinds).Select(StreetModels.TreeLook)
                .Concat(new[] { StreetModels.ConiferLook, StreetModels.ShrubLook, StreetModels.LawnLook, StreetModels.FurnitureLook });
            foreach (var look in looks) Assert.That(VoxelSurfaces.Has(look.Surface), look.ToString());
            foreach (var (name, mesh, budget) in new[] { ("tree", StreetModels.Tree(1), 1000), ("conifer", StreetModels.Conifer(0), 600),
                ("shrub", StreetModels.Shrub(2, 1, 1.25f, 21, true), 1000), ("planter", StreetModels.Planter(0), 1300), ("lawn", StreetModels.Lawn(1), 3600),
                ("street lamp", StreetModels.StreetLamp(), 400), ("bench", StreetModels.Bench(2.4f, true), 500), ("plaza bench", StreetModels.Bench(3, false), 500),
                ("bin", StreetModels.Bin(), 400) })
                Assert.That(mesh.TriangleCount, Is.LessThan(budget), name);
            // Every tree, lamp, bench, bin, planter and lawn the district places, against the voxel models they replace.
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
                }
            foreach (var _ in CityRoads.Lamps) Add(StreetModels.StreetLamp(), VoxelProps.StreetLamp());
            Assert.That(placed, Is.GreaterThan(40));
            Assert.That(poly, Is.LessThan(voxel * 0.8), "the low-poly street is cheaper than the voxel one");
        }
    }
}
