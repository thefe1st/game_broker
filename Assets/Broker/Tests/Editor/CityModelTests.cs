using System;
using System.Collections.Generic;
using System.Linq;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    // The low-poly Exchange, the kiosk, the tunnel portal, the skyline and the hedge that replace the voxel models: sound
    // meshes inside the voxel models' footprints, steps and floors at the heights the controller walks on, open doors and
    // a mouth where the scene expects them.
    public sealed class CityModelTests
    {
        private const int Wall = (int)BuildingPart.Wall, Trim = (int)BuildingPart.Trim, Glass = (int)BuildingPart.Glass, Night = (int)BuildingPart.Night,
            Glow = (int)BuildingPart.Glow, Metal = (int)BuildingPart.Metal;
        private static readonly int[] All = { Wall, Trim, Glass, Night, Glow, Metal };
        private static readonly float[] HedgeLengths = { 0.25f, 1, 3.5f, 9.2f, 26 };

        private static IEnumerable<(string name, PolyMesh mesh)> Models()
        {
            yield return ("podium", BuildingModels.ExchangePodium());
            yield return ("terrace", BuildingModels.ExchangeTerrace());
            yield return ("hall floor", BuildingModels.ExchangeHallFloor());
            yield return ("hall", BuildingModels.ExchangeHall());
            yield return ("roof", BuildingModels.ExchangeRoof());
            yield return ("column", BuildingModels.ExchangeColumn());
            yield return ("quote board", BuildingModels.QuoteBoard());
            yield return ("bull", BuildingModels.ExchangeStatue(true));
            yield return ("bear", BuildingModels.ExchangeStatue(false));
            yield return ("rostral column", BuildingModels.RostralColumn());
            yield return ("kiosk", BuildingModels.Kiosk());
            yield return ("tunnel portal", BuildingModels.TunnelPortal());
            for (int k = 0; k < BuildingModels.SkylineLooks.Length; k++) yield return ("skyline " + k, BuildingModels.Skyline(k));
            foreach (float length in HedgeLengths) yield return ("hedge " + length, StreetModels.Hedge(length, 1));
        }

        private struct Tri { public V3 A, B, C; public int Part; }
        private static List<Tri> Tris(PolyMesh mesh, params int[] parts)
        {
            var list = new List<Tri>();
            foreach (int part in parts.Length > 0 ? parts : All)
            {
                var t = mesh.Triangles[part];
                for (int i = 0; i < t.Length; i += 3) list.Add(new Tri { A = mesh.Position(t[i]), B = mesh.Position(t[i + 1]), C = mesh.Position(t[i + 2]), Part = part });
            }
            return list;
        }
        private static (V3 min, V3 max) Bounds(PolyMesh mesh, params int[] parts)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue, z1 = float.MinValue;
            foreach (int part in parts.Length > 0 ? parts : All)
                foreach (int v in mesh.Triangles[part])
                {
                    var p = mesh.Position(v);
                    x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); z0 = Math.Min(z0, p.Z); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); z1 = Math.Max(z1, p.Z);
                }
            return (new V3(x0, y0, z0), new V3(x1, y1, z1));
        }
        // Inside the box, standing on its floor (y = 0) unless told otherwise.
        private static void AssertInside(PolyMesh mesh, V3 min, V3 max, string name, bool grounded = true)
        {
            const float e = 1e-3f;
            var (lo, hi) = Bounds(mesh);
            Assert.That(lo.X, Is.GreaterThanOrEqualTo(min.X - e), name + " left"); Assert.That(hi.X, Is.LessThanOrEqualTo(max.X + e), name + " right");
            Assert.That(lo.Z, Is.GreaterThanOrEqualTo(min.Z - e), name + " front"); Assert.That(hi.Z, Is.LessThanOrEqualTo(max.Z + e), name + " back");
            Assert.That(lo.Y, Is.GreaterThanOrEqualTo(min.Y - e), name + " bottom"); Assert.That(hi.Y, Is.LessThanOrEqualTo(max.Y + e), name + " top");
            if (grounded) Assert.That(lo.Y, Is.EqualTo(0).Within(e), name + " stands on its pivot");
        }
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
        private static bool Hit(List<Tri> tris, V3 origin, V3 dir, out Tri hit, out float distance)
        {
            hit = default; distance = float.MaxValue; bool any = false;
            foreach (var t in tris)
                if (Meets(t, origin, dir, out float d) && d < distance) { hit = t; distance = d; any = true; }
            return any;
        }
        private static float Down(List<Tri> tris, float x, float z, string name)
        {
            Assert.That(Hit(tris, new V3(x, 500, z), new V3(0, -1, 0), out _, out float d), Is.True, name);
            return 500 - d;
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
                Assert.That(mesh.TriangleCount, Is.GreaterThan(0), name);
                if (name != "quote board") Assert.That(mesh.Triangles[Wall].Length, Is.GreaterThan(0), name + " shows its wall material");
                AssertSame(mesh, again[index++].mesh, name + " is deterministic");
            }
            // Brass, bronze and flames on their own materials.
            foreach (var mesh in new[] { BuildingModels.ExchangeStatue(true), BuildingModels.ExchangeStatue(false), BuildingModels.RostralColumn(), BuildingModels.QuoteBoard() })
                Assert.That(mesh.Triangles[Metal].Length, Is.GreaterThan(0));
            foreach (var mesh in new[] { BuildingModels.RostralColumn(), BuildingModels.QuoteBoard(), BuildingModels.TunnelPortal() })
                Assert.That(mesh.Triangles[Glow].Length, Is.GreaterThan(0));
            Assert.That(BuildingModels.ExchangeStatue(true).Positions, Is.Not.EqualTo(BuildingModels.ExchangeStatue(false).Positions), "a bull and a bear");
        }
        [Test] public void ModelsStayInsideTheVoxelModelsFootprints()
        {
            float px = BuildingModels.PodiumHalf + 0.1f, top = BuildingModels.PodiumTop;
            AssertInside(BuildingModels.ExchangePodium(), new V3(-px, 0, -0.1f), new V3(px, top + 0.6f, BuildingModels.PodiumDepth + 0.1f), "podium");
            Assert.That(Bounds(BuildingModels.ExchangePodium()).max.Y, Is.GreaterThan(top + 0.3f), "a parapet round the podium");
            float hx = BuildingModels.HallWidth / 2, hd = BuildingModels.HallDepth, hh = BuildingModels.HallHeight;
            var (wallMin, wallMax) = Bounds(BuildingModels.ExchangeHall(), Wall);
            Assert.That(wallMin.X, Is.EqualTo(-hx).Within(1e-3f)); Assert.That(wallMax.X, Is.EqualTo(hx).Within(1e-3f));
            Assert.That(wallMin.Z, Is.EqualTo(0).Within(1e-3f)); Assert.That(wallMax.Z, Is.EqualTo(hd).Within(1e-3f)); Assert.That(wallMax.Y, Is.EqualTo(hh).Within(1e-3f));
            AssertInside(BuildingModels.ExchangeHall(), new V3(-hx - 0.15f, 0, -0.15f), new V3(hx + 0.15f, hh, hd + 0.15f), "hall");
            float rx = BuildingModels.RoofWidth / 2;
            AssertInside(BuildingModels.ExchangeRoof(), new V3(-rx, 0, -0.15f), new V3(rx, BuildingModels.RoofRidge + 0.2f, BuildingModels.RoofDepth + 0.15f), "roof");
            float c = BuildingModels.ColumnHalf;
            AssertInside(BuildingModels.ExchangeColumn(), new V3(-c, 0, -c), new V3(c, BuildingModels.ColumnHeight, c), "column");
            Assert.That(Bounds(BuildingModels.ExchangeColumn()).max.Y, Is.EqualTo(BuildingModels.ColumnHeight).Within(1e-3f), "the columns carry the roof");
            float bx = BuildingModels.BoardWidth / 2;
            AssertInside(BuildingModels.QuoteBoard(), new V3(-bx, 0, BuildingModels.BoardRim), new V3(bx, BuildingModels.BoardHeight, 0.1f), "quote board");
            foreach (bool bull in new[] { true, false })
            {
                var statue = BuildingModels.ExchangeStatue(bull); string name = bull ? "bull" : "bear";
                AssertInside(statue, new V3(-BuildingModels.StatueHalfX, 0, -BuildingModels.StatueHalfZ), new V3(BuildingModels.StatueHalfX, BuildingModels.StatueHeight, BuildingModels.StatueHalfZ), name);
                Assert.That(Bounds(statue, Metal).max.Y, Is.GreaterThan(2), name + " stands on its pedestal");
            }
            float r = BuildingModels.RostralHalf;
            AssertInside(BuildingModels.RostralColumn(), new V3(-r, 0, -r), new V3(r, BuildingModels.RostralHeight, r), "rostral column");
            Assert.That(Bounds(BuildingModels.RostralColumn(), Glow).max.Y, Is.GreaterThan(BuildingModels.RostralHeight - 1), "the flame burns on top");
            AssertInside(BuildingModels.Kiosk(), new V3(-BuildingModels.KioskHalfX - 0.1f, 0, -BuildingModels.KioskHalfZ - 0.1f),
                new V3(BuildingModels.KioskHalfX + 0.1f, BuildingModels.KioskHeight, BuildingModels.KioskHalfZ + 0.1f), "kiosk");
            float w = BuildingModels.PortalWidth / 2 + 0.1f, d = BuildingModels.PortalDepth / 2;
            AssertInside(BuildingModels.TunnelPortal(), new V3(-w, 0, -d - 0.2f), new V3(w, BuildingModels.PortalHeight, d + 0.1f), "tunnel portal");
        }
        // The controller walks the plan's floors (and a ramp over the steps); the meshes show them at the same heights.
        [Test] public void StepsTerraceAndHallFloorLieWhereThePlanWalks()
        {
            var podium = Tris(BuildingModels.ExchangePodium()); var terrace = Tris(BuildingModels.ExchangeTerrace()); var floor = Tris(BuildingModels.ExchangeHallFloor());
            float top = BuildingModels.PodiumTop, depth = BuildingModels.StairRun / BuildingModels.Steps;
            Assert.That(top, Is.EqualTo(CityDistrictPlan.PodiumFloorY + CityBlockPlan.GroundY - CityBlockPlan.TerrainTopY).Within(1e-4f));
            for (int i = 0; i < BuildingModels.Steps; i++)
            {
                float z = BuildingModels.TreadFront(i) + depth / 2, tread = BuildingModels.TreadHeight(i);
                Assert.That(Down(podium, BuildingModels.StairHalf - 1, z, "step " + i), Is.EqualTo(tread).Within(1e-3f), "tread " + i);
                Assert.That(Down(podium, 0, z, "runner " + i), Is.InRange(tread, tread + 0.02f), "the runner lies on tread " + i);
                if (i > 0) Assert.That(tread - BuildingModels.TreadHeight(i - 1), Is.EqualTo(BuildingModels.Riser).Within(1e-4f));
            }
            // Over the square (+0.1 m over the terrain) the steps climb to the podium floor in even risers.
            Assert.That(BuildingModels.TreadHeight(0) - (CityBlockPlan.GroundY - CityBlockPlan.TerrainTopY), Is.EqualTo(BuildingModels.Riser + 0.1f).Within(1e-4f));
            Assert.That(top - BuildingModels.TreadHeight(BuildingModels.Steps - 1), Is.EqualTo(BuildingModels.Riser).Within(1e-4f));
            Assert.That(BuildingModels.TreadFront(BuildingModels.Steps), Is.EqualTo(BuildingModels.StairRun).Within(1e-4f));
            // The paving on the cheeks and round the hall, the parquet inside it, the runner up to the board.
            float hallZ = BuildingModels.HallFrontZ - CityDistrictPlan.StepsMinZ, inside = hallZ + BuildingModels.HallDepth / 2;
            Assert.That(Down(terrace, 11, 1, "cheek"), Is.EqualTo(top).Within(1e-4f));
            Assert.That(Down(terrace, 11, inside, "side terrace"), Is.EqualTo(top).Within(1e-4f));
            Assert.That(Down(terrace, 0, BuildingModels.PodiumDepth - 1, "back terrace"), Is.EqualTo(top).Within(1e-4f));
            Assert.That(Hit(terrace, new V3(2, 50, inside), new V3(0, -1, 0), out _, out _), Is.False, "no paving inside the hall");
            Assert.That(Down(floor, 2, inside, "hall floor"), Is.EqualTo(top).Within(1e-4f));
            Assert.That(Hit(terrace, new V3(0, 50, BuildingModels.StairRun + 1), new V3(0, -1, 0), out _, out _), Is.False, "no paving under the runner");
            Assert.That(Down(podium, 0, BuildingModels.StairRun + 1, "runner on the terrace"), Is.InRange(top, top + 0.02f));
            Assert.That(Down(podium, 0, BuildingModels.RunnerEndZ - 0.2f, "runner in the hall"), Is.InRange(top, top + 0.02f));
            Assert.That(BuildingModels.RunnerEndZ, Is.LessThan(hallZ + BuildingModels.HallDepth - BuildingModels.HallWall - 1), "the runner stops short of the board");
            // Paving and parquet cover the top once: its area less the stairs and the strip of runner outside the hall.
            double area = 0;
            foreach (var t in terrace.Concat(floor)) area += V3.Cross(t.B - t.A, t.C - t.A).Length / 2;
            float strip = 2 * (BuildingModels.RunnerHalf + BuildingModels.RunnerEdge) * (hallZ + BuildingModels.HallWall - BuildingModels.StairRun);
            Assert.That(area, Is.EqualTo(2 * BuildingModels.PodiumHalf * BuildingModels.PodiumDepth - 2 * BuildingModels.StairHalf * BuildingModels.StairRun - strip).Within(0.01));
        }
        [Test] public void TheHallIsOpenAtItsDoorAndGlazedThroughItsWalls()
        {
            var hall = Tris(BuildingModels.ExchangeHall());
            float x0 = -BuildingModels.HallWidth / 2, back = BuildingModels.HallDepth - BuildingModels.HallWall;
            // Through the door the ray reaches the inside of the back wall.
            float door = x0 + (BuildingModels.HallDoor.U0 + BuildingModels.HallDoor.U1) / 2;
            foreach (float y in new[] { 0.3f, 2f, BuildingModels.HallDoorTop - 0.2f })
            {
                Assert.That(Hit(hall, new V3(door, y, -1), new V3(0, 0, 1), out _, out float d), Is.True);
                Assert.That(d, Is.EqualTo(back + 1).Within(1e-3f), "the door is open at " + y);
            }
            Assert.That(BuildingModels.HallDoor.U1 - BuildingModels.HallDoor.U0, Is.EqualTo(3).Within(1e-4f));
            // Beside it the front wall is solid.
            Assert.That(Hit(hall, new V3(x0 + 1, 1, -1), new V3(0, 0, 1), out _, out float wall), Is.True);
            Assert.That(wall, Is.EqualTo(1).Within(1e-3f), "solid front wall");
            // A front window (beside its mullion, under its transom): the glass from outside, and from the room the same
            // window glazed on its inner side.
            Assert.That(Hit(hall, new V3(x0 + 4, 3, -1), new V3(0, 0, 1), out var pane, out _), Is.True);
            Assert.That(pane.Part, Is.EqualTo(Night), "glass that lights at night");
            Assert.That(Hit(hall, new V3(x0 + 4, 3, 6), new V3(0, 0, -1), out var inner, out float fromRoom), Is.True);
            Assert.That(inner.Part, Is.EqualTo(Night), "the window seen from the room");
            Assert.That(fromRoom, Is.InRange(6 - BuildingModels.HallWall, 6 - 0.1f), "set in the wall");
            // The side walls have their windows too, and the room is closed above the sills.
            Assert.That(Tris(BuildingModels.ExchangeHall(), Night).Count(t => Math.Abs(t.A.X - t.B.X) < 1e-4f && Math.Abs(t.A.X - t.C.X) < 1e-4f), Is.GreaterThan(0), "side windows");
            Assert.That(Hit(hall, new V3(0, 6.6f, 6), new V3(1, 0, 0), out _, out float side), Is.True);
            Assert.That(side, Is.InRange(BuildingModels.HallWidth / 2 - BuildingModels.HallWall - 0.2f, BuildingModels.HallWidth / 2 - BuildingModels.HallWall + 1e-3f), "inner side wall");
        }
        [Test] public void TheRoofKeepsAPlainFriezeOverTheColumns()
        {
            var roof = Tris(BuildingModels.ExchangeRoof());
            // A plain band 0.2 m behind the front edge at 0.75 m (the name came off it in stage 6), triglyphs only further out.
            foreach (float x in new[] { -4.5f, -2f, 0f, 2f, 4.5f })
            {
                Assert.That(Hit(roof, new V3(x, 0.75f, -1), new V3(0, 0, 1), out var frieze, out float d), Is.True);
                Assert.That(frieze.Part, Is.EqualTo(Wall), "plain frieze at " + x);
                Assert.That(d - 1, Is.GreaterThan(0.2f), "the frieze set back at " + x);
            }
            Assert.That(Hit(roof, new V3(BuildingModels.RoofWidth / 2 - 2.2f, 0.75f, -1), new V3(0, 0, 1), out var glyph, out _), Is.True);
            Assert.That(glyph.Part, Is.EqualTo(Trim), "triglyphs further out");
            // A gable over the middle, eaves at the sides.
            Assert.That(Down(roof, 0, BuildingModels.RoofDepth / 2, "ridge"), Is.InRange(BuildingModels.RoofRidge, BuildingModels.RoofRidge + 0.2f));
            Assert.That(Down(roof, BuildingModels.RoofWidth / 2 - 0.3f, BuildingModels.RoofDepth / 2, "eaves"), Is.LessThan(BuildingModels.RoofEave + 0.5f));
        }
        [Test] public void FacesWhereTheSceneHangsItsText()
        {
            // The board's rows at BoardFace for the cell texts, the ticker glowing low on it.
            var board = Tris(BuildingModels.QuoteBoard());
            Assert.That(Hit(board, new V3(0, 1.6f, -1), new V3(0, 0, 1), out _, out float face), Is.True);
            Assert.That(face - 1, Is.EqualTo(BuildingModels.BoardFace).Within(1e-3f));
            Assert.That(Bounds(BuildingModels.QuoteBoard(), Glow).max.Y, Is.LessThan(0.6f), "ticker along the bottom");
            // The kiosk's sign band faces the square 0.8 m in front of its pivot, under its newspaper pictogram.
            var kiosk = Tris(BuildingModels.Kiosk());
            Assert.That(Hit(kiosk, new V3(0, 2.25f, -2), new V3(0, 0, 1), out var band, out float k), Is.True);
            Assert.That(k - 2, Is.EqualTo(-BuildingModels.KioskHalfZ).Within(0.01f), "sign band");
            Assert.That(band.Part, Is.EqualTo(Trim));
            Assert.That(Tris(BuildingModels.Kiosk(), Glass).Count, Is.GreaterThan(0), "a glazed display");
        }
        [Test] public void TheTunnelMouthIsOpenOverTheCarriageway()
        {
            var portal = Tris(BuildingModels.TunnelPortal());
            float m = BuildingModels.MouthHalf;
            foreach (float x in new[] { -m + 0.3f, 0, m - 0.3f })
                foreach (float y in new[] { 0.2f, 3, CityScenery.TunnelHeight - 0.2f })
                    Assert.That(Hit(portal, new V3(x, y, -3), new V3(0, 0, 1), out _, out _), Is.False, "open at " + x + ", " + y);
            Assert.That(Hit(portal, new V3(0, 1, 0), new V3(0, 1, 0), out _, out float soffit), Is.True);
            Assert.That(1 + soffit, Is.EqualTo(CityScenery.TunnelHeight).Within(1e-3f), "the mouth's soffit");
            Assert.That(Hit(portal, new V3(m + 1.5f, 3, -3), new V3(0, 0, 1), out _, out float wall), Is.True);
            Assert.That(wall, Is.InRange(3 - BuildingModels.PortalDepth / 2 - 0.1f, 3 - BuildingModels.PortalDepth / 2 + 1e-3f), "solid beside the mouth");
            Assert.That(2 * m, Is.EqualTo(CityScenery.TunnelMaxZ - CityScenery.TunnelMinZ).Within(1e-3f), "as wide as the tunnel");
        }
        [Test] public void HedgesRunTheirLengthWithSeededBumps()
        {
            foreach (float length in HedgeLengths)
            {
                var hedge = StreetModels.Hedge(length, 2); string name = "hedge " + length;
                var (min, max) = Bounds(hedge);
                Assert.That(min.X, Is.EqualTo(-length / 2).Within(1e-3f), name); Assert.That(max.X, Is.EqualTo(length / 2).Within(1e-3f), name);
                Assert.That(min.Y, Is.EqualTo(0).Within(1e-4f), name);
                Assert.That(max.Y, Is.InRange(StreetModels.HedgeHeight - 0.05f, StreetModels.HedgeHeight + 0.05f), name);
                Assert.That(Math.Max(-min.Z, max.Z), Is.InRange(StreetModels.HedgeDepth / 2, StreetModels.HedgeDepth / 2 + 0.05f), name);
                Assert.That(hedge.TriangleCount, Is.LessThan(150 + 90 * length), name + " stays cheap");
                Assert.That(Down(Tris(hedge), 0, 0, name), Is.InRange(StreetModels.HedgeHeight - 0.05f, StreetModels.HedgeHeight + 0.05f), name + " top");
            }
            // The seed moves the bumps, not the size.
            var a = StreetModels.Hedge(5, 0); var b = StreetModels.Hedge(5, 1);
            Assert.That(a.Positions, Is.Not.EqualTo(b.Positions));
            Assert.That(a.TriangleCount, Is.EqualTo(b.TriangleCount));
            Assert.That(StreetModels.HedgeLook.Surface, Is.EqualTo("leaves")); Assert.That(VoxelSurfaces.Has("leaves"));
            foreach (float bad in new[] { 0.2f, 0, -1, float.NaN, float.PositiveInfinity, 101 })
                Assert.Throws<ArgumentOutOfRangeException>(() => StreetModels.Hedge(bad, 0), bad.ToString());
        }
        [Test] public void TheSkylineShowsEveryDistantBlockOnce()
        {
            for (int shade = 0; shade < BuildingModels.SkylineLooks.Length; shade++)
            {
                var mesh = BuildingModels.Skyline(shade);
                var blocks = CityScenery.Skyline.Where(s => s.Shade == shade).ToList();
                Assert.That(blocks.Count, Is.GreaterThan(0));
                Assert.That(mesh.Triangles[Wall].Length / 3, Is.EqualTo(8 * blocks.Count), "four walls a block, shade " + shade);
                Assert.That(mesh.Shades.All(s => s == 255), "evenly lit, shade " + shade);
                var tris = Tris(mesh);
                foreach (var block in blocks)
                {
                    float roof = Down(tris, block.X + block.Width / 2 - 0.5f, block.Z, "block at " + block.X + ", " + block.Z);
                    Assert.That(roof, Is.EqualTo(block.Height).Within(1e-3f), "roof of the block at " + block.X + ", " + block.Z);
                }
                Assert.That(VoxelSurfaces.Has(BuildingModels.SkylineLooks[shade].Surface));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => BuildingModels.Skyline(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => BuildingModels.Skyline(BuildingModels.SkylineLooks.Length));
        }
        [Test] public void LooksUseKnownSurfacesAndTheTrianglesStayCheap()
        {
            foreach (var look in new[] { BuildingModels.ExchangeGranite, BuildingModels.ExchangeDarkGranite, BuildingModels.ExchangePaving, BuildingModels.ExchangeParquet,
                BuildingModels.ExchangeWalls, BuildingModels.ExchangeStone, BuildingModels.KioskWall, BuildingModels.PortalConcrete, StreetModels.HedgeLook })
                Assert.That(VoxelSurfaces.Has(look.Surface), look.ToString());
            Assert.That(BuildingModels.ExchangeGranite.Surface, Is.EqualTo("ashlar"));
            int columns = CityScenery.Props.Count(p => p.Kind == SceneryKind.ExchangeColumn), rostral = CityScenery.Props.Count(p => p.Kind == SceneryKind.RostralColumn);
            Assert.That(columns, Is.GreaterThan(0)); Assert.That(rostral, Is.EqualTo(2));
            int exchange = BuildingModels.ExchangePodium().TriangleCount + BuildingModels.ExchangeTerrace().TriangleCount + BuildingModels.ExchangeHallFloor().TriangleCount
                + BuildingModels.ExchangeHall().TriangleCount + BuildingModels.ExchangeRoof().TriangleCount + BuildingModels.QuoteBoard().TriangleCount
                + BuildingModels.ExchangeStatue(true).TriangleCount + BuildingModels.ExchangeStatue(false).TriangleCount
                + columns * BuildingModels.ExchangeColumn().TriangleCount + rostral * BuildingModels.RostralColumn().TriangleCount;
            Assert.That(exchange, Is.LessThan(30000), "the Exchange with its square");
            foreach (var (name, mesh, budget) in new[] { ("column", BuildingModels.ExchangeColumn(), 1200), ("bull", BuildingModels.ExchangeStatue(true), 2000),
                ("rostral column", BuildingModels.RostralColumn(), 2000), ("kiosk", BuildingModels.Kiosk(), 800), ("tunnel portal", BuildingModels.TunnelPortal(), 800) })
                Assert.That(mesh.TriangleCount, Is.LessThan(budget), name);
            int skyline = 0;
            for (int k = 0; k < BuildingModels.SkylineLooks.Length; k++) skyline += BuildingModels.Skyline(k).TriangleCount;
            Assert.That(skyline, Is.LessThan(6000), "the skyline");
        }
    }
}
