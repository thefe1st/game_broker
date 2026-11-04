using System;
using System.Collections.Generic;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class BuildingModelTests
    {
        private const int Wall = (int)BuildingPart.Wall, Trim = (int)BuildingPart.Trim, Glass = (int)BuildingPart.Glass, Night = (int)BuildingPart.Night;
        // Outer walls by the way they face: front −Z, back +Z, left −X, right +X.
        private const int Front = 0, Back = 1, Left = 2, Right = 3;
        private static readonly TowerStyle[] Styles = (TowerStyle[])Enum.GetValues(typeof(TowerStyle));
        private static readonly ShopStyle[] ShopStyles = (ShopStyle[])Enum.GetValues(typeof(ShopStyle));

        // The towers of the scene with the seeds it gives them: the plan's blocks north of the boulevard and the ring's frontage.
        private sealed class TowerCase
        {
            public string Name; public float Width, Depth, Height; public PolyMesh Mesh;
        }
        private static List<TowerCase> towers;
        private static List<TowerCase> Towers()
        {
            if (towers != null) return towers;
            var list = new List<TowerCase>();
            int number = 0;
            foreach (var b in new CityBlockPlan().Buildings)
            {
                list.Add(new TowerCase { Name = "tower " + number, Width = b.Width, Depth = b.Depth, Height = b.Height,
                    Mesh = BuildingModels.Tower(b.Width, b.Depth, b.Height, (TowerStyle)b.Style, number) });
                number++;
            }
            var frontage = CityRoads.Frontage;
            for (int i = 0; i < frontage.Count; i++)
            {
                var b = frontage[i];
                list.Add(new TowerCase { Name = "ring tower " + i, Width = b.Front, Depth = b.Depth, Height = b.Height,
                    Mesh = BuildingModels.Tower(b.Front, b.Depth, b.Height, (TowerStyle)b.Style, 40 + i) });
            }
            return towers = list;
        }
        // The block under the office: two floors on a plinth, no shop front and no roof plant, windows on the back too.
        private static PolyMesh Office() => BuildingModels.Tower(31, 19.5f, 7.5f, TowerStyle.Plaster, 5, false, false, true);
        private static IEnumerable<(string name, PolyMesh mesh)> SceneModels()
        {
            foreach (var t in Towers()) yield return (t.Name, t.Mesh);
            yield return ("office block", Office());
            foreach (var style in ShopStyles)
            {
                yield return (style + " street building", BuildingModels.Shop(style, 1, true));
                yield return (style + " street building without back windows", BuildingModels.Shop(style, 2));
            }
            yield return ("café", BuildingModels.Cafe());
        }

        private struct Tri
        {
            public V3 A, B, C, Face; public int Part;
            public V3 Centre => (A + B + C) * (1f / 3);
            public double Area => Face.Length / 2;
        }
        // Triangles of the given parts with their outward face vectors (cross(b − a, c − a), twice the area long).
        private static IEnumerable<Tri> Tris(PolyMesh mesh, params int[] parts)
        {
            foreach (int part in parts)
            {
                var t = mesh.Triangles[part];
                for (int i = 0; i < t.Length; i += 3)
                {
                    V3 a = mesh.Position(t[i]), b = mesh.Position(t[i + 1]), c = mesh.Position(t[i + 2]);
                    yield return new Tri { A = a, B = b, C = c, Face = V3.Cross(b - a, c - a), Part = part };
                }
            }
        }
        private static int Side(V3 n) => Math.Abs(n.Z) >= Math.Abs(n.X) ? (n.Z < 0 ? Front : Back) : (n.X < 0 ? Left : Right);
        private static bool Vertical(V3 n) => Math.Abs(n.Y) <= 1e-3f * n.Length;
        private static (V3 min, V3 max) Bounds(PolyMesh mesh, params int[] parts)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue, z1 = float.MinValue;
            foreach (int part in parts)
                foreach (int v in mesh.Triangles[part])
                {
                    var p = mesh.Position(v);
                    x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); z0 = Math.Min(z0, p.Z); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); z1 = Math.Max(z1, p.Z);
                }
            return (new V3(x0, y0, z0), new V3(x1, y1, z1));
        }
        private static HashSet<string> Centres(PolyMesh mesh, int part)
        {
            var set = new HashSet<string>();
            foreach (var t in Tris(mesh, part)) { var c = t.Centre; set.Add(Math.Round(c.X, 2) + " " + Math.Round(c.Y, 2) + " " + Math.Round(c.Z, 2)); }
            return set;
        }
        private static void AssertSame(PolyMesh a, PolyMesh b, string name)
        {
            Assert.That(a.Positions, Is.EqualTo(b.Positions), name); Assert.That(a.Normals, Is.EqualTo(b.Normals), name);
            Assert.That(a.Uvs, Is.EqualTo(b.Uvs), name); Assert.That(a.Shades, Is.EqualTo(b.Shades), name);
            for (int p = 0; p < BuildingModels.PartCount; p++) Assert.That(a.Triangles[p], Is.EqualTo(b.Triangles[p]), name + " part " + p);
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
        private static bool Nearest(List<Tri> tris, V3 origin, V3 dir, float reach, out Tri hit, out float distance)
        {
            hit = default; distance = reach; bool any = false;
            foreach (var t in tris)
                if (Meets(t, origin, dir, out float d) && d < distance) { hit = t; distance = d; any = true; }
            return any;
        }

        [Test] public void MeshesAreWellFormedWithUnitNormalsThatAgreeWithTheirFaces()
        {
            Assert.That(Enum.GetValues(typeof(BuildingPart)).Length, Is.EqualTo(BuildingModels.PartCount));
            foreach (var (name, mesh) in SceneModels())
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
                        // Unity front faces: cross(b − a, c − a) points out, the side every corner normal is on.
                        for (int k = 0; k < 3; k++) if (V3.Dot(face, mesh.Normal(tris[t + k])) <= 0) { flipped++; break; }
                    }
                }
                Assert.That(badNormal, Is.Zero, name + ": unit normals"); Assert.That(dark, Is.Zero, name + ": the shade keeps some light");
                Assert.That(outside, Is.Zero, name + ": indices in range"); Assert.That(degenerate, Is.Zero, name + ": no degenerate triangles");
                Assert.That(flipped, Is.Zero, name + ": normals on the outer side of their triangles");
                foreach (var part in new[] { Wall, Trim, Glass, Night }) Assert.That(mesh.Triangles[part].Length, Is.GreaterThan(0), name + " part " + part);
            }
        }
        [Test] public void ModelsAreDeterministicAndTheSeedChangesTheWindows()
        {
            foreach (var style in Styles)
            {
                PolyMesh a = BuildingModels.Tower(10, 11, 21, style, 7), b = BuildingModels.Tower(10, 11, 21, style, 7), c = BuildingModels.Tower(10, 11, 21, style, 8);
                AssertSame(a, b, style.ToString());
                Assert.That(Centres(c, Night), Is.Not.EquivalentTo(Centres(a, Night)), style + ": another seed lights other windows");
            }
            foreach (var style in ShopStyles) AssertSame(BuildingModels.Shop(style, 3, true), BuildingModels.Shop(style, 3, true), style.ToString());
            AssertSame(BuildingModels.Cafe(), BuildingModels.Cafe(), "café");
        }
        // Walls on the footprint from the ground to the parapet; cornices, sills and caps stand out a little, an awning or a
        // canopy over the pavement in front of a shop floor, roof plant and a mast at most 4 m over the roof.
        [Test] public void TowersStandOnTheirFootprintsAndReachTheirHeights()
        {
            foreach (var t in Towers()) AssertTowerBounds(t.Mesh, t.Width, t.Depth, t.Height, true, true, t.Name);
            AssertTowerBounds(Office(), 31, 19.5f, 7.5f, false, false, "office block");
            foreach (var style in Styles)
            {
                AssertTowerBounds(BuildingModels.Tower(12, 9, 20, style, 4, false, false), 12, 9, 20, false, false, style + " without a shop floor or plant");
                AssertTowerBounds(BuildingModels.Tower(9, 8, 30, style, 4), 9, 8, 30, true, true, style + " with a mast");
            }
        }
        private static void AssertTowerBounds(PolyMesh mesh, float w, float d, float h, bool shopFloor, bool plant, string name)
        {
            float x = w / 2, z = d / 2, proud = BuildingModels.CorniceProud, roof = h + BuildingModels.ParapetHeight;
            var (wallMin, wallMax) = Bounds(mesh, Wall);
            Assert.That(wallMin.X, Is.EqualTo(-x).Within(1e-3f), name); Assert.That(wallMax.X, Is.EqualTo(x).Within(1e-3f), name);
            Assert.That(wallMin.Z, Is.EqualTo(-z).Within(1e-3f), name); Assert.That(wallMax.Z, Is.EqualTo(z).Within(1e-3f), name);
            Assert.That(wallMin.Y, Is.EqualTo(0).Within(1e-4f), name); Assert.That(wallMax.Y, Is.EqualTo(roof).Within(1e-3f), name + " parapet");
            var (min, max) = Bounds(mesh, Wall, Trim, Glass, Night);
            Assert.That(min.Y, Is.EqualTo(0).Within(1e-4f), name + " stands on the ground");
            Assert.That(-min.X, Is.EqualTo(x + proud).Within(1e-3f), name + " cornice"); Assert.That(max.X, Is.EqualTo(x + proud).Within(1e-3f), name + " cornice");
            Assert.That(max.Z, Is.EqualTo(z + proud).Within(1e-3f), name + " cornice at the back");
            if (shopFloor) Assert.That(-min.Z, Is.InRange(z + 1.1f - 1e-3f, z + 1.2f + 1e-3f), name + ": an awning or a canopy over the pavement");
            else Assert.That(-min.Z, Is.EqualTo(z + proud).Within(1e-3f), name + " cornice in front");
            if (plant) Assert.That(max.Y, Is.InRange(roof + BuildingModels.CapHeight + 0.05f, h + 4 + 1e-3f), name + ": roof plant over the parapet");
            else Assert.That(max.Y, Is.EqualTo(roof + BuildingModels.CapHeight).Within(1e-3f), name + ": only the parapet cap over the roof");
        }
        // Glass stands upright, set back in its reveal; every floor has windows on the front and both sides, the back only
        // when asked (the scene's towers stand back to back; the office block shows its back to the ring road).
        [Test] public void EveryFloorHasWindowsOnTheFrontAndSidesAndTheBackOnlyWhenAsked()
        {
            foreach (var t in Towers()) AssertWindows(t.Mesh, t.Width, t.Depth, t.Height, true, false, t.Name);
            AssertWindows(Office(), 31, 19.5f, 7.5f, false, true, "office block");
            foreach (var style in Styles) AssertWindows(BuildingModels.Tower(14, 12, 18, style, 9, true, true, true), 14, 12, 18, true, true, style + " with back windows");
        }
        private static void AssertWindows(PolyMesh mesh, float w, float d, float h, bool shopFloor, bool back, string name)
        {
            int floors = BuildingModels.Floors(h, shopFloor, out float floorH);
            float ground = shopFloor ? BuildingModels.ShopFloorHeight : BuildingModels.PlinthHeight;
            var rows = new bool[4, floors]; double backGlass = 0; int lying = 0, flush = 0;
            foreach (var t in Tris(mesh, Glass, Night))
            {
                if (!Vertical(t.Face)) { lying++; continue; }
                int side = Side(t.Face); var c = t.Centre;
                float inset = side == Front ? c.Z + d / 2 : side == Back ? d / 2 - c.Z : side == Left ? c.X + w / 2 : w / 2 - c.X;
                if (inset < 0.09f) flush++;
                if (side == Back) backGlass += t.Area;
                int floor = (int)Math.Floor((c.Y - ground) / floorH);
                if (floor >= 0 && floor < floors) rows[side, floor] = true;
            }
            Assert.That(lying, Is.Zero, name + ": glass stands upright"); Assert.That(flush, Is.Zero, name + ": glass sits back in its reveal");
            Assert.That(backGlass > 0, Is.EqualTo(back), name + " back windows");
            foreach (int side in new[] { Front, Back, Left, Right })
                if (side != Back || back)
                    for (int floor = 0; floor < floors; floor++) Assert.That(rows[side, floor], name + ": windows on side " + side + ", floor " + floor);
        }
        // The night glass is a separate submesh for the day–night stage: about two in five windows upstairs, shuffled by the
        // seed, and the whole shop front.
        [Test] public void AboutTwoFifthsOfTheWindowsUpstairsAndEveryShopFrontLightUpAtNight()
        {
            double lit = 0, all = 0;
            foreach (var t in Towers())
            {
                double towerLit = 0, towerAll = 0; int darkShopFront = 0;
                foreach (var tri in Tris(t.Mesh, Glass, Night))
                {
                    if (tri.Centre.Y < BuildingModels.ShopFloorHeight)
                    {
                        if (Side(tri.Face) == Front && tri.Part != Night) darkShopFront++;
                        continue;
                    }
                    towerAll += tri.Area; if (tri.Part == Night) towerLit += tri.Area;
                }
                Assert.That(darkShopFront, Is.Zero, t.Name + ": the shop windows and the door light up");
                Assert.That(towerLit, Is.GreaterThan(0), t.Name + ": some windows light up");
                Assert.That(towerLit, Is.LessThan(towerAll), t.Name + ": some stay dark");
                lit += towerLit; all += towerAll;
            }
            Assert.That(lit / all, Is.InRange(0.3, 0.55), "lit share of the windows upstairs");
            Assert.That(BuildingModels.NightShare, Is.InRange(0.3f, 0.55f));
        }
        // 6 × 8 × 9 m: a shop window, a glazed door and two floors of windows on the front, party walls at the sides, the
        // back windowed only when it faces a street; an awning over the window of brick shops and cafés, a canopy at offices.
        [Test] public void StreetBuildingsHaveShopFrontsPartyWallsAndBacksOnRequest()
        {
            float x = BuildingModels.ShopWidth / 2, z = BuildingModels.ShopDepth / 2, h = BuildingModels.ShopHeight;
            foreach (var style in ShopStyles)
                foreach (bool withBack in new[] { false, true })
                {
                    var mesh = BuildingModels.Shop(style, 4, withBack); string name = style + (withBack ? " with back windows" : "");
                    var (wallMin, wallMax) = Bounds(mesh, Wall);
                    Assert.That(wallMin.X, Is.EqualTo(-x).Within(1e-3f), name); Assert.That(wallMax.X, Is.EqualTo(x).Within(1e-3f), name);
                    Assert.That(wallMin.Z, Is.EqualTo(-z).Within(1e-3f), name); Assert.That(wallMax.Z, Is.EqualTo(z).Within(1e-3f), name);
                    Assert.That(wallMin.Y, Is.EqualTo(0).Within(1e-4f), name); Assert.That(wallMax.Y, Is.EqualTo(h + 0.3f).Within(1e-3f), name);
                    var (min, max) = Bounds(mesh, Wall, Trim, Glass, Night);
                    Assert.That(min.Y, Is.EqualTo(0).Within(1e-4f), name); Assert.That(max.Y, Is.LessThanOrEqualTo(h + 0.5f + 1e-3f), name);
                    Assert.That(Math.Max(-min.X, max.X), Is.LessThanOrEqualTo(x + 0.25f + 1e-3f), name + ": only the cornice runs over the party walls");
                    Assert.That(max.Z, Is.LessThan(z + 0.06f), name + ": a flat back");
                    Assert.That(-min.Z, Is.EqualTo(z + (style == ShopStyle.Office ? 0.8f : 1.5f)).Within(1e-3f), name + (style == ShopStyle.Office ? " canopy" : " awning"));
                    double front = 0, sides = 0, back = 0; int darkShopFront = 0;
                    foreach (var t in Tris(mesh, Glass, Night))
                    {
                        int side = Side(t.Face);
                        if (side == Front) { front += t.Area; if (t.Centre.Y < 2.5f && t.Part != Night) darkShopFront++; }
                        else if (side == Back) back += t.Area;
                        else sides += t.Area;
                    }
                    Assert.That(sides, Is.Zero, name + ": party walls");
                    Assert.That(back > 0, Is.EqualTo(withBack), name + " back windows");
                    Assert.That(darkShopFront, Is.Zero, name + ": the shop window and the door light up");
                    Assert.That(front, Is.GreaterThan(15), name + ": shop window, door and six windows upstairs");
                }
        }
        // The café's front wall is open where the scene's blockers leave the way in and the view: a 1.5 m doorway and a wide
        // window with a mullion, nothing in either; the room behind them has walls facing in all round and a ceiling.
        [Test] public void TheCafeIsOpenThroughItsDoorAndWindowIntoARoom()
        {
            var cafe = BuildingModels.Cafe(); var tris = new List<Tri>(Tris(cafe, Wall, Trim, Glass, Night));
            float x = BuildingModels.ShopWidth / 2, z = BuildingModels.ShopDepth / 2, t = BuildingModels.CafeWall;
            var door = BuildingModels.CafeDoor; var window = BuildingModels.CafeWindow;
            Assert.That(door.U1 - door.U0, Is.EqualTo(1.5f).Within(1e-4f), "a 1.5 m doorway");
            Assert.That(window.U0, Is.GreaterThan(0.5f)); Assert.That(window.U1, Is.LessThan(door.U0 - 0.3f), "a pier between the window and the door");
            // Straight through the wall at every point of the openings (the front's u runs along +X from its left corner).
            int blocked = 0, rays = 0; var into = new V3(0, 0, 1);
            for (float u = door.U0 + 0.05f; u < door.U1 - 0.04f; u += 0.1f)
                for (float v = 0.05f; v < BuildingModels.CafeDoorTop - 0.04f; v += 0.1f)
                {
                    rays++; if (Nearest(tris, new V3(u - x, v, -z - 0.3f), into, t + 0.6f, out _, out _)) blocked++;
                }
            Assert.That(blocked, Is.Zero, "the doorway is open (" + rays + " rays)");
            float mullion = (window.U0 + window.U1) / 2;
            for (float u = window.U0 + 0.05f; u < window.U1 - 0.04f; u += 0.1f)
                for (float v = BuildingModels.CafeWindowSill + 0.05f; v < BuildingModels.CafeWindowTop - 0.04f; v += 0.1f)
                {
                    if (Math.Abs(u - mullion) < 0.12f) continue;
                    rays++; if (Nearest(tris, new V3(u - x, v, -z - 0.3f), into, t + 0.6f, out _, out _)) blocked++;
                }
            Assert.That(blocked, Is.Zero, "the window is open, without glass");
            Assert.That(Nearest(tris, new V3(mullion - x, 1.5f, -z - 0.3f), into, t + 0.6f, out _, out _), "the mullion");
            // The room: from its middle the walls are the thickness of the shell away, facing in; the ceiling is 3 m up.
            var middle = new V3(0, 1.5f, 0);
            foreach (var (dir, reach, what) in new[]
            {
                (new V3(1, 0, 0), x - t, "right wall"), (new V3(-1, 0, 0), x - t, "left wall"), (new V3(0, 0, 1), z - t, "back wall"),
                (new V3(0, 1, 0), BuildingModels.CafeRoomHeight - 1.5f, "ceiling"),
            })
            {
                Assert.That(Nearest(tris, middle, dir, 20, out var hit, out float distance), what);
                Assert.That(distance, Is.EqualTo(reach).Within(0.035f), what);
                Assert.That(V3.Dot(hit.Face, dir), Is.LessThan(0), what + " faces into the room");
            }
            float pier = (window.U1 + door.U0) / 2 - x;
            Assert.That(Nearest(tris, new V3(pier, 1.5f, 0), new V3(0, 0, -1), 20, out var front, out float wall), "front wall between the window and the door");
            Assert.That(wall, Is.EqualTo(z - t).Within(0.035f)); Assert.That(V3.Dot(front.Face, new V3(0, 0, -1)), Is.LessThan(0));
            // No glass downstairs in front: the café is open, unlike the shops.
            foreach (var g in Tris(cafe, Glass, Night)) Assert.That(g.Centre.Y > 3 || Side(g.Face) != Front, "glass in the café front at " + g.Centre);
        }
        [Test] public void BuildingsKeepToATriangleBudget()
        {
            int total = 0;
            foreach (var t in Towers()) { Assert.That(t.Mesh.TriangleCount, Is.LessThan(6500), t.Name); total += t.Mesh.TriangleCount; }
            var office = Office(); Assert.That(office.TriangleCount, Is.LessThan(4000), "office block"); total += office.TriangleCount;
            // The district's five street buildings (the sixth is the café).
            foreach (var style in new[] { ShopStyle.BrickShop, ShopStyle.Office, ShopStyle.BrickShop, ShopStyle.Office, ShopStyle.BrickShop })
            {
                var shop = BuildingModels.Shop(style, 0, true); Assert.That(shop.TriangleCount, Is.LessThan(1000), style.ToString()); total += shop.TriangleCount;
            }
            var cafe = BuildingModels.Cafe(); Assert.That(cafe.TriangleCount, Is.LessThan(1200), "café"); total += cafe.TriangleCount;
            Assert.That(total, Is.LessThan(100000), "all the buildings of the scene");
        }
        [Test] public void WallLooksFloorsAndSizes()
        {
            var plaster = new HashSet<uint>();
            for (int seed = 0; seed < 40; seed++)
            {
                foreach (var style in Styles)
                {
                    var look = BuildingModels.Wall(style, seed);
                    Assert.That(VoxelSurfaces.Has(look.Surface), look.ToString());
                    Assert.That(look.Surface, Is.EqualTo(style == TowerStyle.Brick ? "brick" : "stucco"), style.ToString());
                    Assert.That(look.Tint, Is.LessThanOrEqualTo(0xFFFFFFu));
                    // Light tints: the texture and the sun still read on them.
                    for (int shift = 0; shift < 24; shift += 8) Assert.That((look.Tint >> shift) & 0xFF, Is.GreaterThan(0x99u), look.ToString());
                    Assert.That(BuildingModels.Wall(style, seed), Is.EqualTo(look));
                }
                plaster.Add(BuildingModels.Wall(TowerStyle.Plaster, seed).Tint);
            }
            Assert.That(plaster.Count, Is.GreaterThanOrEqualTo(4), "pastel stucco in several colours");
            foreach (var style in ShopStyles) Assert.That(VoxelSurfaces.Has(BuildingModels.Wall(style).Surface), style.ToString());
            Assert.That(BuildingModels.Wall(ShopStyle.BrickShop).Surface, Is.EqualTo("brick"));
            Assert.That(BuildingModels.Wall(ShopStyle.Cafe), Is.Not.EqualTo(BuildingModels.Wall(ShopStyle.Office)));
            Assert.That(new WallLook("stucco", 0xF0D49C).ToString(), Is.EqualTo("stucco #F0D49C"));
            // About 3 m floors between the shop floor and the cornice.
            foreach (var t in Towers())
            {
                int n = BuildingModels.Floors(t.Height, true, out float floorH);
                Assert.That(n, Is.GreaterThanOrEqualTo(2), t.Name); Assert.That(floorH, Is.InRange(BuildingModels.MinFloorHeight, 4.1f), t.Name);
                Assert.That(BuildingModels.ShopFloorHeight + n * floorH + BuildingModels.TopZone, Is.EqualTo(t.Height).Within(1e-3f), t.Name);
            }
            Assert.That(BuildingModels.Floors(7.5f, false, out float office), Is.EqualTo(2)); Assert.That(office, Is.EqualTo(2.8f).Within(1e-4f));
            for (float h = 8; h <= 80; h += 0.25f)
            {
                BuildingModels.Floors(h, true, out float floorH);
                Assert.That(floorH, Is.GreaterThanOrEqualTo(BuildingModels.MinFloorHeight - 1e-4f), h + " m");
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => BuildingModels.Tower(3, 10, 20, TowerStyle.Brick, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => BuildingModels.Tower(10, 3, 20, TowerStyle.Brick, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => BuildingModels.Tower(61, 10, 20, TowerStyle.Glass, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => BuildingModels.Tower(10, 10, 81, TowerStyle.Plaster, 0));
            // Too low for a floor over the shop; without the shop floor the same height has one.
            Assert.Throws<ArgumentOutOfRangeException>(() => BuildingModels.Tower(10, 10, 7, TowerStyle.Plaster, 0));
            Assert.That(BuildingModels.Floors(7, false, out _), Is.EqualTo(1));
            Assert.That(BuildingModels.Tower(10, 10, 7, TowerStyle.Plaster, 0, true, false).TriangleCount, Is.GreaterThan(0));
        }
    }
}
