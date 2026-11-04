using System.Collections.Generic;
using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    // The ring of roads round the office quarter (CityRoads): asphalt slabs of the west, south and east streets and of the
    // lay-by, the 45° kerb cuts as asphalt prisms, kerbs on every edge, pavements, centre dashes, lamps, the towers along
    // the outer side, and the railing with its parking sign round the lay-by. The player's car meets the kerbs in Core
    // (PlayerCar); the walls here only keep a walker on the square, and they let the aim through to the car.
    public sealed partial class OfficeScene
    {
        // Top of the ring's kerbs above the ground; slanted kerbs stay a hair lower, so their overlapping ends never share
        // a face with the straight ones; how far kerb ends reach past a corner to close it.
        private const float KerbTop = 0.34f, SlantedKerbDrop = 0.002f, KerbJoin = 0.15f;
        // The boulevard's asphalt slab (BuildBoulevard) starts here; the slabs of the ring end at it.
        private const float BoulevardAsphaltMinZ = CityDistrictPlan.SouthKerbZ - 0.2f;
        // The faces of the district hedges and of the back walls that the inner pavements reach.
        private const float HedgeFaceX = 44.85f, BackWallFaceZ = -12.175f;
        private const float RailHeight = 1.33f, RailPostSpacing = 3.4f;
        private const int IgnoreRaycastLayer = 2;

        private void BuildRoads(Material asphalt, Material concrete, Material pavement, Material grass, Material metal)
        {
            const float g = CityBlockPlan.GroundY, low = CityBlockPlan.TerrainTopY, h = CityRoads.KerbHalf;
            var west = CityRoads.WestStreet; var east = CityRoads.EastStreet; var south = CityRoads.SouthStreet;
            const float bz = CityRoads.BoulevardMinZ, outer = CityRoads.OuterCut, inner = CityRoads.InnerCut;
            // Ground south of the city terrain, under the south street and its towers.
            Box("Ring / south ground", new Vector3(0, low - 0.25f, -30), new Vector3(200, 0.5f, 30), concrete, false);
            // Asphalt: the side streets end at the boulevard's slab, the south street holds both outer corners.
            Slab("Ring / west street", west.MinX, south.MaxZ, west.MaxX, BoulevardAsphaltMinZ, asphalt);
            Slab("Ring / east street", east.MinX, south.MaxZ, east.MaxX, BoulevardAsphaltMinZ, asphalt);
            Slab("Ring / south street", south.MinX, south.MinZ, south.MaxX, south.MaxZ, asphalt);
            Slab("Lay-by / asphalt", CityRoads.BayMinX, CityRoads.BayMinZ, CityRoads.BayMaxX, BoulevardAsphaltMinZ, asphalt);
            foreach (var cut in CityRoads.KerbCuts) CutAsphalt(cut, asphalt);
            // Kerbs: the outer south kerb runs past both outer corners and covers them; the inner kerbs end at the cuts.
            Kerb("Ring / outer kerb", west.MinX, south.MinZ, west.MinX, bz - outer, -1, 0, 0, KerbJoin, false, concrete);
            Kerb("Ring / outer kerb", east.MaxX, south.MinZ, east.MaxX, bz - outer, 1, 0, 0, KerbJoin, false, concrete);
            Kerb("Ring / outer kerb", west.MinX, south.MinZ, east.MaxX, south.MinZ, 0, -1, 2 * h, 2 * h, false, concrete);
            Kerb("Ring / inner kerb", west.MaxX, south.MaxZ + outer, west.MaxX, bz - inner, 1, 0, KerbJoin, KerbJoin, false, concrete);
            Kerb("Ring / inner kerb", east.MinX, south.MaxZ + outer, east.MinX, bz - inner, -1, 0, KerbJoin, KerbJoin, false, concrete);
            Kerb("Ring / inner kerb", west.MaxX + outer, south.MaxZ, east.MinX - outer, south.MaxZ, 0, 1, KerbJoin, KerbJoin, false, concrete);
            Kerb("Lay-by / kerb", CityRoads.BayMinX, CityRoads.BayMinZ, CityRoads.BayMaxX, CityRoads.BayMinZ, 0, -1, KerbJoin, KerbJoin, false, concrete);
            const float diagonal = 0.70710678f;
            foreach (var cut in CityRoads.KerbCuts)
                Kerb("Ring / slanted kerb", cut.Start.X, cut.Start.Z, cut.End.X, cut.End.Z, cut.Sx * diagonal, cut.Sz * diagonal, KerbJoin, KerbJoin, true, concrete);
            // Pavements: a strip of pavers on the inner side up to the hedges and the back walls, 2.5 m outside; the south
            // pavement holds the outer corners.
            float kerbFace = 2 * h;
            Paving("Ring / inner pavement", west.MaxX + kerbFace, BackWallFaceZ, -HedgeFaceX, BoulevardAsphaltMinZ, pavement);
            Paving("Ring / inner pavement", HedgeFaceX, BackWallFaceZ, east.MinX - kerbFace, BoulevardAsphaltMinZ, pavement);
            Paving("Ring / inner pavement", west.MaxX + kerbFace, south.MaxZ + kerbFace, east.MinX - kerbFace, BackWallFaceZ, pavement);
            Paving("Ring / outer pavement", CityRoads.OuterPavementWestX, south.MinZ - kerbFace, west.MinX - kerbFace, BoulevardAsphaltMinZ, pavement);
            Paving("Ring / outer pavement", east.MaxX + kerbFace, south.MinZ - kerbFace, -CityRoads.OuterPavementWestX, BoulevardAsphaltMinZ, pavement);
            Paving("Ring / outer pavement", CityRoads.OuterPavementWestX, CityRoads.OuterPavementSouthZ, -CityRoads.OuterPavementWestX, south.MinZ - kerbFace, pavement);
            // Centre dashes of the two-way streets.
            var paint = Mat("#E9E4D6");
            const float markY = g + RoadTop + 0.005f;
            for (int dash = 0; dash < 7; dash++)
                foreach (float x in new[] { CityRoads.WestX, CityRoads.EastX })
                    Box("Ring / centre dash", new Vector3(x, markY, -10.5f + dash * 6), new Vector3(0.12f, 0.01f, 3), paint, false);
            for (int dash = 0; dash < 15; dash++)
                Box("Ring / centre dash", new Vector3(-42 + dash * 6, markY, CityRoads.SouthZ), new Vector3(3, 0.01f, 0.12f), paint, false);
            // The lay-by: a dashed edge line along the boulevard and a parking sign on the railing for the square.
            for (int dash = 0; dash < 6; dash++)
                Box("Lay-by / edge dash", new Vector3(CityRoads.BayMinX + 0.5f + dash * 2, markY, bz - 0.12f), new Vector3(1, 0.01f, 0.12f), paint, false);
            BuildParkingSign(metal);
            BuildBayRailing(metal);
            // Lamps over the outer pavements and towers behind them, fronts to the road.
            foreach (var lamp in CityRoads.Lamps)
                PlaceStreetLamp("Ring / street lamp", new Vector3(lamp.X, g, lamp.Z), lamp.Yaw);
            var frontage = CityRoads.Frontage;
            for (int i = 0; i < frontage.Count; i++)
            {
                var b = frontage[i]; int seed = 40 + i;
                PlaceBuilding("ring tower/" + i, () => BuildingModels.Tower(b.Front, b.Depth, b.Height, (TowerStyle)b.Style, seed),
                    BuildingModels.Wall((TowerStyle)b.Style, seed), "Ring / tower " + i, new Vector3(b.X, low, b.Z), b.Yaw);
            }
        }
        // Asphalt from the terrain up to the road surface.
        private void Slab(string name, float minX, float minZ, float maxX, float maxZ, Material material)
        {
            const float low = CityBlockPlan.TerrainTopY, top = CityBlockPlan.GroundY + RoadTop;
            Box(name, new Vector3((minX + maxX) / 2, (low + top) / 2, (minZ + maxZ) / 2), new Vector3(maxX - minX, top - low, maxZ - minZ), material, false);
        }
        // Pavers from the terrain up to the walking level.
        private void Paving(string name, float minX, float minZ, float maxX, float maxZ, Material material)
        {
            const float low = CityBlockPlan.TerrainTopY, top = CityBlockPlan.GroundY;
            Box(name, new Vector3((minX + maxX) / 2, (low + top) / 2, (minZ + maxZ) / 2), new Vector3(maxX - minX, top - low, maxZ - minZ), material, false);
        }
        // A kerb on the road edge from (x0, z0) to (x1, z1), on the side of the unit normal (nx, nz) away from the road,
        // reaching past its ends to close the corners with its neighbours.
        private void Kerb(string name, float x0, float z0, float x1, float z1, float nx, float nz, float join0, float join1, bool slanted, Material concrete)
        {
            const float low = CityBlockPlan.TerrainTopY, half = CityRoads.KerbHalf;
            float top = CityBlockPlan.GroundY + KerbTop - (slanted ? SlantedKerbDrop : 0);
            float dx = x1 - x0, dz = z1 - z0, length = Mathf.Sqrt(dx * dx + dz * dz); dx /= length; dz /= length;
            float shift = (join1 - join0) / 2;
            var centre = new Vector3((x0 + x1) / 2 + dx * shift + nx * half, (low + top) / 2, (z0 + z1) / 2 + dz * shift + nz * half);
            var kerb = Box(name, centre, new Vector3(length + join0 + join1, top - low, 2 * half), concrete, false);
            kerb.transform.localRotation = Quaternion.Euler(0, YawAlong(dx, dz), 0);
        }
        // The yaw that turns a box's +X along the direction (dx, dz).
        private static float YawAlong(float dx, float dz) => Mathf.Atan2(-dz, dx) * Mathf.Rad2Deg;
        // The asphalt of a kerb cut: the triangle between its corner and the ends of the slanted kerb, cut back where the
        // boulevard's slab already covers it, as a prism from the terrain to the road surface with metric UVs on top.
        private void CutAsphalt(KerbCut cut, Material asphalt)
        {
            var outline = ClipBelow(new List<Vector2> { new Vector2(cut.X, cut.Z), new Vector2(cut.Start.X, cut.Start.Z), new Vector2(cut.End.X, cut.End.Z) },
                BoulevardAsphaltMinZ);
            if (outline.Count < 3) return;
            float tile = surfaceTiles.TryGetValue(asphalt, out float metres) ? metres : 2;
            var mesh = PrismMesh(outline, CityBlockPlan.TerrainTopY, CityBlockPlan.GroundY + RoadTop, tile);
            mesh.name = "Broker road / kerb cut"; surfaceMeshes.Add(mesh);
            var obj = new GameObject("Ring / kerb cut asphalt"); obj.transform.SetParent(transform, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = asphalt;
        }
        // The part of a convex polygon (x, z) with z at most maxZ.
        private static List<Vector2> ClipBelow(List<Vector2> polygon, float maxZ)
        {
            var kept = new List<Vector2>();
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                bool aIn = a.y <= maxZ, bIn = b.y <= maxZ;
                if (aIn) kept.Add(a);
                if (aIn != bIn) kept.Add(new Vector2(a.x + (b.x - a.x) * (maxZ - a.y) / (b.y - a.y), maxZ));
            }
            return kept;
        }
        // A vertical prism over a convex outline: the top facing up with UVs in metres like the asphalt boxes, and the
        // sides facing out.
        private static Mesh PrismMesh(List<Vector2> outline, float bottom, float top, float tile)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            float area = 0, cx = 0, cz = 0;
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Count];
                area += a.x * b.y - b.x * a.y; cx += a.x; cz += a.y;
            }
            cx /= outline.Count; cz /= outline.Count;
            // Unity faces are clockwise seen from the front: from above, a counter-clockwise outline (x right, z up) is walked
            // backwards.
            bool counterClockwise = area > 0;
            for (int i = 0; i < outline.Count; i++)
            {
                vertices.Add(new Vector3(outline[i].x, top, outline[i].y)); normals.Add(Vector3.up); uv.Add(new Vector2(outline[i].x / tile, outline[i].y / tile));
            }
            for (int i = 1; i + 1 < outline.Count; i++)
            {
                if (counterClockwise) { triangles.Add(0); triangles.Add(i + 1); triangles.Add(i); }
                else { triangles.Add(0); triangles.Add(i); triangles.Add(i + 1); }
            }
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Count];
                float ex = b.x - a.x, ez = b.y - a.y, length = Mathf.Sqrt(ex * ex + ez * ez);
                if (length < 1e-4f) continue;
                var normal = new Vector3(ez / length, 0, -ex / length);
                if (normal.x * ((a.x + b.x) / 2 - cx) + normal.z * ((a.y + b.y) / 2 - cz) < 0) normal = -normal;
                int n = vertices.Count;
                vertices.Add(new Vector3(a.x, top, a.y)); vertices.Add(new Vector3(b.x, top, b.y));
                vertices.Add(new Vector3(b.x, bottom, b.y)); vertices.Add(new Vector3(a.x, bottom, a.y));
                for (int k = 0; k < 4; k++) normals.Add(normal);
                uv.Add(new Vector2(0, top / tile)); uv.Add(new Vector2(length / tile, top / tile));
                uv.Add(new Vector2(length / tile, bottom / tile)); uv.Add(new Vector2(0, bottom / tile));
                // Clockwise seen from outside: the winding whose cross product points along the normal.
                var cross = Vector3.Cross(vertices[n + 1] - vertices[n], vertices[n + 2] - vertices[n]);
                if (Vector3.Dot(cross, normal) >= 0) { triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2); triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 3); }
                else { triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 1); triangles.Add(n); triangles.Add(n + 3); triangles.Add(n + 2); }
            }
            var mesh = new Mesh { vertices = vertices.ToArray(), normals = normals.ToArray(), uv = uv.ToArray() };
            mesh.triangles = triangles.ToArray(); mesh.RecalculateBounds();
            return mesh;
        }
        // Railing posts at every corner and at most 3.4 m apart with a top rail between them.
        private void Railing(string name, Vector2[] points, Material metal)
        {
            const float g = CityBlockPlan.GroundY;
            for (int i = 0; i + 1 < points.Length; i++)
            {
                Vector2 a = points[i], b = points[i + 1];
                float dx = b.x - a.x, dz = b.y - a.y, length = Mathf.Sqrt(dx * dx + dz * dz);
                int spans = Mathf.Max(1, Mathf.CeilToInt(length / RailPostSpacing - 0.01f));
                for (int k = 0; k < spans; k++)
                    Box(name + " post", new Vector3(a.x + dx * k / spans, g + 0.8f, a.y + dz * k / spans), new Vector3(0.1f, 1.1f, 0.1f), metal, false);
                var rail = Box(name, new Vector3((a.x + b.x) / 2, g + RailHeight, (a.y + b.y) / 2), new Vector3(length + 0.07f, 0.08f, 0.14f), metal, false);
                rail.transform.localRotation = Quaternion.Euler(0, YawAlong(dx / length, dz / length), 0);
            }
            var last = points[points.Length - 1];
            Box(name + " post", new Vector3(last.x, g + 0.8f, last.y), new Vector3(0.1f, 1.1f, 0.1f), metal, false);
        }
        // Invisible walls along a polyline that keep walkers on the pavement; see-through walls sit on Ignore Raycast, so the
        // aim reaches whatever stands behind them.
        private void Barrier(string name, Vector2[] points, bool seeThrough)
        {
            const float g = CityBlockPlan.GroundY;
            for (int i = 0; i + 1 < points.Length; i++)
            {
                Vector2 a = points[i], b = points[i + 1];
                float dx = b.x - a.x, dz = b.y - a.y, length = Mathf.Sqrt(dx * dx + dz * dz);
                var wall = Blocker(name, new Vector3((a.x + b.x) / 2, g + 0.65f, (a.y + b.y) / 2), new Vector3(length + 0.3f, 1.3f, 2 * CityRoads.KerbHalf));
                wall.transform.localRotation = Quaternion.Euler(0, YawAlong(dx / length, dz / length), 0);
                if (seeThrough) wall.layer = IgnoreRaycastLayer;
            }
        }
        // The south railing west of the crossing follows the kerb round the lay-by: along the boulevard, down the western
        // taper, along the back kerb and up the eastern taper. Its walls round the lay-by let the aim through to the car.
        private void BuildBayRailing(Material metal)
        {
            const float rail = CityDistrictPlan.SouthKerbZ, back = CityRoads.BayMinZ - CityRoads.KerbHalf;
            const float slant = CityRoads.KerbHalf * 1.41421356f;
            // The kerb centre lines of the tapers: x + z and x − z stay constant along them.
            const float westLine = CityRoads.BayMinX + CityRoads.BayMinZ - slant, eastLine = CityRoads.BayMaxX - CityRoads.BayMinZ + slant;
            var westTop = new Vector2(westLine - rail, rail); var westFoot = new Vector2(westLine - back, back);
            var eastFoot = new Vector2(eastLine + back, back); var eastTop = new Vector2(eastLine + rail, rail);
            const float first = 3.3f, districtEdge = 44.5f;
            Railing("Boulevard / railing", new[] { new Vector2(-first - 12 * RailPostSpacing, rail), westTop, westFoot, eastFoot, eastTop, new Vector2(-first, rail) }, metal);
            Barrier("Boulevard / kerb barrier", new[] { new Vector2(-districtEdge, rail), westTop }, false);
            Barrier("Lay-by / railing barrier", new[] { westTop, westFoot, eastFoot, eastTop }, true);
            Barrier("Boulevard / kerb barrier", new[] { eastTop, new Vector2(-CityCrosswalk.GateHalfWidth, rail) }, false);
        }
        // A two-sided blue «P» board on a post in the railing at the eastern end of the lay-by: one face for the square,
        // the other for the drivers in the lay-by.
        private void BuildParkingSign(Material metal)
        {
            const float g = CityBlockPlan.GroundY, z = CityRoads.BayMinZ - CityRoads.KerbHalf, x = CityRoads.BayMaxX - 0.9f;
            var blue = Mat("#2F5FA8");
            Box("Lay-by / sign post", new Vector3(x, g + 1.3f, z), new Vector3(0.08f, 2.6f, 0.08f), metal, false);
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Lay-by / sign board", new Vector3(x, g + 2.35f, z + side * 0.06f), new Vector3(0.62f, 0.62f, 0.04f), blue, false);
                Box("Lay-by / sign plate", new Vector3(x, g + 1.92f, z + side * 0.06f), new Vector3(0.62f, 0.16f, 0.04f), blue, false);
                var letter = Text("Lay-by / sign letter", "P", new Vector3(x, g + 2.35f, z + side * 0.085f), 96, 0.05f, Color.white);
                var label = Text("Lay-by / sign label", "ПАРКОВКА", new Vector3(x, g + 1.92f, z + side * 0.085f), 48, 0.02f, Color.white);
                if (side < 0) continue;
                letter.transform.localEulerAngles = new Vector3(0, 180, 0); label.transform.localEulerAngles = new Vector3(0, 180, 0);
            }
        }
    }
}
