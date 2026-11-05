using System;
using System.Collections.Generic;
using System.Linq;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    // The low-poly office and café that replace the voxel furniture and the primitive boxes: sound meshes inside the models
    // they replace, desk tops, seats, the counter and the pedestal at the heights the scene and its people expect, the
    // screens' openings, the door's sign, the elevator's label and the espresso machine's spot left clear, every key, lamp,
    // drawer and price where it belongs, and the whole interior within its triangle budget.
    public sealed class InteriorModelTests
    {
        private const int Wall = (int)BuildingPart.Wall, Trim = (int)BuildingPart.Trim, Glass = (int)BuildingPart.Glass, Night = (int)BuildingPart.Night,
            Glow = (int)BuildingPart.Glow, Metal = (int)BuildingPart.Metal;
        private static readonly int[] All = { Wall, Trim, Glass, Night, Glow, Metal };
        private static readonly V3 Ahead = new V3(0, 0, 1), Down3 = new V3(0, -1, 0), Up = new V3(0, 1, 0);
        // The desks as the office places them: the director's, the employees' and the supervisor's.
        private static readonly (string name, DeskStyle style, float width, float depth, byte top, byte frame)[] Desks =
        {
            ("director desk", DeskStyle.Executive, 3.6f, 1.25f, VoxelPalette.Wood, VoxelPalette.WoodDark),
            ("employee desk", DeskStyle.Staff, 2.1f, 1.1f, VoxelPalette.WoodLight, VoxelPalette.Navy),
            ("supervisor desk", DeskStyle.Staff, 2, 0.8f, VoxelPalette.Wood, VoxelPalette.Navy),
        };
        // The monitors round the director's screen and the employees'.
        private static readonly (string name, float width, float height, float centre)[] Monitors =
            { ("director monitor", 0.86f, 0.5f, 0.4f), ("employee monitor", 0.58f, 0.39f, 0.27f) };
        // The models with a glowing part: power lights, displays, lamps, the diffuser, the indicator and the till's screen.
        private static readonly string[] Lit = { "director monitor", "employee monitor", "phone", "golden phone", "coffee machine", "ceiling light", "elevator",
            "cafe counter", "pendant lamp" };

        private static IEnumerable<(string name, PolyMesh mesh)> Models()
        {
            foreach (var d in Desks) yield return (d.name, InteriorModels.Desk(d.style, d.width, d.depth, d.top, d.frame));
            yield return ("office chair", InteriorModels.OfficeChair(VoxelPalette.Navy));
            foreach (var m in Monitors) yield return (m.name, InteriorModels.Monitor(m.width, m.height, m.centre));
            yield return ("keyboard", InteriorModels.Keyboard());
            yield return ("mouse", InteriorModels.Mouse());
            yield return ("phone", InteriorModels.Phone(false));
            yield return ("golden phone", InteriorModels.Phone(true));
            yield return ("papers 1", InteriorModels.Papers(0.48f, 0.35f, 1));
            yield return ("papers 2", InteriorModels.Papers(0.4f, 0.3f, 2));
            yield return ("coffee machine", InteriorModels.CoffeeMachine());
            yield return ("coffee cup", InteriorModels.CoffeeCup());
            yield return ("coffee cabinet", InteriorModels.CoffeeCabinet());
            yield return ("potted plant", InteriorModels.PottedPlant(4));
            yield return ("display pedestal", InteriorModels.DisplayPedestal());
            yield return ("door", InteriorModels.Door());
            yield return ("window frame", InteriorModels.WindowFrame());
            yield return ("ceiling light", InteriorModels.CeilingLight());
            yield return ("plaque", InteriorModels.Plaque(6, 0.45f));
            yield return ("elevator", InteriorModels.ElevatorDoor());
            yield return ("skirting", InteriorModels.Skirting(12));
            yield return ("cafe counter", InteriorModels.CafeCounter());
            for (int seed = 0; seed < 2; seed++) yield return ("cafe shelf " + seed, InteriorModels.CafeShelf(seed));
            yield return ("cafe table", InteriorModels.CafeTable());
            yield return ("cafe chair", InteriorModels.CafeChair());
            yield return ("menu board", InteriorModels.MenuBoard());
            yield return ("pendant lamp", InteriorModels.PendantLamp());
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
        // Inside the box.
        private static void AssertWithin(PolyMesh mesh, V3 min, V3 max, string name)
        {
            const float e = 1e-3f;
            var (lo, hi) = Bounds(mesh);
            Assert.That(lo.X, Is.GreaterThanOrEqualTo(min.X - e), name + " left"); Assert.That(hi.X, Is.LessThanOrEqualTo(max.X + e), name + " right");
            Assert.That(lo.Z, Is.GreaterThanOrEqualTo(min.Z - e), name + " front"); Assert.That(hi.Z, Is.LessThanOrEqualTo(max.Z + e), name + " back");
            Assert.That(lo.Y, Is.GreaterThanOrEqualTo(min.Y - e), name + " bottom"); Assert.That(hi.Y, Is.LessThanOrEqualTo(max.Y + e), name + " top");
        }
        // Inside the box and standing on its floor (y = 0).
        private static void AssertInside(PolyMesh mesh, V3 min, V3 max, string name)
        {
            AssertWithin(mesh, min, max, name);
            Assert.That(Bounds(mesh).min.Y, Is.EqualTo(0).Within(1e-3f), name + " stands on its pivot");
        }
        private static bool IsColour(PolyMesh mesh, int vertex, byte colour)
            => Math.Abs(mesh.Uvs[vertex * 2] - VoxelPalette.U(colour)) < 1e-5f && Math.Abs(mesh.Uvs[vertex * 2 + 1] - VoxelPalette.V(colour)) < 1e-5f;
        private static List<Tri> Coloured(PolyMesh mesh, byte colour) => Tris(mesh).Where(t => IsColour(mesh, t.Vertex, colour)).ToList();
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
        private static bool Misses(List<Tri> tris, V3 origin, V3 dir) => !tris.Any(t => Meets(t, origin, dir, out _));
        private static float Down(List<Tri> tris, float x, float z, string name) { Hit(tris, new V3(x, 50, z), Down3, out float d, name); return 50 - d; }
        // The colour of the first surface a ray meets.
        private static bool Sees(PolyMesh mesh, List<Tri> tris, V3 origin, V3 dir, byte colour, string name) => IsColour(mesh, Hit(tris, origin, dir, out _, name).Vertex, colour);
        private static bool Sees(PolyMesh mesh, List<Tri> tris, V3 origin, V3 dir, int part, byte colour, string name)
        {
            var hit = Hit(tris, origin, dir, out _, name);
            return hit.Part == part && IsColour(mesh, hit.Vertex, colour);
        }
        // Distance from the vertical axis of the first surface met by a ray from 2 m out along −X (a hair off the axis line).
        private static float Reach(List<Tri> tris, float y, string name) { Hit(tris, new V3(-2, y, 0.004f), new V3(1, 0, 0), out float d, name); return 2 - d; }
        // The separate pieces of a set of triangles (keys, lamps, prices): triangles joined through shared corners.
        private static List<(V3 min, V3 max)> Pieces(List<Tri> tris)
        {
            var parent = Enumerable.Range(0, tris.Count).ToArray();
            int Find(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
            var corners = new Dictionary<(long, long, long), int>();
            for (int i = 0; i < tris.Count; i++)
                foreach (var p in new[] { tris[i].A, tris[i].B, tris[i].C })
                {
                    var key = ((long)Math.Round(p.X * 1e5), (long)Math.Round(p.Y * 1e5), (long)Math.Round(p.Z * 1e5));
                    if (corners.TryGetValue(key, out int j)) parent[Find(i)] = Find(j); else corners[key] = i;
                }
            return Enumerable.Range(0, tris.Count).GroupBy(Find).Select(g => Bounds(g.Select(i => tris[i]))).ToList();
        }
        // The number of runs of a colour met by rays along +Z from x0 to x1 at height y.
        private static int Runs(PolyMesh mesh, List<Tri> tris, float x0, float x1, float y, byte colour, string name)
        {
            int runs = 0; bool inside = false;
            for (float x = x0; x <= x1; x += 0.005f)
            {
                bool now = IsColour(mesh, Hit(tris, new V3(x, y, -5), Ahead, out _, name).Vertex, colour);
                if (now && !inside) runs++;
                inside = now;
            }
            return runs;
        }
        private static void AssertSame(PolyMesh a, PolyMesh b, string name)
        {
            Assert.That(a.Positions, Is.EqualTo(b.Positions), name); Assert.That(a.Normals, Is.EqualTo(b.Normals), name);
            Assert.That(a.Uvs, Is.EqualTo(b.Uvs), name); Assert.That(a.Shades, Is.EqualTo(b.Shades), name);
            for (int p = 0; p < BuildingModels.PartCount; p++) Assert.That(a.Triangles[p], Is.EqualTo(b.Triangles[p]), name + " part " + p);
        }

        [Test] public void MeshesAreWellFormedAndDeterministic()
        {
            var again = Models().ToList();
            int index = 0;
            foreach (var (name, mesh) in Models())
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
                // Only the plant shows the leaves material and only the pastry case is glass; the night windows stay the
                // buildings' own.
                if (name == "potted plant") Assert.That(mesh.Triangles[Wall].Length, Is.GreaterThan(0), name + " shows its leaves");
                else Assert.That(mesh.Triangles[Wall], Is.Empty, name + ": no wall material");
                if (name == "cafe counter") Assert.That(mesh.Triangles[Glass].Length, Is.GreaterThan(0), name + ": the pastry case's glass");
                else Assert.That(mesh.Triangles[Glass], Is.Empty, name + ": no glass");
                Assert.That(mesh.Triangles[Night], Is.Empty, name + ": no night windows");
                if (Lit.Contains(name)) Assert.That(mesh.Triangles[Glow].Length, Is.GreaterThan(0), name + " glows");
                else Assert.That(mesh.Triangles[Glow], Is.Empty, name + ": nothing glows");
                AssertSame(mesh, again[index++].mesh, name + " is deterministic");
            }
        }

        [Test] public void DesksCarryTheirTopsAtDeskHeightOverKneeRoomAndThreeDrawers()
        {
            float h = InteriorModels.DeskHeight;
            foreach (var (name, style, width, depth, top, frame) in Desks)
            {
                var desk = InteriorModels.Desk(style, width, depth, top, frame); var tris = Tris(desk);
                float hw = width / 2, hd = depth / 2; bool executive = style == DeskStyle.Executive;
                AssertInside(desk, new V3(-hw, 0, -hd), new V3(hw, h, hd), name);
                Assert.That(Bounds(desk).max.Y, Is.EqualTo(h).Within(1e-3f), name + ": the top");
                foreach (var (x, z) in new[] { (0f, 0f), (-hw + 0.05f, -hd + 0.05f), (hw - 0.05f, hd - 0.05f), (-hw + 0.05f, hd - 0.05f), (hw - 0.05f, -hd + 0.05f) })
                    Assert.That(Down(tris, x, z, name), Is.EqualTo(h).Within(2e-3f), name + ": flat at " + x + ", " + z);
                // The director writes on green leather edged in brass inside a wooden border; the staff on the bare top.
                Assert.That(Sees(desk, tris, new V3(0.3f, 50, 0.1f), Down3, Trim, executive ? VoxelPalette.PineDark : top, name), name + (executive ? ": leather" : ": the bare top"));
                Assert.That(Sees(desk, tris, new V3(0.3f, 50, hd - InteriorModels.LeatherBorder / 2), Down3, Trim, top, name), name + ": the wooden border");
                if (executive)
                {
                    float lx = hw - InteriorModels.LeatherBorder, lz = hd - InteriorModels.LeatherBorder;
                    foreach (var (x, z) in new[] { (0.3f, -lz + 0.004f), (0.3f, lz - 0.004f), (-lx + 0.004f, 0f), (lx - 0.004f, 0f) })
                        Assert.That(Sees(desk, tris, new V3(x, 50, z), Down3, Metal, VoxelPalette.Brass, name), name + ": a brass line round the leather at " + x + ", " + z);
                }
                // The drawers face the sitter at +Z on the right: three fronts in the top's wood over the pedestal's frame,
                // with brass knobs on the director's desk and chrome handles on the staff's.
                int fronts = 0; bool inside = false;
                for (float y = 0.05f; y < h - 0.08f; y += 0.005f)
                {
                    var hit = Hit(tris, new V3(hw - 0.25f, y, 5), new V3(0, 0, -1), out _, name);
                    bool now = IsColour(desk, hit.Vertex, top);
                    if (now && !inside) fronts++;
                    inside = now;
                }
                Assert.That(fronts, Is.EqualTo(3), name + ": three drawers");
                var handles = Tris(desk, Metal).Where(t => Math.Min(t.A.Z, Math.Min(t.B.Z, t.C.Z)) > hd - 0.06f).ToList();
                Assert.That(handles, Is.Not.Empty, name + ": handles on the drawers");
                Assert.That(handles.All(t => IsColour(desk, t.Vertex, executive ? VoxelPalette.Brass : VoxelPalette.Chrome)), name + (executive ? ": brass knobs" : ": chrome handles"));
                var (handleMin, handleMax) = Bounds(handles);
                Assert.That(handleMin.X > hw - 0.6f && handleMax.X < hw - 0.1f && handleMax.Z <= hd, name + ": on the pedestal under the top");
                // Knee room from the sitter's side to the modesty panel; the panel closes the front for the visitor.
                var back = Hit(tris, new V3(-0.2f, 0.45f, 5), new V3(0, 0, -1), out float d, name);
                Assert.That(5 - d, Is.LessThan(-hd + 0.14f), name + ": knee room down to the modesty panel");
                Assert.That(IsColour(desk, back.Vertex, frame), name + ": the panel in the frame's colour");
                var panel = Hit(tris, new V3(-0.2f, 0.45f, -5), Ahead, out d, name);
                Assert.That(d - 5, Is.LessThan(-hd + 0.11f), name + ": the modesty panel at the front");
                Assert.That(IsColour(desk, panel.Vertex, frame), name + ": in the frame's colour");
                // The ends carry the top just inside its edges.
                var end = Hit(tris, new V3(-5, 0.4f, 0.004f), new V3(1, 0, 0), out d, name);
                Assert.That(d - 5, Is.InRange(-hw + 0.035f, -hw + 0.06f), name + ": an end under the top's edge");
                Assert.That(IsColour(desk, end.Vertex, frame), name + ": the end in the frame's colour");
            }
            foreach (var (w, d) in new[] { (1f, 1f), (4.5f, 1f), (2f, 0.5f), (2f, 1.8f), (float.NaN, 1f), (2f, float.PositiveInfinity) })
                Assert.Throws<ArgumentOutOfRangeException>(() => InteriorModels.Desk(DeskStyle.Staff, w, d, VoxelPalette.Wood, VoxelPalette.Navy), w + " × " + d);
        }

        [Test] public void TheOfficeChairSeatsAtChairSeatOnFiveCastors()
        {
            var chair = InteriorModels.OfficeChair(VoxelPalette.Navy); var tris = Tris(chair); const string name = "office chair";
            AssertInside(chair, new V3(-0.35f, 0, -0.35f), new V3(0.35f, 1.25f, 0.35f), name);
            foreach (var (x, z) in new[] { (0f, -0.1f), (-0.15f, -0.2f), (0.15f, 0.05f), (0f, 0.15f) })
            {
                var seat = Hit(tris, new V3(x, 50, z), Down3, out float d, name);
                Assert.That(50 - d, Is.EqualTo(InteriorModels.ChairSeat).Within(1e-3f), name + ": the seat at " + x + ", " + z);
                Assert.That(IsColour(chair, seat.Vertex, VoxelPalette.Navy), name + ": upholstered at " + x + ", " + z);
            }
            // Five castors on the floor at the tips of the arms.
            for (int k = 0; k < 5; k++)
            {
                double a = -Math.PI / 2 + 2 * Math.PI * k / 5;
                var wheel = Hit(tris, new V3(0.27f * (float)Math.Cos(a), -1, 0.27f * (float)Math.Sin(a)), Up, out float d, name);
                Assert.That(d - 1, Is.LessThan(0.005f), name + ": castor " + k + " on the floor");
                Assert.That(IsColour(chair, wheel.Vertex, VoxelPalette.Tire), name + ": rubber castor " + k);
            }
            // The reclined back towards +Z, armrest pads either side over the seat.
            var back = Hit(tris, new V3(0, 0.95f, -5), Ahead, out float far, name);
            Assert.That(IsColour(chair, back.Vertex, VoxelPalette.Navy), name + ": an upholstered back");
            Assert.That(far - 5, Is.InRange(0.15f, 0.3f), name + ": the back behind the seat");
            foreach (float x in new[] { -0.28f, 0.28f })
            {
                Assert.That(Sees(chair, tris, new V3(x, 50, 0), Down3, VoxelPalette.Ink, name), name + ": an armrest pad at " + x);
                Assert.That(Down(tris, x, 0, name), Is.InRange(InteriorModels.ChairSeat + 0.15f, 0.75f), name + ": over the seat");
            }
            var red = InteriorModels.OfficeChair(VoxelPalette.CarRed);
            Assert.That(Sees(red, Tris(red), new V3(0, 50, -0.1f), Down3, VoxelPalette.CarRed, name), "the fabric as asked");
        }

        [Test] public void MonitorsLeaveTheScreenSlabToTheSceneInsideTheirBezels()
        {
            foreach (var (name, width, height, centre) in Monitors)
            {
                var monitor = InteriorModels.Monitor(width, height, centre); var tris = Tris(monitor);
                float hw = width / 2, y0 = centre - height / 2, y1 = centre + height / 2, side = InteriorModels.BezelSide;
                var (lo, hi) = Bounds(monitor);
                Assert.That(lo.Y, Is.EqualTo(0).Within(1e-3f), name + ": the foot on the desk");
                Assert.That(hi.Y, Is.EqualTo(y1 + side).Within(1e-3f), name + ": the bezel's top");
                Assert.That(lo.X >= -hw - side - 1e-3f && hi.X <= hw + side + 1e-3f, name + ": as wide as the bezel");
                Assert.That(hi.Z, Is.GreaterThan(InteriorModels.ScreenDepth + 0.05f), name + ": a stand behind the screen");
                // Over the foot nothing comes forward of the bezel, the power light on its face.
                var raised = Tris(monitor).Where(t => Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y)) > 0.05f).ToList();
                Assert.That(Bounds(raised).min.Z, Is.GreaterThanOrEqualTo(-InteriorModels.BezelProud - 1e-3f), name + ": a slim bezel");
                // The opening: rays through it pass the scene's screen slab z ∈ [0, ScreenDepth] and meet the dark back.
                for (int i = 0; i <= 6; i++)
                    for (int j = 0; j <= 4; j++)
                    {
                        float x = -hw + 0.01f + (width - 0.02f) * i / 6, y = y0 + 0.01f + (height - 0.02f) * j / 4;
                        var behind = Hit(tris, new V3(x, y, -5), Ahead, out float d, name);
                        Assert.That(d - 5, Is.GreaterThanOrEqualTo(InteriorModels.ScreenDepth), name + ": the opening at " + x + ", " + y + " left to the screen");
                        Assert.That(IsColour(monitor, behind.Vertex, VoxelPalette.Ink), name + ": a dark back behind the screen");
                    }
                // The bezel round it, BezelProud in front of the screen's face.
                foreach (var (x, y) in new[] { (hw + side / 2, centre), (-hw - side / 2, centre), (0f, y1 + side / 2), (0f, y0 - InteriorModels.BezelChin / 2) })
                {
                    var bezel = Hit(tris, new V3(x, y, -5), Ahead, out float d, name);
                    Assert.That(d - 5, Is.EqualTo(-InteriorModels.BezelProud).Within(1e-3f), name + ": the bezel at " + x + ", " + y);
                    Assert.That(IsColour(monitor, bezel.Vertex, VoxelPalette.Ink), name + ": a dark bezel");
                }
                // The power light: a small green lamp in the chin's right corner.
                var glow = Tris(monitor, Glow); var (gl, gh) = Bounds(glow);
                Assert.That(glow.All(t => IsColour(monitor, t.Vertex, VoxelPalette.SignalGreen)), name + ": a green light");
                Assert.That(gl.Y >= y0 - InteriorModels.BezelChin && gh.Y <= y0 && gl.X > hw / 2, name + ": in the chin, on the right");
                Assert.That(gh.X - gl.X, Is.LessThan(0.015f), name + ": a small light");
            }
            foreach (var (w, h, c) in new[] { (0.1f, 0.5f, 0.4f), (2f, 0.5f, 0.4f), (0.86f, 0.1f, 0.4f), (0.86f, 0.5f, 0.3f), (float.NaN, 0.5f, 0.4f) })
                Assert.Throws<ArgumentOutOfRangeException>(() => InteriorModels.Monitor(w, h, c), w + " × " + h + " at " + c);
        }

        [Test] public void TheKeyboardHasEveryKeyAndTheMouseItsWheel()
        {
            var keyboard = InteriorModels.Keyboard(); var tris = Tris(keyboard); const string name = "keyboard";
            AssertInside(keyboard, new V3(-0.425f, 0, -0.125f), new V3(0.425f, 0.05f, 0.125f), name);
            var (lo, hi) = Bounds(keyboard);
            Assert.That(hi.X - lo.X, Is.EqualTo(InteriorModels.KeyboardWidth).Within(1e-3f), name + " width");
            Assert.That(hi.Z - lo.Z, Is.EqualTo(InteriorModels.KeyboardDepth).Within(1e-3f), name + " depth");
            Assert.That(hi.Y, Is.EqualTo(InteriorModels.KeyTop).Within(1e-4f), name + ": the key tops");
            Assert.That(InteriorModels.KeyCount, Is.EqualTo(80), "a full keyboard with its numeric pad");
            var keys = Pieces(Coloured(keyboard, VoxelPalette.Cream));
            Assert.That(keys.Count, Is.EqualTo(InteriorModels.KeyCount), name + ": every key a cap of its own");
            foreach (var (min, max) in keys)
            {
                Assert.That(Down(tris, (min.X + max.X) / 2, (min.Z + max.Z) / 2, name), Is.EqualTo(InteriorModels.KeyTop).Within(1e-4f), name + ": a key top at KeyTop");
                Assert.That(min.Y, Is.EqualTo(InteriorModels.KeyboardCase).Within(1e-4f), name + ": the key on the case");
            }
            // The space bar, the widest key, in the row nearest the typist (−Z).
            var space = keys.OrderByDescending(k => k.max.X - k.min.X).First();
            Assert.That(space.max.X - space.min.X, Is.GreaterThan(0.2f), name + ": a long space bar");
            Assert.That(keys.Min(k => k.min.Z), Is.EqualTo(space.min.Z).Within(1e-4f), name + ": the space bar towards the typist");
            float rim = InteriorModels.KeyboardWidth / 2 - 0.015f;
            Assert.That(Sees(keyboard, tris, new V3(rim, 50, 0), Down3, VoxelPalette.StoneDark, name), name + ": a dark case");
            Assert.That(Down(tris, rim, 0, name), Is.EqualTo(InteriorModels.KeyboardCase).Within(1e-3f), name + ": the case's top");

            var mouse = InteriorModels.Mouse(); var shell = Tris(mouse); const string m = "mouse";
            AssertInside(mouse, new V3(-0.04f, 0, -0.06f), new V3(0.04f, 0.04f, 0.06f), m);
            Assert.That(Sees(mouse, shell, new V3(0, 50, 0.02f), Down3, VoxelPalette.Cream, m), m + ": a cream shell");
            Assert.That(Sees(mouse, shell, new V3(0, 50, -0.021f), Down3, VoxelPalette.StoneDark, m), m + ": the wheel over the shell");
            Assert.That(Bounds(Coloured(mouse, VoxelPalette.StoneDark)).max.Z, Is.LessThan(0), m + ": the wheel at the front");
        }

        [Test] public void TelephonesKeepTheirKeysAndDisplayInsideThePrimitiveAndTheGoldenOneIsBrass()
        {
            foreach (bool golden in new[] { false, true })
            {
                var phone = InteriorModels.Phone(golden); var tris = Tris(phone); string name = golden ? "golden phone" : "phone";
                // The primitive base and handset the model replaces: 0.72 × 0.4 m and PhoneHeight over the desk.
                AssertInside(phone, new V3(-0.36f, 0, -0.2f), new V3(0.36f, InteriorModels.PhoneHeight, 0.2f), name);
                var keys = Pieces(Coloured(phone, VoxelPalette.Cream));
                Assert.That(keys.Count, Is.EqualTo(InteriorModels.PhoneKeys), name + ": twelve keys");
                Assert.That(keys.All(k => k.max.Z < 0 && k.min.Y > 0.06f), name + ": on the console's slope towards the caller");
                Assert.That(Sees(phone, tris, new V3(-0.105f, 0.0825f, -5), Ahead, VoxelPalette.Cream, name), name + ": the keys face the caller");
                var display = Tris(phone, Glow);
                Assert.That(display.Count, Is.EqualTo(2), name + ": one display");
                Assert.That(display.All(t => IsColour(phone, t.Vertex, VoxelPalette.ScreenGreen) && phone.Normal(t.Vertex).Z < -0.5f), name + ": a green display tilted to the caller");
                Assert.That(Hit(tris, new V3(0.12f, 0.11f, -5), Ahead, out _, name).Part, Is.EqualTo(Glow), name + ": the display in view");
                // The handset across the top, its cups over the cradles, in the body's finish: navy paint or polished brass.
                var handset = Hit(tris, new V3(0, 50, 0.01f), Down3, out float d, name);
                Assert.That(50 - d, Is.GreaterThan(0.25f), name + ": the handset on top");
                Assert.That(handset.Part, Is.EqualTo(golden ? Metal : Trim), name + ": its finish");
                Assert.That(IsColour(phone, handset.Vertex, golden ? VoxelPalette.Brass : VoxelPalette.Navy), name + ": its colour");
                foreach (float x in new[] { -0.245f, 0.245f })
                    Assert.That(Down(tris, x, 0.01f, name), Is.InRange(0.24f, InteriorModels.PhoneHeight), name + ": a cup over its cradle at " + x);
                Assert.That(Bounds(Coloured(phone, VoxelPalette.Ink).Where(t => t.A.Y > 0.03f)).min.X, Is.LessThan(-0.3f), name + ": the cord on the left");
                if (golden) Assert.That(Tris(phone, Metal).All(t => IsColour(phone, t.Vertex, VoxelPalette.Brass)), name + ": all brass");
                else Assert.That(phone.Triangles[Metal], Is.Empty, name + ": no polished metal");
            }
            Assert.That(Bounds(InteriorModels.Phone(true)), Is.EqualTo(Bounds(InteriorModels.Phone(false))), "the same telephone in two finishes");
        }

        [Test] public void PapersLieInALowStackWithTypeAndAPen()
        {
            foreach (var (width, depth, seed) in new[] { (0.48f, 0.35f, 1), (0.4f, 0.3f, 2) })
            {
                var papers = InteriorModels.Papers(width, depth, seed); var tris = Tris(papers); string name = "papers " + seed;
                AssertInside(papers, new V3(-width / 2, 0, -depth / 2), new V3(width / 2, 0.04f, depth / 2), name);
                // The top sheet, white, five sheets up; seven lines of type on it and the pen across.
                var sheet = Hit(tris, new V3(width * 0.78f * 0.3f, 50, -depth * 0.8f * 0.35f), Down3, out float d, name);
                Assert.That(50 - d, Is.InRange(0.015f, 0.02f), name + ": five sheets"); Assert.That(IsColour(papers, sheet.Vertex, VoxelPalette.CarWhite), name + ": a white top sheet");
                var type = Coloured(papers, VoxelPalette.StoneDark);
                Assert.That(Pieces(type).Count, Is.EqualTo(7), name + ": seven lines of type");
                Assert.That(Bounds(type).min.Y, Is.GreaterThan(50 - d), name + ": the type on the top sheet");
                Assert.That(Tris(papers, Metal), Is.Not.Empty, name + ": the pen's brass tip and clip");
                Assert.That(Tris(papers, Metal).All(t => IsColour(papers, t.Vertex, VoxelPalette.Brass)), name + ": brass");
                Assert.That(Bounds(papers).max.Y, Is.GreaterThan(50 - d + 0.008f), name + ": the pen on the stack");
            }
            Assert.That(InteriorModels.Papers(0.4f, 0.3f, 1).Positions, Is.Not.EqualTo(InteriorModels.Papers(0.4f, 0.3f, 2).Positions), "seeded sheets");
            foreach (var (w, d) in new[] { (0.1f, 0.3f), (1.2f, 0.3f), (0.4f, 0.05f), (0.4f, 1.5f), (float.NaN, 0.3f) })
                Assert.Throws<ArgumentOutOfRangeException>(() => InteriorModels.Papers(w, d, 0), w + " × " + d);
        }

        [Test] public void TheEspressoMachineLightsTwoLampsOverTwoCupsAndFitsItsCabinetAndTheCounter()
        {
            var machine = InteriorModels.CoffeeMachine(); var tris = Tris(machine); const string name = "coffee machine";
            AssertInside(machine, new V3(-0.35f, 0, -0.275f), new V3(0.35f, 0.6f, 0.275f), name);
            var (lo, hi) = Bounds(machine);
            Assert.That(hi.X - lo.X, Is.LessThanOrEqualTo(InteriorModels.CounterMachineX1 - InteriorModels.CounterMachineX0), name + ": fits the counter's spot across");
            Assert.That(hi.Z - lo.Z, Is.LessThanOrEqualTo(InteriorModels.CounterMachineZ1 - InteriorModels.CounterMachineZ0), name + ": and in depth");
            Assert.That(Math.Max(-lo.X, hi.X) <= 0.55f && Math.Max(-lo.Z, hi.Z) <= 0.4f, name + ": on the cabinet's top");
            // Two lamps on the front: green on the right, amber on the left, both in view.
            var glow = Tris(machine, Glow); var lamps = Pieces(glow);
            Assert.That(lamps.Count, Is.EqualTo(2), name + ": two lamps");
            Assert.That(glow.All(t => machine.Normal(t.Vertex).Z < -0.999f), name + ": on the front");
            Assert.That(glow.Where(t => IsColour(machine, t.Vertex, VoxelPalette.SignalGreen)).All(t => t.A.X > 0), name + ": green on the right");
            Assert.That(glow.Where(t => IsColour(machine, t.Vertex, VoxelPalette.SignalAmber)).All(t => t.A.X < 0), name + ": amber on the left");
            Assert.That(glow.All(t => IsColour(machine, t.Vertex, VoxelPalette.SignalGreen) || IsColour(machine, t.Vertex, VoxelPalette.SignalAmber)), name + ": lamp colours");
            foreach (var (min, max) in lamps)
                Assert.That(Hit(tris, new V3((min.X + max.X) / 2, (min.Y + max.Y) / 2, -5), Ahead, out _, name).Part, Is.EqualTo(Glow), name + ": a lamp in view");
            Assert.That(Sees(machine, tris, new V3(0.1f, 0.45f, -5), Ahead, Metal, VoxelPalette.Brass, name), name + ": a brass panel");
            Assert.That(Sees(machine, tris, new V3(-0.02f, 0.38f, -5), Ahead, VoxelPalette.Cream, name), name + ": the gauge's face");
            foreach (float x in new[] { -0.13f, 0.13f })
                Assert.That(Sees(machine, tris, new V3(x, 0.07f, -5), Ahead, VoxelPalette.CarWhite, name), name + ": a cup under the group head at " + x);
            Assert.That(Sees(machine, tris, new V3(0.004f, 50, -0.2475f), Down3, Trim, VoxelPalette.Ink, name), name + ": the drip tray's grille");
            Assert.That(Sees(machine, tris, new V3(0.004f, 50, -0.2355f), Down3, Metal, VoxelPalette.Chrome, name), name + ": chrome between its slats");

            var cup = InteriorModels.CoffeeCup(); var china = Tris(cup); const string c = "coffee cup";
            AssertInside(cup, new V3(-0.065f, 0, -0.065f), new V3(0.065f, 0.07f, 0.065f), c);
            var coffee = Hit(china, new V3(0.004f, 50, 0.004f), Down3, out float d, c);
            Assert.That(IsColour(cup, coffee.Vertex, VoxelPalette.BarkDark), c + ": coffee in it"); Assert.That(50 - d, Is.InRange(0.04f, 0.06f), c + ": below the rim");
            var saucer = Hit(china, new V3(-0.055f, 50, 0), Down3, out d, c);
            Assert.That(IsColour(cup, saucer.Vertex, VoxelPalette.CarWhite) && 50 - d < 0.012f, c + ": on a white saucer");
        }

        [Test] public void TheCabinetAndThePedestalOfferFlatTopsForWhatStandsOnThem()
        {
            var cabinet = InteriorModels.CoffeeCabinet(); var tris = Tris(cabinet); const string name = "coffee cabinet";
            AssertInside(cabinet, new V3(-0.55f, 0, -0.4f), new V3(0.55f, InteriorModels.CabinetTop, 0.4f), name);
            foreach (float x in new[] { -0.5f, 0, 0.5f })
                foreach (float z in new[] { -0.35f, 0, 0.35f })
                    Assert.That(Down(tris, x, z, name), Is.EqualTo(InteriorModels.CabinetTop).Within(1e-3f), name + ": a flat top at " + x + ", " + z);
            Assert.That(Sees(cabinet, tris, new V3(0.1f, 50, 0.1f), Down3, VoxelPalette.WoodDark, name), name + ": a dark top");
            foreach (float s in new[] { -1f, 1f })
            {
                Assert.That(Sees(cabinet, tris, new V3(s * 0.27f, 0.5f, -5), Ahead, VoxelPalette.Wood, name), name + ": a panelled door at " + s);
                Assert.That(Sees(cabinet, tris, new V3(s * 0.04f, 0.78f, -5), Ahead, Metal, VoxelPalette.Brass, name), name + ": a brass knob at " + s);
            }

            var pedestal = InteriorModels.DisplayPedestal(); var stand = Tris(pedestal); const string p = "display pedestal"; float h = InteriorModels.PedestalHalf;
            AssertInside(pedestal, new V3(-h, 0, -h), new V3(h, InteriorModels.PedestalTop, h), p);
            foreach (float x in new[] { -0.4f, 0, 0.4f })
                foreach (float z in new[] { -0.4f, 0, 0.4f })
                    Assert.That(Down(stand, x, z, p), Is.EqualTo(InteriorModels.PedestalTop).Within(1e-3f), p + ": a flat top at " + x + ", " + z);
            var (phoneMin, phoneMax) = Bounds(InteriorModels.Phone(true));
            Assert.That(Math.Max(-phoneMin.X, phoneMax.X) <= h && Math.Max(-phoneMin.Z, phoneMax.Z) <= h, "the golden telephone fits on the pedestal");
            foreach (float y in new[] { 0.09f, 1.03f })
                Assert.That(Sees(pedestal, stand, new V3(0.1f, y, -5), Ahead, Metal, VoxelPalette.Brass, p), p + ": a brass band at " + y);
            Assert.That(Sees(pedestal, stand, new V3(0.1f, 0.5f, -5), Ahead, VoxelPalette.Navy, p), p + ": navy");
        }

        [Test] public void ThePottedPlantGrowsItsLeavesOverItsPot()
        {
            var plant = InteriorModels.PottedPlant(4); var tris = Tris(plant); const string name = "potted plant";
            AssertInside(plant, new V3(-0.45f, 0, -0.45f), new V3(0.45f, 1.65f, 0.45f), name);
            var (leafMin, leafMax) = Bounds(plant, Wall);
            Assert.That(leafMin.Y, Is.GreaterThan(InteriorModels.PotTop + 0.15f), name + ": the leaves over the pot");
            Assert.That(leafMax.Y, Is.GreaterThan(1.45f), name + ": nearly as tall as the voxel plant");
            Assert.That(leafMax.X - leafMin.X, Is.GreaterThan(0.6f), name + ": a full head of leaves");
            Assert.That(Hit(tris, new V3(0.004f, 50, 0.004f), Down3, out _, name).Part, Is.EqualTo(Wall), name + ": seen from above, leaves");
            Assert.That(Sees(plant, tris, new V3(-2, 0.3f, 0.004f), new V3(1, 0, 0), VoxelPalette.BrickRed, name), name + ": a terracotta pot");
            Assert.That(Reach(tris, InteriorModels.PotTop - 0.03f, name), Is.InRange(InteriorModels.PotRadius - 0.01f, InteriorModels.PotRadius + 1e-3f), name + ": the rolled rim");
            var soil = Hit(tris, new V3(0.18f, 0.8f, -0.12f), Down3, out float d, name);
            Assert.That(IsColour(plant, soil.Vertex, VoxelPalette.Soil), name + ": soil under the leaves");
            Assert.That(0.8f - d, Is.InRange(0.5f, InteriorModels.PotTop), name + ": inside the rim");
            Assert.That(Sees(plant, tris, new V3(-2, 0.75f, 0.003f), new V3(1, 0, 0), VoxelPalette.Bark, name), name + ": a trunk");
            Assert.That(InteriorModels.PottedPlant(4).Positions, Is.Not.EqualTo(InteriorModels.PottedPlant(5).Positions), "seeded clumps");
        }

        [Test] public void TheDoorKeepsItsSignClearOnTheUpperPanelAndItsHandleOnTheLeaf()
        {
            var door = InteriorModels.Door(); var tris = Tris(door); const string name = "door";
            AssertInside(door, new V3(-InteriorModels.DoorHalfWidth, 0, InteriorModels.DoorSignZ), new V3(InteriorModels.DoorHalfWidth, InteriorModels.DoorHeight, 0), name);
            Assert.That(Bounds(door).min.Z, Is.GreaterThan(InteriorModels.DoorSignZ + 0.005f), name + ": nothing reaches the sign");
            // The sign hangs on the upper panel: flat and navy all over its area.
            float face = float.NaN;
            foreach (float x in new[] { -0.4f, 0, 0.4f })
                foreach (float y in new[] { 1.7f, 1.95f, 2.2f })
                {
                    var panel = Hit(tris, new V3(x, y, -5), Ahead, out float d, name);
                    if (float.IsNaN(face)) face = d;
                    Assert.That(d, Is.EqualTo(face).Within(1e-4f), name + ": a flat upper panel at " + x + ", " + y);
                    Assert.That(IsColour(door, panel.Vertex, VoxelPalette.Navy), name + ": navy at " + x + ", " + y);
                }
            foreach (float x in new[] { -0.25f, 0.25f })
                Assert.That(Sees(door, tris, new V3(x, 0.9f, -5), Ahead, VoxelPalette.Navy, name), name + ": a lower panel at " + x);
            // Brass casing round the leaf, a brass kick plate, the lever in front of its backplate and a keyhole under it.
            foreach (var (x, y) in new[] { (-0.76f, 1.3f), (0.76f, 1.3f), (0f, 2.52f) })
            {
                var casing = Hit(tris, new V3(x, y, -5), Ahead, out float d, name);
                Assert.That(d - 5, Is.EqualTo(-InteriorModels.DoorDepth).Within(1e-3f), name + ": the casing's face at " + x + ", " + y);
                Assert.That(casing.Part == Metal && IsColour(door, casing.Vertex, VoxelPalette.Brass), name + ": brass casing at " + x + ", " + y);
            }
            Assert.That(Sees(door, tris, new V3(0.3f, 0.12f, -5), Ahead, Metal, VoxelPalette.Brass, name), name + ": a brass kick plate");
            var lever = Hit(tris, new V3(InteriorModels.DoorHandleX - 0.06f, InteriorModels.DoorHandleY - 0.003f, -5), Ahead, out float l, name);
            Assert.That(lever.Part == Metal && IsColour(door, lever.Vertex, VoxelPalette.Brass), name + ": a brass lever");
            Assert.That(5 - l, Is.GreaterThan(0.29f), name + ": the lever stands off the leaf");
            Assert.That(Sees(door, tris, new V3(InteriorModels.DoorHandleX, 1.01f, -5), Ahead, VoxelPalette.Ink, name), name + ": a keyhole under the handle");
        }

        [Test] public void WindowFramesLeaveTheirOpeningsClearForTheGlass()
        {
            var window = InteriorModels.WindowFrame(); var tris = Tris(window); const string name = "window frame";
            float inner = InteriorModels.WindowInnerHalf, rail = InteriorModels.WindowRail, top = InteriorModels.WindowFrameHeight;
            AssertInside(window, new V3(-1.5f, 0, InteriorModels.WindowSillFront), new V3(1.5f, top, 0.075f), name);
            foreach (float x in new[] { -inner + 0.02f, -0.7f, -0.06f, 0.06f, 0.7f, inner - 0.02f })
                foreach (float y in new[] { 0.25f, 1, top - rail - 0.02f })
                    Assert.That(Misses(tris, new V3(x, y, -5), Ahead), name + ": the opening clear at " + x + ", " + y);
            Assert.That(Sees(window, tris, new V3(0, 1, -5), Ahead, VoxelPalette.Cream, name), name + ": a cream mullion");
            var frame = Hit(tris, new V3((inner + InteriorModels.WindowHalf) / 2, 1, -5), Ahead, out float d, name);
            Assert.That(IsColour(window, frame.Vertex, VoxelPalette.Navy), name + ": a navy frame"); Assert.That(d - 5, Is.EqualTo(-0.075f).Within(1e-3f), name + ": its face");
            Assert.That(Down(tris, 0.5f, 0, name), Is.EqualTo(top).Within(1e-3f), name + ": its head");
            var sill = Hit(tris, new V3(0, 50, -0.3f), Down3, out d, name);
            Assert.That(IsColour(window, sill.Vertex, VoxelPalette.Cream), name + ": a cream sill"); Assert.That(50 - d, Is.InRange(rail, 0.25f), name + ": under the opening");
        }

        [Test] public void CeilingLightsThePlaqueTheElevatorAndTheSkirtingKeepToTheWallsAndTheCeiling()
        {
            var light = InteriorModels.CeilingLight(); var tris = Tris(light); const string name = "ceiling light";
            AssertWithin(light, new V3(-0.9f, -InteriorModels.CeilingLightDrop, -0.4f), new V3(0.9f, 0, 0.4f), name);
            Assert.That(Bounds(light).max.Y, Is.EqualTo(0).Within(1e-4f), name + ": against the ceiling");
            var glow = Tris(light, Glow);
            Assert.That(glow.Count, Is.EqualTo(2), name + ": one diffuser");
            Assert.That(glow.All(t => light.Normal(t.Vertex).Y < -0.999f && IsColour(light, t.Vertex, VoxelPalette.Headlight)), name + ": lit downward");
            Assert.That(Hit(tris, new V3(0.2f, -1, 0.1f), Up, out _, name).Part, Is.EqualTo(Glow), name + ": from below, the diffuser");
            var bar = Hit(tris, new V3(0.425f, -1, 0.1f), Up, out float d, name);
            Assert.That(bar.Part, Is.EqualTo(Trim), name + ": a bar across it"); Assert.That(d - 1, Is.EqualTo(-InteriorModels.CeilingLightDrop).Within(1e-3f), name + ": hanging lowest");

            var plaque = InteriorModels.Plaque(6, 0.45f); var board = Tris(plaque); const string p = "plaque";
            AssertWithin(plaque, new V3(-3, -0.225f, InteriorModels.PlaqueFront), new V3(3, 0.225f, 0.05f), p);
            Assert.That(Bounds(plaque).min.Z, Is.EqualTo(InteriorModels.PlaqueFront).Within(1e-4f), p + ": the frame's face");
            foreach (var (x, y) in new[] { (0f, 0f), (-2.5f, 0.1f), (2.5f, -0.1f) })
                Assert.That(Sees(plaque, board, new V3(x, y, -5), Ahead, VoxelPalette.Navy, p), p + ": a navy board at " + x + ", " + y);
            var frame = Hit(board, new V3(3 - 0.035f, 0, -5), Ahead, out d, p);
            Assert.That(frame.Part == Metal && IsColour(plaque, frame.Vertex, VoxelPalette.Brass), p + ": a brass frame");
            Assert.That(d - 5, Is.EqualTo(InteriorModels.PlaqueFront).Within(1e-3f), p + ": its face");
            foreach (var (w, h) in new[] { (0.2f, 0.45f), (13f, 0.45f), (6f, 0.1f), (6f, 3f), (float.NaN, 0.45f) })
                Assert.Throws<ArgumentOutOfRangeException>(() => InteriorModels.Plaque(w, h), w + " × " + h);

            // The elevator: the call plate stands on the wall within 12 cm of the right jamb.
            var lift = InteriorModels.ElevatorDoor(); var shaft = Tris(lift); const string e = "elevator";
            AssertInside(lift, new V3(-InteriorModels.ElevatorJamb, 0, InteriorModels.ElevatorLabelZ), new V3(InteriorModels.ElevatorJamb + 0.12f, InteriorModels.ElevatorHeight, 0), e);
            Assert.That(Bounds(lift).min.Z, Is.GreaterThan(InteriorModels.ElevatorLabelZ + 0.01f), e + ": nothing reaches the label");
            foreach (float s in new[] { -1f, 1f })
            {
                Assert.That(Sees(lift, shaft, new V3(s * 0.3f, 1.2f, -5), Ahead, Trim, VoxelPalette.Navy, e), e + ": a navy leaf at " + s);
                var jamb = Hit(shaft, new V3(s * (InteriorModels.ElevatorHalf + InteriorModels.ElevatorJamb) / 2, 1.2f, -5), Ahead, out d, e);
                Assert.That(jamb.Part == Metal && IsColour(lift, jamb.Vertex, VoxelPalette.Brass), e + ": a brass jamb at " + s);
                Assert.That(d - 5, Is.EqualTo(-InteriorModels.ElevatorDepth).Within(1e-3f), e + ": the jamb's face");
            }
            Assert.That(Sees(lift, shaft, new V3(0, 1.2f, -5), Ahead, Metal, VoxelPalette.Chrome, e), e + ": the chrome seam");
            Assert.That(Sees(lift, shaft, new V3(0.2f, 2.5f, -5), Ahead, Glow, VoxelPalette.WindowLit, e), e + ": the lit indicator over the leaves");
            Assert.That(Sees(lift, shaft, new V3(0.84f, 1.185f, -5), Ahead, Glow, VoxelPalette.SignalAmber, e), e + ": the lit call button");
            Assert.That(Sees(lift, shaft, new V3(0.84f, 1.095f, -5), Ahead, Trim, VoxelPalette.Cream, e), e + ": the unlit one");

            var skirting = InteriorModels.Skirting(12); const string k = "skirting";
            AssertInside(skirting, new V3(-6, 0, -InteriorModels.SkirtingDepth), new V3(6, InteriorModels.SkirtingHeight, 0), k);
            var (lo, hi) = Bounds(skirting);
            Assert.That(hi.X - lo.X, Is.EqualTo(12).Within(1e-3f), k + ": its length"); Assert.That(hi.Y, Is.EqualTo(InteriorModels.SkirtingHeight).Within(1e-4f), k + ": its height");
            Assert.That(Tris(skirting).All(t => IsColour(skirting, t.Vertex, VoxelPalette.WoodDark)), k + ": dark wood");
            foreach (float bad in new[] { 0.1f, 0, -1, 41, float.NaN })
                Assert.Throws<ArgumentOutOfRangeException>(() => InteriorModels.Skirting(bad), bad.ToString());
        }

        [Test] public void TheCafeCounterKeepsTheMachineSpotClearBetweenItsCaseAndItsTill()
        {
            var counter = InteriorModels.CafeCounter(); var tris = Tris(counter); const string name = "cafe counter";
            AssertInside(counter, new V3(-1.6f, 0, -0.35f), new V3(1.6f, InteriorModels.CounterHeight, 0.35f), name);
            foreach (var (x, z) in new[] { (0f, 0f), (-0.4f, -0.3f), (0.2f, 0.3f), (-1.5f, 0.28f), (1.55f, -0.3f) })
            {
                var top = Hit(tris, new V3(x, 50, z), Down3, out float d, name);
                Assert.That(50 - d, Is.EqualTo(InteriorModels.CounterTop).Within(1e-3f), name + ": the top at " + x + ", " + z);
                Assert.That(IsColour(counter, top.Vertex, VoxelPalette.StoneLight), name + ": a stone top at " + x + ", " + z);
            }
            float x0 = InteriorModels.CounterMachineX0, x1 = InteriorModels.CounterMachineX1, z0 = InteriorModels.CounterMachineZ0, z1 = InteriorModels.CounterMachineZ1;
            for (int i = 0; i <= 7; i++)
                for (int j = 0; j <= 5; j++)
                {
                    float x = x0 + (x1 - x0) * i / 7, z = z0 + (z1 - z0) * j / 5;
                    Assert.That(Down(tris, x, z, name), Is.EqualTo(InteriorModels.CounterTop).Within(1e-3f), name + ": the machine's spot clear at " + x + ", " + z);
                }
            // The pastry case on the left: a glass top over a cake; the till on the right shows the customers (−Z) its screen.
            var (gl, gh) = Bounds(counter, Glass);
            Assert.That(gh.X, Is.LessThan(x0), name + ": the case left of the spot");
            Assert.That(gl.Y > InteriorModels.CounterTop + 0.1f && gh.Y <= InteriorModels.CounterHeight + 1e-3f, name + ": the glass over the case");
            var under = Hit(Tris(counter, Trim, Metal, Glow), new V3(-1.0f, 50, 0.06f), Down3, out float u, name);
            Assert.That(50 - u, Is.InRange(InteriorModels.CounterTop + 0.03f, gl.Y), name + ": a cake under the glass");
            var screen = Tris(counter, Glow);
            Assert.That(screen.All(t => IsColour(counter, t.Vertex, VoxelPalette.ScreenGreen) && counter.Normal(t.Vertex).Z < -0.5f), name + ": the till's screen faces the customers");
            Assert.That(Bounds(screen).min.X, Is.GreaterThan(x1), name + ": the till right of the spot");
            // The customers' side: eight dark raised fields between wooden stiles.
            Assert.That(Runs(counter, tris, -1.59f, 1.59f, 0.5f, VoxelPalette.WoodDark, name), Is.EqualTo(8), name + ": eight fields");
            Assert.That(Sees(counter, tris, new V3(-1.177f, 0.5f, -5), Ahead, VoxelPalette.Wood, name), name + ": a wooden stile");
        }

        [Test] public void TheCafeShelfStocksEveryLevelWithACloseRowOfGoods()
        {
            Assert.That(InteriorModels.ShelfLevels.Count, Is.EqualTo(4), "a plinth and three boards");
            for (int seed = 0; seed < 2; seed++)
            {
                var shelf = InteriorModels.CafeShelf(seed); var tris = Tris(shelf); string name = "cafe shelf " + seed;
                AssertInside(shelf, new V3(-1.5f, 0, -0.2f), new V3(1.5f, InteriorModels.ShelfHeight, 0.2f), name);
                var colours = new HashSet<(float, float)>();
                foreach (float level in InteriorModels.ShelfLevels)
                {
                    // The board under the goods' front, flat at its level.
                    var board = Hit(tris, new V3(0.3f, level + 0.3f, -0.15f), Down3, out float d, name);
                    Assert.That(level + 0.3f - d, Is.EqualTo(level).Within(1e-3f), name + ": the board at " + level);
                    Assert.That(IsColour(shelf, board.Vertex, VoxelPalette.WoodDark), name + ": a dark board at " + level);
                    // Goods along it: rays a little over the board meet a good in front of the back more often than not.
                    int rays = 0, goods = 0;
                    for (int i = 0; i <= 276; i++, rays++)
                    {
                        var hit = Hit(tris, new V3(-1.38f + 0.01f * i, level + 0.06f, -5), Ahead, out float far, name);
                        if (far - 5 < 0.1f) { goods++; colours.Add((shelf.Uvs[hit.Vertex * 2], shelf.Uvs[hit.Vertex * 2 + 1])); }
                    }
                    Assert.That(goods, Is.GreaterThan(rays * 0.45f), name + ": a close row at " + level);
                }
                Assert.That(colours.Count, Is.GreaterThanOrEqualTo(6), name + ": goods of many colours");
            }
            Assert.That(InteriorModels.CafeShelf(0).Positions, Is.Not.EqualTo(InteriorModels.CafeShelf(1).Positions), "two shelves, two rows");
        }

        [Test] public void CafeTablesAndChairsKeepTheHeightsOfTheirGuests()
        {
            var table = InteriorModels.CafeTable(); var tris = Tris(table); const string name = "cafe table";
            AssertInside(table, new V3(-0.4f, 0, -0.4f), new V3(0.4f, 0.85f, 0.4f), name);
            foreach (var (x, z) in new[] { (0.2f, 0.15f), (-0.3f, 0.2f), (0.25f, -0.25f) })
            {
                var top = Hit(tris, new V3(x, 50, z), Down3, out float d, name);
                Assert.That(50 - d, Is.EqualTo(PeopleModels.CafeTableTop).Within(1e-3f), name + ": the top where the guests' arms rest");
                Assert.That(IsColour(table, top.Vertex, VoxelPalette.WoodLight), name + ": a wooden top");
            }
            var cup = Hit(tris, new V3(-0.1f, 50, -0.1f), Down3, out float c, name);
            Assert.That(IsColour(table, cup.Vertex, VoxelPalette.BarkDark), name + ": a cup of coffee"); Assert.That(50 - c, Is.GreaterThan(PeopleModels.CafeTableTop + 0.03f), name + ": in its cup");
            Assert.That(Reach(tris, 0.01f, name), Is.GreaterThan(0.2f), name + ": a broad foot");
            Assert.That(Reach(tris, 0.4f, name), Is.LessThan(0.04f), name + ": a slim column");

            var chair = InteriorModels.CafeChair(); var seat = Tris(chair); const string s = "cafe chair";
            AssertInside(chair, new V3(-0.225f, 0, -0.225f), new V3(0.225f, 0.9f, 0.225f), s);
            foreach (var (x, z) in new[] { (0f, -0.1f), (0.1f, 0f), (-0.1f, 0.05f) })
            {
                var top = Hit(seat, new V3(x, 50, z), Down3, out float d, s);
                Assert.That(50 - d, Is.EqualTo(InteriorModels.CafeSeat).Within(1e-3f), s + ": the seat at " + x + ", " + z);
                Assert.That(IsColour(chair, top.Vertex, VoxelPalette.Wood), s + ": a wooden seat");
            }
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    var foot = Hit(seat, new V3(sx * 0.18f, -1, sz * 0.18f), Up, out float d, s);
                    Assert.That(d - 1, Is.LessThan(0.005f), s + ": a leg on the floor at " + sx + ", " + sz);
                    Assert.That(IsColour(chair, foot.Vertex, VoxelPalette.MetalDark), s + ": metal legs");
                }
            var back = Hit(seat, new V3(0, 0.77f, -5), Ahead, out float b, s);
            Assert.That(IsColour(chair, back.Vertex, VoxelPalette.Wood), s + ": a wooden back"); Assert.That(b - 5, Is.GreaterThan(0.12f), s + ": towards +Z");
        }

        [Test] public void TheMenuWritesItsTitleDishesAndPricesInChalkInsideItsFrame()
        {
            var menu = InteriorModels.MenuBoard(); var tris = Tris(menu); const string name = "menu board";
            AssertInside(menu, new V3(-0.6f, 0, -0.05f), new V3(0.6f, 0.9f, 0.05f), name);
            var title = Coloured(menu, VoxelPalette.Cream); var dishes = Coloured(menu, VoxelPalette.CarWhite); var prices = Coloured(menu, VoxelPalette.Amber);
            var (lo, hi) = Bounds(title.Concat(dishes).Concat(prices));
            Assert.That(lo.X >= -0.54f && hi.X <= 0.54f && lo.Y >= 0.06f && hi.Y <= 0.84f, name + ": the writing on the board inside the frame");
            Assert.That(title.Concat(dishes).Concat(prices).All(t => menu.Normal(t.Vertex).Z < -0.999f) && hi.Z < -0.03f, name + ": on the board's face");
            var (titleMin, titleMax) = Bounds(title);
            Assert.That(titleMin.Y, Is.GreaterThan(Bounds(dishes).max.Y), name + ": «МЕНЮ» over the dishes");
            Assert.That(Math.Abs(titleMin.X + titleMax.X) / 2, Is.LessThan(0.03f), name + ": a centred title");
            var tags = Pieces(prices);
            Assert.That(tags.Count, Is.EqualTo(4), name + ": four prices");
            Assert.That(Bounds(dishes).max.X, Is.LessThan(tags.Min(t => t.min.X)), name + ": the dishes left of their prices");
            Assert.That(Sees(menu, tris, new V3(0.57f, 0.45f, -5), Ahead, VoxelPalette.Wood, name), name + ": a wooden frame");
            Assert.That(Sees(menu, tris, new V3(0.45f, 0.62f, -5), Ahead, VoxelPalette.Ink, name), name + ": a dark board");
        }

        [Test] public void ThePendantLampHangsItsBulbInsideTheShade()
        {
            var lamp = InteriorModels.PendantLamp(); var tris = Tris(lamp); const string name = "pendant lamp";
            AssertWithin(lamp, new V3(-0.15f, -InteriorModels.LampDrop, -0.15f), new V3(0.15f, 0, 0.15f), name);
            Assert.That(Bounds(lamp).max.Y, Is.EqualTo(0).Within(1e-4f), name + ": from the ceiling");
            var glow = Tris(lamp, Glow); var (gl, gh) = Bounds(glow);
            Assert.That(glow.All(t => IsColour(lamp, t.Vertex, VoxelPalette.LampWarm)), name + ": a warm bulb");
            Assert.That(Hit(tris, new V3(0.004f, -2, 0.004f), Up, out _, name).Part, Is.EqualTo(Glow), name + ": from below, the bulb");
            Assert.That(Sees(lamp, tris, new V3(-2, (gl.Y + gh.Y) / 2, 0.004f), new V3(1, 0, 0), Metal, VoxelPalette.Brass, name), name + ": a brass shade round the bulb");
            Assert.That(Sees(lamp, tris, new V3(0.004f, -2, 0.12f), Up, VoxelPalette.Cream, name), name + ": cream inside the shade");
        }

        [Test] public void TheInteriorStaysWithinItsTriangleBudget()
        {
            var budgets = new Dictionary<string, int>
            {
                ["director desk"] = 800, ["employee desk"] = 550, ["supervisor desk"] = 550, ["office chair"] = 700, ["director monitor"] = 200,
                ["employee monitor"] = 200, ["keyboard"] = 900, ["mouse"] = 100, ["phone"] = 800, ["golden phone"] = 800, ["papers 1"] = 150, ["papers 2"] = 150,
                ["coffee machine"] = 1000, ["coffee cup"] = 250, ["coffee cabinet"] = 400, ["potted plant"] = 800, ["display pedestal"] = 320, ["door"] = 450,
                ["window frame"] = 150, ["ceiling light"] = 100, ["plaque"] = 100, ["elevator"] = 400, ["skirting"] = 50, ["cafe counter"] = 1100,
                ["cafe shelf 0"] = 2800, ["cafe shelf 1"] = 2800, ["cafe table"] = 500, ["cafe chair"] = 320, ["menu board"] = 150, ["pendant lamp"] = 400,
            };
            foreach (var (name, mesh) in Models()) Assert.That(mesh.TriangleCount, Is.LessThan(budgets[name]), name);
            // Everything the office places (one of the two desk telephones shows at a time; the supervisor's desk once one is
            // hired) and the café's furniture from the district plan with its espresso machine.
            long total = 0; int placed = 0;
            void Add(PolyMesh mesh, int count) { total += (long)mesh.TriangleCount * count; placed += count; }
            Add(InteriorModels.Desk(DeskStyle.Executive, 3.6f, 1.25f, VoxelPalette.Wood, VoxelPalette.WoodDark), 1);
            Add(InteriorModels.Desk(DeskStyle.Staff, 2.1f, 1.1f, VoxelPalette.WoodLight, VoxelPalette.Navy), 2);
            Add(InteriorModels.Desk(DeskStyle.Staff, 2, 0.8f, VoxelPalette.Wood, VoxelPalette.Navy), 1);
            Add(InteriorModels.OfficeChair(VoxelPalette.Navy), 3);
            Add(InteriorModels.Monitor(0.86f, 0.5f, 0.4f), 1); Add(InteriorModels.Monitor(0.58f, 0.39f, 0.27f), 2);
            Add(InteriorModels.Keyboard(), 1); Add(InteriorModels.Mouse(), 1);
            Add(InteriorModels.Papers(0.48f, 0.35f, 1), 1); Add(InteriorModels.Papers(0.4f, 0.3f, 2), 2);
            Add(InteriorModels.Phone(false), 1); Add(InteriorModels.Phone(true), 1); Add(InteriorModels.DisplayPedestal(), 1);
            Add(InteriorModels.CoffeeCabinet(), 1); Add(InteriorModels.CoffeeMachine(), 1); Add(InteriorModels.CoffeeCup(), 1); Add(InteriorModels.PottedPlant(4), 1);
            Add(InteriorModels.Door(), 1); Add(InteriorModels.WindowFrame(), 3); Add(InteriorModels.CeilingLight(), 3); Add(InteriorModels.Plaque(6, 0.45f), 1);
            Add(InteriorModels.ElevatorDoor(), 1); Add(InteriorModels.Skirting(12), 6);
            foreach (var item in CityScenery.Props)
                switch (item.Kind)
                {
                    case SceneryKind.CafeCounter: Add(InteriorModels.CafeCounter(), 1); Add(InteriorModels.CoffeeMachine(), 1); break;
                    case SceneryKind.CafeShelf: Add(InteriorModels.CafeShelf(item.Seed % 2), 1); break;
                    case SceneryKind.CafeTable: Add(InteriorModels.CafeTable(), 1); break;
                    case SceneryKind.CafeChair: Add(InteriorModels.CafeChair(), 1); break;
                    case SceneryKind.MenuBoard: Add(InteriorModels.MenuBoard(), 1); break;
                    case SceneryKind.PendantLamp: Add(InteriorModels.PendantLamp(), 1); break;
                }
            Assert.That(placed, Is.GreaterThan(45));
            Assert.That(total, Is.LessThan(21000), "the low-poly interior: " + total + " triangles");
        }
    }
}
