using System;
using System.Collections.Generic;

namespace Broker.Core
{
    // The Exchange as low-poly models with the sizes and pivots of the voxel ones: the granite podium with its steps,
    // cheeks, parapets and the red runner; the paving of its top and the hall's parquet (meshes of their own, for their
    // own wall materials); the hall with pilasters, tall windows and an open door; the entablature and pediments with
    // triglyphs and a brass medallion; the fluted columns, the quote board, the bull and the bear, the rostral columns.
    public static partial class BuildingModels
    {
        public static readonly WallLook ExchangeGranite = new WallLook("ashlar", 0xD3BFB3), ExchangeDarkGranite = new WallLook("ashlar", 0x9C928C),
            ExchangePaving = new WallLook("pavers", 0xF6EEE4), ExchangeParquet = new WallLook("wood", 0xFFFFFF),
            ExchangeWalls = new WallLook("stucco", 0xEEDDBF), ExchangeStone = new WallLook("stucco", 0xE8E2D6);

        // ---------- Podium ----------
        // Podium coordinates: x across, z from the foot of the steps (CityDistrictPlan.StepsMinZ) back, y from the terrain,
        // which lies 0.1 m under the square; the top is at CityDistrictPlan.PodiumFloorY over the square.
        public const float PodiumTop = CityDistrictPlan.PodiumFloorY + (CityBlockPlan.GroundY - CityBlockPlan.TerrainTopY);
        public const float PodiumHalf = CityDistrictPlan.PodiumHalfWidth, PodiumDepth = CityDistrictPlan.PodiumMaxZ - CityDistrictPlan.StepsMinZ;
        public const float StairHalf = CityDistrictPlan.StepsHalfWidth, StairRun = CityDistrictPlan.PodiumMinZ - CityDistrictPlan.StepsMinZ;
        // Four steps of 0.2 m between the cheeks; the first tread 0.4 m over the terrain.
        public const int Steps = 4;
        public const float FirstTread = 0.4f, Riser = 0.2f;
        public static float TreadHeight(int step) => FirstTread + Riser * step;
        public static float TreadFront(int step) => StairRun * step / Steps;
        // The hall stands on the podium with its front face at HallFrontZ on the plan; walls HallWall thick.
        public const float HallFrontZ = 60, HallWidth = 18, HallDepth = 12, HallHeight = 7, HallWall = 0.5f;
        private const float HallZ = HallFrontZ - CityDistrictPlan.StepsMinZ;
        // The runner from the foot of the steps to the quote board, in podium coordinates.
        public const float RunnerHalf = 0.8f, RunnerEdge = 0.1f, RunnerEndZ = 14.2f;

        // Granite walls on the wall material with a dark plinth and a light coping, four steps with stone nosings between
        // the cheeks, low capped parapets along the sides and the back, the runner over the steps. The top itself is
        // ExchangeTerrace and ExchangeHallFloor. Pivot at the middle of the front edge on the terrain.
        public static PolyMesh ExchangePodium()
        {
            var b = NewBuilder();
            const float x = PodiumHalf, d = PodiumDepth, top = PodiumTop, s = StairHalf, run = StairRun;
            var faces = new[]
            {
                new Face(new V3(-x, 0, 0), new V3(1, 0, 0), new V3(0, 0, -1), 2 * x),
                new Face(new V3(x, 0, d), new V3(-1, 0, 0), new V3(0, 0, 1), 2 * x),
                new Face(new V3(-x, 0, d), new V3(0, 0, -1), new V3(-1, 0, 0), d),
                new Face(new V3(x, 0, 0), new V3(0, 0, 1), new V3(1, 0, 0), d),
            };
            for (int k = 0; k < 4; k++)
                WallFace(b, faces[k], 0, top, k == Front ? new List<Rect> { new Rect(x - s, x + s, 0, top) } : new List<Rect>());
            var gap = new List<(int, float, float)> { (Front, x - s, x + s) };
            Belt(b, faces, 0, 0.36f, 0.04f, VoxelPalette.StoneDark, true, gap);
            Belt(b, faces, top - 0.12f, top, 0.05f, VoxelPalette.StoneLight, true, gap);
            // The cheeks' inner faces along the steps; the coping turns in over them.
            var left = new Face(new V3(-s, 0, 0), new V3(0, 0, 1), new V3(1, 0, 0), run);
            var right = new Face(new V3(s, 0, run), new V3(0, 0, -1), new V3(-1, 0, 0), run);
            WallFace(b, left, 0, top, new List<Rect>()); WallFace(b, right, 0, top, new List<Rect>());
            Ledge(b, left, -0.05f, run, top - 0.12f, top, 0.05f, VoxelPalette.StoneLight);
            Ledge(b, right, 0, run + 0.05f, top - 0.12f, top, 0.05f, VoxelPalette.StoneLight);
            // Risers on the wall material, light stone nosings, treads; the last riser climbs to the top.
            for (int i = 0; i <= Steps; i++)
            {
                float y = i < Steps ? TreadHeight(i) : top, below = i == 0 ? 0 : TreadHeight(i - 1), z0 = TreadFront(i);
                b.Quad(new V3(-s, below, z0), new V3(s, below, z0), new V3(s, y - 0.06f, z0), new V3(-s, y - 0.06f, z0), new V3(0, 0, -1), WallPart, 0);
                b.Box(new V3(-s, y - 0.06f, z0 - 0.03f), new V3(s, y, z0), 0, TrimPart, VoxelPalette.StoneLight);
                if (i < Steps)
                    b.Quad(new V3(-s, y, z0), new V3(s, y, z0), new V3(s, y, TreadFront(i + 1)), new V3(-s, y, TreadFront(i + 1)), Up, WallPart, 0);
            }
            // Parapets behind the cheeks along both sides and across the back, with stone caps.
            const float pt = 0.2f, ph = 0.4f, cap = 0.1f, co = 0.05f;
            foreach (float side in new[] { -1f, 1f })
            {
                float x0 = side < 0 ? -x : x - pt, x1 = side < 0 ? -x + pt : x;
                b.Box(new V3(x0, top, run), new V3(x1, top + ph, d - pt), 0, WallPart, 0);
                b.Box(new V3(x0 - co, top + ph, run - co), new V3(x1 + co, top + ph + cap, d - pt - co), 0.015f, TrimPart, VoxelPalette.StoneLight);
            }
            b.Box(new V3(-x, top, d - pt), new V3(x, top + ph, d), 0, WallPart, 0);
            b.Box(new V3(-x - co, top + ph, d - pt - co), new V3(x + co, top + ph + cap, d + co), 0.015f, TrimPart, VoxelPalette.StoneLight);
            // The red runner with brass edges, down every riser in front of the nosing and along every tread.
            Runner(b, -RunnerHalf, RunnerHalf, TrimPart, VoxelPalette.CarRed);
            Runner(b, -RunnerHalf - RunnerEdge, -RunnerHalf, MetalPart, VoxelPalette.Brass);
            Runner(b, RunnerHalf, RunnerHalf + RunnerEdge, MetalPart, VoxelPalette.Brass);
            return b.Build(PaletteUv);
        }
        private static void Runner(PolyBuilder b, float x0, float x1, int part, byte color)
        {
            const float lift = 0.012f, o = 0.04f;
            for (int i = 0; i <= Steps; i++)
            {
                float y = (i < Steps ? TreadHeight(i) : PodiumTop) + lift, below = i == 0 ? 0.2f : TreadHeight(i - 1) + lift, z0 = TreadFront(i) - o;
                float z1 = i < Steps ? TreadFront(i + 1) - o : RunnerEndZ;
                b.Quad(new V3(x0, below, z0), new V3(x1, below, z0), new V3(x1, y, z0), new V3(x0, y, z0), new V3(0, 0, -1), part, color);
                b.Quad(new V3(x0, y, z0), new V3(x1, y, z0), new V3(x1, y, z1), new V3(x0, y, z1), Up, part, color);
            }
        }
        // The paving of the podium top and the cheeks (the wall material is the paving), around the hall's floor and the
        // runner. Podium coordinates.
        public static PolyMesh ExchangeTerrace()
        {
            var b = NewBuilder();
            const float x = PodiumHalf, s = StairHalf, run = StairRun, r = RunnerHalf + RunnerEdge;
            float ix = HallWidth / 2 - HallWall, iz0 = HallZ + HallWall, iz1 = HallZ + HallDepth - HallWall;
            Floor(b, -x, -s, 0, run); Floor(b, s, x, 0, run);
            Floor(b, -x, -r, run, iz0); Floor(b, r, x, run, iz0);
            Floor(b, -x, -ix, iz0, iz1); Floor(b, ix, x, iz0, iz1);
            Floor(b, -x, x, iz1, PodiumDepth);
            return b.Build(PaletteUv);
        }
        // The hall's parquet inside its walls (the wall material is the wood). Podium coordinates.
        public static PolyMesh ExchangeHallFloor()
        {
            var b = NewBuilder();
            float ix = HallWidth / 2 - HallWall;
            Floor(b, -ix, ix, HallZ + HallWall, HallZ + HallDepth - HallWall);
            return b.Build(PaletteUv);
        }
        private static void Floor(PolyBuilder b, float x0, float x1, float z0, float z1)
            => b.Quad(new V3(x0, PodiumTop, z0), new V3(x1, PodiumTop, z0), new V3(x1, PodiumTop, z1), new V3(x0, PodiumTop, z1), Up, WallPart, 0);

        // ---------- Hall ----------
        // 18 × 12 × 7 m with 0.5 m walls: warm stucco on the wall material, a stone base course and top band, pilasters
        // between the openings and at the corners, an open 3 × 4 m door in a stone frame with a brass keystone and a glazed
        // transom, two tall windows in front and three in each side, through the wall; inside a dark wainscot, a rail,
        // stucco and a stone cornice. The roof closes it from above. Pivot at the middle of the front wall's outer face.
        public static readonly (float U0, float U1) HallDoor = (7.5f, 10.5f);
        public const float HallDoorTop = 4;
        private static readonly float[] HallSideWindows = { 2.75f, 6.25f, 9.5f };
        public static PolyMesh ExchangeHall()
        {
            var b = NewBuilder();
            const float w = HallWidth, d = HallDepth, h = HallHeight, t = HallWall;
            var faces = Faces(w, d);
            for (int k = 0; k < 4; k++) faces[k] = new Face(faces[k].Origin + new V3(0, 0, d / 2), faces[k].U, faces[k].N, faces[k].Length);
            var holes = new List<Rect>[4];
            for (int k = 0; k < 4; k++) holes[k] = new List<Rect>();
            var front = faces[Front];
            var door = new Rect(HallDoor.U0, HallDoor.U1, 0, HallDoorTop);
            holes[Front].Add(door);
            Reveal(b, front, door, t, false);
            Ledge(b, front, door.U0 - 0.5f, door.U0, 0, door.V1, 0.06f, VoxelPalette.StoneLight);
            Ledge(b, front, door.U1, door.U1 + 0.5f, 0, door.V1, 0.06f, VoxelPalette.StoneLight);
            Ledge(b, front, door.U0 - 0.5f, door.U1 + 0.5f, door.V1, door.V1 + 0.5f, 0.06f, VoxelPalette.StoneLight);
            float key = (door.U0 + door.U1) / 2;
            FaceBox(b, front, key - 0.25f, key + 0.25f, door.V1 - 0.04f, door.V1 + 0.56f, 0, 0.1f, MetalPart, VoxelPalette.Brass, 0.015f);
            var tall = new Sash { Depth = 0.18f, Frame = 0.07f, Bar = 0.06f, Cross = true, TransomAt = 0.72f, SillColor = VoxelPalette.StoneLight,
                SillProud = 0.08f, SillHeight = 0.1f, Lintel = 0.22f, LintelColor = VoxelPalette.StoneLight };
            var transom = new Sash { Depth = 0.18f, Frame = 0.07f, Bar = 0.06f, MullionStep = 0.75f, SillProud = 0, Lintel = 0.22f, LintelColor = VoxelPalette.StoneLight };
            Window(b, front, holes[Front], new Rect(door.U0, door.U1, 4.5f, 5.5f), transom, NightPart, t);
            foreach (float u0 in new[] { 3.5f, 12.5f }) Window(b, front, holes[Front], new Rect(u0, u0 + 2, 2, 5.5f), tall, NightPart, t);
            // The left face runs from the back to the front, the right one from the front to the back.
            foreach (float z0 in HallSideWindows)
            {
                Window(b, faces[Left], holes[Left], new Rect(d - z0 - 1, d - z0, 2, 5.5f), tall, NightPart, t);
                Window(b, faces[Right], holes[Right], new Rect(z0, z0 + 1, 2, 5.5f), tall, NightPart, t);
            }
            for (int k = 0; k < 4; k++) { WallFace(b, faces[k], 0, ContactHeight, holes[k]); WallFace(b, faces[k], ContactHeight, h, holes[k]); }
            Belt(b, faces, 0, 0.3f, 0.05f, VoxelPalette.Stone, true, new List<(int, float, float)> { (Front, door.U0 - 0.5f, door.U1 + 0.5f) });
            Belt(b, faces, h - 0.5f, h, 0.1f, VoxelPalette.StoneLight);
            foreach (float u0 in new[] { 2.5f, 6.25f, 11.25f, 15f })
            {
                Ledge(b, front, u0, u0 + 0.5f, 0.3f, h - 0.5f, 0.1f, VoxelPalette.StoneLight, true, false);
                Ledge(b, faces[Back], u0, u0 + 0.5f, 0.3f, h - 0.5f, 0.1f, VoxelPalette.StoneLight, true, false);
            }
            foreach (float z0 in new[] { 1.25f, 4.75f, 8f })
            {
                Ledge(b, faces[Left], d - z0 - 0.5f, d - z0, 0.3f, h - 0.5f, 0.1f, VoxelPalette.StoneLight, true, false);
                Ledge(b, faces[Right], z0, z0 + 0.5f, 0.3f, h - 0.5f, 0.1f, VoxelPalette.StoneLight, true, false);
            }
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    float cx = sx * w / 2, cz = sz < 0 ? 0 : d;
                    b.Box(new V3(Math.Min(cx + sx * 0.1f, cx - sx * 0.5f), 0.3f, Math.Min(cz + sz * 0.1f, cz - sz * 0.5f)),
                        new V3(Math.Max(cx + sx * 0.1f, cx - sx * 0.5f), h - 0.5f, Math.Max(cz + sz * 0.1f, cz - sz * 0.5f)), 0, TrimPart, VoxelPalette.StoneLight);
                }
            // The room; the inner faces mirror the outer u.
            const float ix = w / 2 - t, iz0 = t, iz1 = d - t;
            var inner = new[]
            {
                new Face(new V3(ix, 0, iz0), new V3(-1, 0, 0), new V3(0, 0, 1), 2 * ix),
                new Face(new V3(-ix, 0, iz1), new V3(1, 0, 0), new V3(0, 0, -1), 2 * ix),
                new Face(new V3(-ix, 0, iz0), new V3(0, 0, 1), new V3(1, 0, 0), iz1 - iz0),
                new Face(new V3(ix, 0, iz1), new V3(0, 0, -1), new V3(-1, 0, 0), iz1 - iz0),
            };
            for (int k = 0; k < 4; k++)
            {
                float flip = k == Front || k == Back ? w - t : d - t;
                var openings = new List<Rect>();
                foreach (var r in holes[k]) openings.Add(new Rect(flip - r.U1, flip - r.U0, r.V0, r.V1));
                openings.Sort((p, q) => p.U0.CompareTo(q.U0));
                var f = inner[k];
                WallFace(b, f, 0, 1.0f, openings, TrimPart, VoxelPalette.WoodDark);
                WallFace(b, f, 1.0f, 1.12f, openings, TrimPart, VoxelPalette.Wood);
                WallFace(b, f, 1.12f, h, openings, WallPart, 0, 0.95f);
                float from = 0;
                foreach (var r in openings)
                {
                    if (r.V0 >= 1.12f) continue;
                    if (r.U0 > from) Ledge(b, f, from, r.U0, 1.0f, 1.12f, 0.03f, VoxelPalette.Wood);
                    from = r.U1;
                }
                Ledge(b, f, from, f.Length, 1.0f, 1.12f, 0.03f, VoxelPalette.Wood);
                Ledge(b, f, 0, f.Length, h - 0.45f, h - 0.1f, 0.12f, VoxelPalette.StoneLight);
            }
            return b.Build(PaletteUv);
        }

        // ---------- Roof ----------
        // Entablature and pediments over the hall and the colonnade, 25 × 15.75 m: a stone architrave, a stucco frieze
        // with triglyphs (the plain middle of the front carries the scene's «БИРЖА»), a bed moulding and the cornice, a
        // low gable roof in sage with a ridge cap, framed pediments with recessed tympana and a brass medallion in front.
        // Pivot at the middle of the front edge, on the column tops.
        public const float RoofWidth = 25, RoofDepth = 15.75f, RoofEave = 1.25f, RoofRidge = 4.4f;
        public static PolyMesh ExchangeRoof()
        {
            var b = NewBuilder(); b.Ambient = p => 1;
            const float x = RoofWidth / 2, d = RoofDepth, frieze = 0.25f;
            b.Box(new V3(-x + 0.22f, 0, 0.22f), new V3(x - 0.22f, 0.5f, d - 0.22f), 0, TrimPart, VoxelPalette.StoneLight);
            b.Box(new V3(-x + frieze, 0.5f, frieze), new V3(x - frieze, 0.95f, d - frieze), 0, WallPart, 0);
            b.Box(new V3(-x + 0.12f, 0.92f, 0.12f), new V3(x - 0.12f, 1.0f, d - 0.12f), 0, TrimPart, VoxelPalette.StoneLight);
            b.Box(new V3(-x, 1.0f, 0), new V3(x, RoofEave, d), 0.02f, TrimPart, VoxelPalette.StoneLight);
            // Triglyphs: either side of the name in front, along the back and the sides, wrapping the corners.
            float fl = 2 * (x - frieze), sl = d - 2 * frieze;
            var fronts = new[]
            {
                new Face(new V3(-x + frieze, 0, frieze), new V3(1, 0, 0), new V3(0, 0, -1), fl),
                new Face(new V3(x - frieze, 0, d - frieze), new V3(-1, 0, 0), new V3(0, 0, 1), fl),
                new Face(new V3(-x + frieze, 0, d - frieze), new V3(0, 0, -1), new V3(-1, 0, 0), sl),
                new Face(new V3(x - frieze, 0, frieze), new V3(0, 0, 1), new V3(1, 0, 0), sl),
            };
            // About 1.7 m apart from the corner ones (centred 0.25 m in from each corner); none over |x| < 5 in front.
            for (int k = 1; k <= 4; k++) { float cx = fl / 2 - 0.25f - 1.7f * k; Triglyph(b, fronts[Front], fl / 2 - cx); Triglyph(b, fronts[Front], fl / 2 + cx); }
            for (int k = 1; k < 14; k++) Triglyph(b, fronts[Back], 0.25f + (fl - 0.5f) * k / 14);
            int along = (int)Math.Round((sl - 0.5f) / 1.75f);
            for (int k = 1; k < along; k++) { float u = 0.25f + (sl - 0.5f) * k / along; Triglyph(b, fronts[Left], u); Triglyph(b, fronts[Right], u); }
            for (int k = 0; k < 4; k++)
            {
                Ledge(b, fronts[k], k < 2 ? -0.06f : 0, 0.5f, 0.5f, 0.92f, 0.06f, VoxelPalette.Stone, true, true);
                Ledge(b, fronts[k], fronts[k].Length - 0.5f, fronts[k].Length + (k < 2 ? 0.06f : 0), 0.5f, 0.92f, 0.06f, VoxelPalette.Stone, true, true);
            }
            Pediment(b, x, RoofEave, RoofRidge, 0, -1, true);
            Pediment(b, x, RoofEave, RoofRidge, d, 1, false);
            // Roof slabs from the eaves to the ridge, overhanging the pediments, and the ridge cap.
            const float lift = 0.08f, over = 0.08f;
            foreach (float side in new[] { -1f, 1f })
            {
                var lower = new[] { new V3(side * x, RoofEave, -over), new V3(0, RoofRidge, -over), new V3(0, RoofRidge, d + over), new V3(side * x, RoofEave, d + over) };
                var upper = new V3[4];
                for (int k = 0; k < 4; k++) upper[k] = lower[k] + new V3(0, lift, 0);
                PolyShapes.Block(b, lower, upper, TrimPart, VoxelPalette.PlasterSage);
            }
            b.Box(new V3(-0.14f, RoofRidge, -over - 0.02f), new V3(0.14f, RoofRidge + lift + 0.06f, d + over + 0.02f), 0.02f, TrimPart, VoxelPalette.StoneLight);
            return b.Build(PaletteUv);
        }
        // A stone block proud of the frieze with two dark grooves, centred at u.
        private static void Triglyph(PolyBuilder b, Face f, float u)
        {
            const float half = 0.25f, proud = 0.06f, v0 = 0.5f, v1 = 0.92f;
            Ledge(b, f, u - half, u + half, v0, v1, proud, VoxelPalette.Stone, true, true);
            foreach (float g in new[] { -0.09f, 0.09f })
                Panel(b, f, u + g - 0.03f, u + g + 0.03f, v0 + 0.05f, v1, proud + 0.002f, TrimPart, VoxelPalette.StoneDark, 1);
        }
        // Gable end from (±half, y0) to the apex: a stone frame in three mitred bands, the tympanum recessed behind it,
        // and on the front a brass medallion with an amber boss. facing −1 looks −Z (the front), +1 looks +Z.
        private static void Pediment(PolyBuilder b, float half, float y0, float apex, float z, int facing, bool medallion)
        {
            const float frame = 0.5f, bottom = 0.3f, recess = 0.25f;
            float slope = (apex - y0) / half, offset = frame * (float)Math.Sqrt(1 + slope * slope);
            float iy0 = y0 + bottom, iapex = apex - offset, ihalf = (iapex - iy0) / slope;
            var n = new V3(0, 0, facing); float zr = z - facing * recess;
            V3 o1 = new V3(-half, y0, z), o2 = new V3(half, y0, z), o3 = new V3(0, apex, z);
            V3 i1 = new V3(-ihalf, iy0, z), i2 = new V3(ihalf, iy0, z), i3 = new V3(0, iapex, z);
            b.Quad(o1, o2, i2, i1, n, TrimPart, VoxelPalette.StoneLight);
            b.Quad(o2, o3, i3, i2, n, TrimPart, VoxelPalette.StoneLight);
            b.Quad(o3, o1, i1, i3, n, TrimPart, VoxelPalette.StoneLight);
            b.Triangle(new V3(-ihalf, iy0, zr), new V3(ihalf, iy0, zr), new V3(0, iapex, zr), n, TrimPart, VoxelPalette.Stone);
            var centre = (i1 + i2 + i3) * (1f / 3);
            foreach (var (a, c) in new[] { (i1, i2), (i2, i3), (i3, i1) })
                b.Quad(a, c, new V3(c.X, c.Y, zr), new V3(a.X, a.Y, zr), centre - (a + c) * 0.5f, TrimPart, VoxelPalette.Stone, 0.8f);
            if (!medallion) return;
            float my = iy0 + (iapex - iy0) * 0.42f;
            PolyShapes.Limb(b, new V3(0, my, zr), new V3(0, my, zr + facing * 0.2f), 0.8f, 0.8f, 20, MetalPart, VoxelPalette.Brass);
            PolyShapes.Limb(b, new V3(0, my, zr + facing * 0.2f), new V3(0, my, zr + facing * 0.27f), 0.36f, 0.3f, 16, TrimPart, VoxelPalette.Amber);
        }

        // ---------- Colonnade ----------
        // Fluted column, 7 m: a square plinth, a moulded base, a shaft of twenty flutes that tapers with a slight swell,
        // a necking ring, the echinus and the abacus. All on the wall material (light stucco stone).
        public const float ColumnHeight = 7, ColumnHalf = 0.6f;
        public static PolyMesh ExchangeColumn()
        {
            var b = NewBuilder();
            const float half = ColumnHalf;
            b.Box(new V3(-half, 0, -half), new V3(half, 0.2f, half), 0.02f, WallPart, 0);
            PolyShapes.Lathe(b, 0, 0, new[] { (0.2f, 0.555f), (0.24f, 0.585f), (0.3f, 0.585f), (0.34f, 0.55f), (0.37f, 0.5f), (0.41f, 0.495f), (0.45f, 0.47f) },
                24, WallPart, 0, 1, false, true);
            Fluted(b, 0.45f, 6.3f, 0.47f, 0.4f, 20, 0.035f);
            PolyShapes.Lathe(b, 0, 0, new[] { (6.3f, 0.4f), (6.33f, 0.43f), (6.39f, 0.43f), (6.42f, 0.41f), (6.5f, 0.43f), (6.62f, 0.49f), (6.72f, 0.555f), (6.8f, 0.6f) },
                24, WallPart, 0, 1, true, false);
            b.Box(new V3(-half, 6.8f, -half), new V3(half, ColumnHeight, half), 0.02f, WallPart, 0);
            return b.Build(PaletteUv);
        }
        // Shaft with `flutes` V grooves `depth` deep, from r0 at y0 to r1 at y1 with a slight swell at a third.
        private static void Fluted(PolyBuilder b, float y0, float y1, float r0, float r1, int flutes, float depth)
        {
            float[] ys = { y0, y0 + (y1 - y0) / 3, y1 };
            float[] rs = { r0, r0 + (r1 - r0) / 3 + 0.012f, r1 };
            int n = 2 * flutes;
            V3 P(int level, int k)
            {
                double a = 2 * Math.PI * k / n; float r = rs[level] - (k % 2 == 1 ? depth : 0);
                return new V3(r * (float)Math.Cos(a), ys[level], r * (float)Math.Sin(a));
            }
            for (int level = 0; level < 2; level++)
                for (int k = 0; k < n; k++)
                {
                    double am = 2 * Math.PI * (k + 0.5) / n;
                    b.Quad(P(level, k), P(level, k + 1), P(level + 1, k + 1), P(level + 1, k), new V3((float)Math.Cos(am), 0, (float)Math.Sin(am)), WallPart, 0);
                }
        }

        // ---------- Quote board ----------
        // 8.4 × 3.4 m on the back wall: a brass body and rim, six dark rows for the scene's text under a navy header, a
        // dot-matrix ticker glowing along the bottom. Front −Z; pivot at the bottom centre of the 0.2 m slab, the face
        // 0.1 m in front of it and the rim 0.115 m.
        public const float BoardWidth = 8.4f, BoardHeight = 3.4f, BoardFace = -0.1f, BoardRim = -0.115f;
        public static PolyMesh QuoteBoard()
        {
            var b = NewBuilder(); b.Ambient = p => 1;
            const float x = BoardWidth / 2, h = BoardHeight, face = BoardFace, rim = BoardRim, inset = 0.2f;
            b.Box(new V3(-x, 0, face + 0.01f), new V3(x, h, 0.1f), 0.015f, MetalPart, VoxelPalette.Brass);
            void Row(float y0, float y1, float o, float x0, float x1, int part, byte color)
                => b.Quad(new V3(x0, y0, o), new V3(x1, y0, o), new V3(x1, y1, o), new V3(x0, y1, o), new V3(0, 0, -1), part, color);
            for (int row = 0; row < 6; row++) Row(0.4f + row * 0.4f, 0.8f + row * 0.4f, face, -x + inset, x - inset, TrimPart, row % 2 == 1 ? VoxelPalette.CarBlack : VoxelPalette.Ink);
            Row(2.8f, 3.2f, face, -x + inset, x - inset, TrimPart, VoxelPalette.Navy);
            Row(0.1f, 0.4f, face, -x + inset, x - inset, TrimPart, VoxelPalette.Ink);
            for (float u = -x + inset + 0.03f; u + 0.12f <= x - inset; u += 0.16f) Row(0.13f, 0.31f, face - 0.002f, u, u + 0.12f, GlowPart, VoxelPalette.ScreenGreen);
            b.Box(new V3(-x, 0, rim), new V3(x, 0.1f, face + 0.01f), 0, MetalPart, VoxelPalette.Brass);
            b.Box(new V3(-x, h - 0.2f, rim), new V3(x, h, face + 0.01f), 0, MetalPart, VoxelPalette.Brass);
            b.Box(new V3(-x, 0.1f, rim), new V3(-x + inset, h - 0.2f, face + 0.01f), 0, MetalPart, VoxelPalette.Brass);
            b.Box(new V3(x - inset, 0.1f, rim), new V3(x, h - 0.2f, face + 0.01f), 0, MetalPart, VoxelPalette.Brass);
            return b.Build(PaletteUv);
        }

        // ---------- Bull and bear ----------
        // On a dark granite pedestal (the wall material) with a stone slab and brass plaques, 2.6 m in all, within the
        // voxel model's 1.4 × 2.6 m: a charging brass bull with cream horns, or a bronze bear. Heads −Z.
        public const float StatueHalfX = 0.7f, StatueHalfZ = 1.3f, StatueHeight = 2.6f, StatueSlab = 1.0f;
        public static PolyMesh ExchangeStatue(bool bull)
        {
            var b = NewBuilder(40);
            const float hx = StatueHalfX, hz = StatueHalfZ, s = StatueSlab;
            b.Box(new V3(-hx + 0.03f, 0, -hz + 0.03f), new V3(hx - 0.03f, 0.9f, hz - 0.03f), 0.02f, WallPart, 0);
            b.Box(new V3(-hx, 0.9f, -hz), new V3(hx, s, hz), 0.02f, TrimPart, VoxelPalette.Stone);
            foreach (float side in new[] { -1f, 1f })
                b.Box(new V3(side < 0 ? -hx : hx - 0.04f, 0.3f, -0.6f), new V3(side < 0 ? -hx + 0.04f : hx, 0.6f, 0.6f), 0.01f, MetalPart, VoxelPalette.Brass);
            byte metal = bull ? VoxelPalette.Brass : VoxelPalette.Wood;
            if (bull)
            {
                foreach (float lx in new[] { -0.35f, 0.35f })
                    foreach (float lz in new[] { -0.55f, 0.65f })
                    {
                        PolyShapes.Limb(b, new V3(lx, s, lz), new V3(lx, s + 0.1f, lz), 0.145f, 0.14f, 8, TrimPart, VoxelPalette.WoodDark);
                        PolyShapes.Limb(b, new V3(lx, s + 0.1f, lz), new V3(lx * 0.92f, s + 0.7f, lz * 0.95f), 0.12f, 0.16f, 8, MetalPart, metal);
                    }
                PolyShapes.Blob(b, new V3(0, 1.85f, 0.1f), 0.55f, 0.4f, 0.9f, 12, 8, MetalPart, metal);
                PolyShapes.Blob(b, new V3(0, 2.05f, -0.4f), 0.5f, 0.36f, 0.45f, 12, 8, MetalPart, metal);
                PolyShapes.Blob(b, new V3(0, 1.65f, -0.9f), 0.3f, 0.3f, 0.34f, 10, 6, MetalPart, metal);
                PolyShapes.Blob(b, new V3(0, 1.5f, -1.12f), 0.2f, 0.14f, 0.14f, 8, 6, MetalPart, metal);
                foreach (float side in new[] { -1f, 1f })
                {
                    PolyShapes.Limb(b, new V3(side * 0.22f, 1.85f, -0.95f), new V3(side * 0.42f, 1.95f, -1.0f), 0.06f, 0.05f, 6, TrimPart, VoxelPalette.Cream);
                    PolyShapes.Limb(b, new V3(side * 0.42f, 1.95f, -1.0f), new V3(side * 0.5f, 2.12f, -1.12f), 0.05f, 0.015f, 6, TrimPart, VoxelPalette.Cream);
                    b.Box(new V3(side * 0.2f - 0.03f, 1.7f, -1.2f), new V3(side * 0.2f + 0.03f, 1.76f, -1.14f), 0, TrimPart, VoxelPalette.Ink);
                }
                PolyShapes.Limb(b, new V3(0, 2.0f, 0.95f), new V3(0, 1.48f, 1.12f), 0.04f, 0.03f, 6, MetalPart, metal);
                PolyShapes.Blob(b, new V3(0, 1.44f, 1.13f), 0.07f, 0.09f, 0.07f, 6, 4, TrimPart, VoxelPalette.WoodDark);
            }
            else
            {
                foreach (float lx in new[] { -0.4f, 0.4f })
                    foreach (float lz in new[] { -0.5f, 0.6f })
                    {
                        PolyShapes.Limb(b, new V3(lx, s, lz), new V3(lx, s + 0.08f, lz), 0.2f, 0.2f, 8, TrimPart, VoxelPalette.WoodDark);
                        PolyShapes.Limb(b, new V3(lx, s + 0.08f, lz), new V3(lx * 0.95f, s + 0.6f, lz), 0.18f, 0.21f, 8, MetalPart, metal);
                    }
                PolyShapes.Blob(b, new V3(0, 1.8f, 0.1f), 0.6f, 0.46f, 0.9f, 12, 8, MetalPart, metal);
                PolyShapes.Blob(b, new V3(0, 2.0f, -0.35f), 0.56f, 0.42f, 0.48f, 12, 8, MetalPart, metal);
                PolyShapes.Blob(b, new V3(0, 1.62f, -0.88f), 0.34f, 0.3f, 0.34f, 10, 6, MetalPart, metal);
                PolyShapes.Blob(b, new V3(0, 1.52f, -1.16f), 0.16f, 0.13f, 0.13f, 8, 6, MetalPart, metal);
                PolyShapes.Blob(b, new V3(0, 1.58f, -1.27f), 0.05f, 0.04f, 0.03f, 6, 4, TrimPart, VoxelPalette.Ink);
                foreach (float side in new[] { -1f, 1f })
                {
                    PolyShapes.Blob(b, new V3(side * 0.22f, 1.9f, -0.86f), 0.09f, 0.08f, 0.06f, 8, 4, MetalPart, metal);
                    b.Box(new V3(side * 0.17f - 0.03f, 1.68f, -1.19f), new V3(side * 0.17f + 0.03f, 1.74f, -1.13f), 0, TrimPart, VoxelPalette.Ink);
                }
                PolyShapes.Blob(b, new V3(0, 1.9f, 1.0f), 0.09f, 0.09f, 0.08f, 8, 4, MetalPart, metal);
            }
            return b.Build(PaletteUv);
        }

        // ---------- Rostral column ----------
        // 12.2 m on a 2.6 m square: a dark base, a granite pedestal (the wall material) with brass plaques and a stone cap,
        // a red shaft with brass rings and pairs of ship prows on four levels (turning a quarter each level), a stone
        // capital and a brass brazier with a glowing flame.
        public const float RostralHalf = 1.3f, RostralHeight = 12.2f;
        public static PolyMesh RostralColumn()
        {
            var b = NewBuilder();
            b.Box(new V3(-1.3f, 0, -1.3f), new V3(1.3f, 0.4f, 1.3f), 0.02f, TrimPart, VoxelPalette.StoneDark);
            b.Box(new V3(-1.1f, 0.4f, -1.1f), new V3(1.1f, 2.2f, 1.1f), 0.02f, WallPart, 0);
            foreach (float side in new[] { -1f, 1f })
            {
                b.Box(new V3(-0.5f, 0.9f, side < 0 ? -1.16f : 1.1f), new V3(0.5f, 1.5f, side < 0 ? -1.1f : 1.16f), 0.01f, MetalPart, VoxelPalette.Brass);
                b.Box(new V3(side < 0 ? -1.16f : 1.1f, 0.9f, -0.5f), new V3(side < 0 ? -1.1f : 1.16f, 1.5f, 0.5f), 0.01f, MetalPart, VoxelPalette.Brass);
            }
            b.Box(new V3(-1.2f, 2.2f, -1.2f), new V3(1.2f, 2.5f, 1.2f), 0.03f, TrimPart, VoxelPalette.StoneLight);
            PolyShapes.Lathe(b, 0, 0, new[] { (2.5f, 0.56f), (2.7f, 0.56f) }, 16, TrimPart, VoxelPalette.StoneLight, 1, false, true);
            PolyShapes.Lathe(b, 0, 0, new[] { (2.7f, 0.46f), (10.3f, 0.46f) }, 16, TrimPart, VoxelPalette.BrickRed);
            for (int level = 0; level < 4; level++)
            {
                float y0 = 3.6f + 1.7f * level;
                PolyShapes.Lathe(b, 0, 0, new[] { (y0 + 0.55f, 0.5f), (y0 + 0.67f, 0.5f) }, 16, MetalPart, VoxelPalette.Brass, 1, true, true);
                bool alongX = level % 2 == 0;
                foreach (float side in new[] { -1f, 1f })
                {
                    V3 P(float a, float t, float y) => alongX ? new V3(side * a, y, t) : new V3(t, y, side * a);
                    var lower = new[] { P(0.4f, -0.15f, y0), P(1.12f, -0.03f, y0 + 0.28f), P(1.12f, 0.03f, y0 + 0.28f), P(0.4f, 0.15f, y0) };
                    var upper = new[] { P(0.4f, -0.15f, y0 + 0.5f), P(1.12f, -0.03f, y0 + 0.36f), P(1.12f, 0.03f, y0 + 0.36f), P(0.4f, 0.15f, y0 + 0.5f) };
                    PolyShapes.Block(b, lower, upper, MetalPart, VoxelPalette.Brass);
                }
            }
            b.Box(new V3(-0.6f, 10.3f, -0.6f), new V3(0.6f, 10.5f, 0.6f), 0.02f, TrimPart, VoxelPalette.StoneLight);
            b.Box(new V3(-0.7f, 10.5f, -0.7f), new V3(0.7f, 10.7f, 0.7f), 0.02f, TrimPart, VoxelPalette.StoneLight);
            PolyShapes.Lathe(b, 0, 0, new[] { (10.7f, 0.2f), (10.95f, 0.2f), (11.05f, 0.42f), (11.2f, 0.5f), (11.4f, 0.56f) }, 16, MetalPart, VoxelPalette.Brass, 1, false, true);
            PolyShapes.Lathe(b, 0, 0, new[] { (11.4f, 0.44f), (11.7f, 0.34f), (12.0f, 0.14f), (RostralHeight, 0) }, 7, GlowPart, VoxelPalette.SignalAmber);
            for (int k = 0; k < 3; k++)
            {
                double a = 2 * Math.PI * k / 3 + 0.4;
                var foot = new V3(0.22f * (float)Math.Cos(a), 11.45f, 0.22f * (float)Math.Sin(a));
                PolyShapes.Limb(b, foot, foot + new V3(0.1f * (float)Math.Cos(a), 0.5f + 0.08f * k, 0.1f * (float)Math.Sin(a)), 0.12f, 0.01f, 5, GlowPart, VoxelPalette.LampWarm);
            }
            return b.Build(PaletteUv);
        }
    }
}
