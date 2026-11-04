using System;
using System.Collections.Generic;

namespace Broker.Core
{
    // Trees, conifers, shrubs, planters and the Exchange lawns: crowns, tiers and shrubs are lumpy clumps on the "leaves"
    // wall material, darker underneath; trunks, kerbs, soil and flowers are palette trim. Pivots at the bottom centre and
    // the bounds inside the voxel models they replace (VoxelProps.RoundTree, Conifer, Shrub, Planter, Lawn).
    public static partial class StreetModels
    {
        public const int TreeKinds = 5, ConiferKinds = 3;
        // No crown comes down below this: walkers pass under the trees as before.
        public const float CrownBase = 2.2f;
        public const float LawnWidth = 6, LawnDepth = 5, LawnKerb = 0.3f, LawnGrass = 0.2f;
        public const float PlanterWidth = 2.25f, PlanterDepth = 1.25f, PlanterSoil = 0.6f, PlanterTop = 0.625f;
        private const int WallPart = (int)BuildingPart.Wall, TrimPart = (int)BuildingPart.Trim, GlowPart = (int)BuildingPart.Glow;
        private const float SmoothAngle = 50;

        // Each tree kind has a green of its own over the leaves surface; the conifers a dark cool one.
        private static readonly uint[] TreeTints = { 0xFFFFFF, 0xF4FBD2, 0xDCEAD2, 0xFFFFE2, 0xE4EEDC };
        public static WallLook TreeLook(int seed) => new WallLook("leaves", TreeTints[Mod(seed, TreeTints.Length)]);
        public static readonly WallLook ConiferLook = new WallLook("leaves", 0x92C0EC);
        // The same material as the hedge (a field of the other file is not safe to copy in a static initialiser).
        public static readonly WallLook ShrubLook = new WallLook("leaves", 0xFFFFFF);
        public static readonly WallLook LawnLook = new WallLook("grass", 0xFFFFFF);
        private static readonly byte[] FlowerColors = { VoxelPalette.FlowerPink, VoxelPalette.FlowerPink, VoxelPalette.FlowerYellow, VoxelPalette.FlowerWhite };
        // Lawn clumps add red.
        private static readonly byte[] BedColors = { VoxelPalette.FlowerPink, VoxelPalette.FlowerYellow, VoxelPalette.FlowerWhite, VoxelPalette.CarRed };

        private static PolyBuilder NewBuilder() => new PolyBuilder(BuildingModels.PartCount, SmoothAngle, WallPart, VoxelSurfaces.MetresPerTile) { Ambient = BuildingModels.Contact };
        private static float Rand(int seed, int salt) => VoxelNoise.Value(salt, 17, 7, seed);
        private static int Mod(int value, int n) => (value % n + n) % n;
        private static float SmoothStep(float a, float b, float x) { float t = Math.Max(0, Math.Min(1, (x - a) / (b - a))); return t * t * (3 - 2 * t); }
        // Foliage is darker underneath and inside, lighter on top.
        private static Func<V3, float> Foliage(float bottom, float top) => p => 0.7f + 0.3f * SmoothStep(bottom, top, p.Y);

        // ---------- Trees ----------
        // Broadleaf tree 4 × 6.5 m at most: a trunk with a flared foot and two or three limbs into a crown of a main clump,
        // four or five clumps round it and one on top. The seed sets the height of the trunk, the clumps and the bumps.
        public static PolyMesh Tree(int seed)
        {
            var b = NewBuilder();
            float trunk = 2.3f + 0.5f * Rand(seed, 1), cy = trunk + 1.45f, main = 1.45f + 0.15f * Rand(seed, 2);
            PolyShapes.Lathe(b, 0, 0, new[] { (0f, 0.25f), (0.07f, 0.2f), (0.3f, 0.155f), (trunk * 0.6f, 0.13f), (cy, 0.1f) }, 7, TrimPart, VoxelPalette.Bark,
                phase: 6.2832f * Rand(seed, 3));
            int count = 4 + (int)(Rand(seed, 4) * 2);
            float turn = 6.2832f * Rand(seed, 5);
            var clumps = new List<(V3 c, float r)>();
            for (int j = 0; j < count; j++)
            {
                double a = turn + 2 * Math.PI * (j + 0.3f * (Rand(seed, 10 + j) - 0.5f)) / count;
                float d = 0.85f + 0.15f * Rand(seed, 20 + j), r = 0.75f + 0.15f * Rand(seed, 30 + j);
                float h = j % 2 == 0 ? -0.15f + 0.25f * Rand(seed, 40 + j) : 0.25f + 0.35f * Rand(seed, 40 + j);
                clumps.Add((new V3(d * (float)Math.Cos(a), cy + h, d * (float)Math.Sin(a)), r));
            }
            // Limbs from below the crown into three of the clumps; their open ends hide in the trunk and the leaves.
            for (int j = 0; j < 3; j++)
            {
                var (c, r) = clumps[j];
                PolyShapes.Limb(b, new V3(0, trunk * (0.74f + 0.08f * j), 0), new V3(c.X * 0.7f, c.Y - 0.3f * r, c.Z * 0.7f), 0.075f, 0.035f, 5,
                    TrimPart, VoxelPalette.Bark, 1, false);
            }
            b.Ambient = Foliage(trunk, cy + 1.6f);
            PolyShapes.Lump(b, new V3(0, cy, 0), main, 0.85f * main, main, 11, 8, 0.07f, seed * 7 + 1, WallPart, 0);
            for (int j = 0; j < count; j++)
            {
                var (c, r) = clumps[j];
                PolyShapes.Lump(b, c, r, 0.9f * r, r, 9, 6, 0.08f, seed * 7 + 2 + j, WallPart, 0);
            }
            PolyShapes.Lump(b, new V3(0.2f * (Rand(seed, 6) - 0.5f), cy + 0.85f, 0.2f * (Rand(seed, 7) - 0.5f)), 0.85f, 0.72f, 0.85f, 9, 6, 0.08f, seed * 7 + 9, WallPart, 0);
            return b.Build(BuildingModels.PaletteUv);
        }

        // Conifer 3 × 7.5 m at most: a short trunk and five drooping tiers with ragged rims, the top one a point.
        public static PolyMesh Conifer(int seed)
        {
            var b = NewBuilder();
            PolyShapes.Lathe(b, 0, 0, new[] { (0f, 0.17f), (0.06f, 0.14f), (1.2f, 0.11f), (6.4f, 0.05f) }, 6, TrimPart, VoxelPalette.BarkDark, phase: 6.2832f * Rand(seed, 1));
            b.Ambient = Foliage(1.0f, 7.4f);
            for (int tier = 0; tier < 5; tier++)
            {
                float y = 1.05f + 1.22f * tier + 0.06f * (Rand(seed, 50 + tier) - 0.5f);
                float rim = (1.36f - 0.225f * tier) * (0.97f + 0.05f * Rand(seed, 60 + tier));
                float h = tier == 4 ? 7.42f - y : 1.75f - 0.08f * tier;
                Tier(b, y, rim, h, 11 - tier / 2, seed * 13 + tier);
            }
            return b.Build(BuildingModels.PaletteUv);
        }
        // One tier: from the trunk under it out to a ragged drooping rim at y, then up, a little hollow, to its tip h above.
        private static void Tier(PolyBuilder b, float y, float rim, float h, int sides, int seed)
        {
            var profile = new (float y, float r, float jitter)[] { (y + 0.32f, 0.035f, 0), (y, rim, 1), (y + 0.3f * h, 0.64f * rim, 0.5f), (y + 0.62f * h, 0.33f * rim, 0.3f), (y + h, 0, 0) };
            int rings = profile.Length;
            var points = new V3[rings, sides];
            for (int i = 0; i < rings; i++)
                for (int k = 0; k < sides; k++)
                {
                    var (py, pr, jitter) = profile[i];
                    double a = 2 * Math.PI * (k + 0.5f * Rand(seed, 200 + k) * jitter) / sides;
                    float r = pr * (1 + 0.07f * jitter * (2 * Rand(seed, 100 + i * 16 + k) - 1)), dy = 0.08f * jitter * (2 * Rand(seed, 300 + i * 16 + k) - 1);
                    points[i, k] = new V3(r * (float)Math.Cos(a), py + dy, r * (float)Math.Sin(a));
                }
            int group = b.Group; b.Group = b.NextGroup();
            for (int i = 0; i + 1 < rings; i++)
            {
                float dy = profile[i + 1].y - profile[i].y, dr = profile[i + 1].r - profile[i].r;
                for (int k = 0; k < sides; k++)
                {
                    int k1 = (k + 1) % sides;
                    double am = 2 * Math.PI * (k + 0.5f) / sides;
                    var outward = new V3((float)Math.Cos(am) * dy, -dr, (float)Math.Sin(am) * dy);
                    b.Quad(points[i, k], points[i, k1], points[i + 1, k1], points[i + 1, k], outward, WallPart, 0, i == 0 ? 0.8f : 1);
                }
            }
            b.Group = group;
        }

        // ---------- Shrubs ----------
        // Shrub dome width × height × depth on a flat base: a main clump, four lower ones round it and a small one on top,
        // with small pink, yellow and white flowers over it when asked.
        public static PolyMesh Shrub(float width, float height, float depth, int seed, bool flowers)
        {
            if (!(width >= 0.3f) || !(height >= 0.2f) || !(depth >= 0.3f) || width > 6 || height > 3 || depth > 6) throw new ArgumentOutOfRangeException(nameof(width));
            var b = NewBuilder();
            AddShrub(b, new V3(0, 0, 0), width, height, depth, seed, flowers);
            return b.Build(BuildingModels.PaletteUv);
        }
        private static void AddShrub(PolyBuilder b, V3 at, float w, float h, float d, int seed, bool flowers)
        {
            var saved = b.Ambient;
            b.Ambient = Foliage(at.Y, at.Y + h);
            float jx = 0.04f * w * (2 * Rand(seed, 1) - 1);
            var clumps = new (V3 c, float rx, float ry, float rz)[]
            {
                (at, 0.34f * w, 0.88f * h, 0.44f * d),
                (at + new V3(-0.23f * w, 0, -0.1f * d), 0.24f * w, (0.6f + 0.08f * Rand(seed, 2)) * h, 0.34f * d),
                (at + new V3(0.23f * w, 0, 0.1f * d), 0.24f * w, (0.62f + 0.08f * Rand(seed, 3)) * h, 0.34f * d),
                (at + new V3(-0.06f * w + jx, 0, 0.18f * d), 0.22f * w, (0.52f + 0.08f * Rand(seed, 4)) * h, 0.28f * d),
                (at + new V3(0.08f * w + jx, 0, -0.18f * d), 0.22f * w, (0.55f + 0.08f * Rand(seed, 5)) * h, 0.28f * d),
            };
            for (int i = 0; i < clumps.Length; i++)
                PolyShapes.Lump(b, clumps[i].c, clumps[i].rx, clumps[i].ry, clumps[i].rz, 10, 8, 0.1f, seed * 5 + i, WallPart, 0, 1, true);
            PolyShapes.Lump(b, at + new V3(jx, 0.62f * h, 0), 0.2f * w, 0.3f * h, 0.25f * d, 8, 6, 0.1f, seed * 5 + 7, WallPart, 0);
            b.Ambient = saved;
            if (!flowers) return;
            int n = (int)Math.Round(w * d * 6);
            for (int f = 0; f < n; f++)
            {
                var (c, rx, ry, rz) = clumps[f % 3 == 2 ? 1 + f / 3 % 4 : 0];
                double a = 2 * Math.PI * Rand(seed, 500 + f), e = Math.PI / 180 * (22 + 50 * Rand(seed, 600 + f));
                float x = c.X + rx * (float)(Math.Cos(e) * Math.Cos(a)), y = c.Y + ry * (float)Math.Sin(e), z = c.Z + rz * (float)(Math.Cos(e) * Math.Sin(a));
                x = Math.Max(at.X - w / 2 + 0.08f, Math.Min(at.X + w / 2 - 0.08f, x)); z = Math.Max(at.Z - d / 2 + 0.08f, Math.Min(at.Z + d / 2 - 0.08f, z));
                Flower(b, new V3(x, Math.Min(at.Y + h - 0.05f, y), z), FlowerColors[(int)(Rand(seed, 700 + f) * FlowerColors.Length)]);
            }
        }
        // A small round flower head; on the lawns a cheaper one of six facets a side, seen from further off.
        private static void Flower(PolyBuilder b, V3 at, byte color, bool small = false)
            => PolyShapes.Blob(b, at, 0.065f, 0.05f, 0.065f, 6, small ? 2 : 3, TrimPart, color);

        // Plaza planter 2.25 × 1.25 m: a dark plinth, a concrete trough and a pale capping round the soil, and a flowering
        // shrub on it.
        public static PolyMesh Planter(int seed)
        {
            var b = NewBuilder();
            const float hx = PlanterWidth / 2, hz = PlanterDepth / 2, cap = 0.11f, under = 0.53f;
            b.Box(new V3(-hx + 0.02f, 0, -hz + 0.02f), new V3(hx - 0.02f, 0.13f, hz - 0.02f), 0.015f, TrimPart, VoxelPalette.StoneDark);
            b.Box(new V3(-hx + 0.035f, 0.13f, -hz + 0.035f), new V3(hx - 0.035f, under, hz - 0.035f), 0.012f, TrimPart, VoxelPalette.Concrete);
            Frame(b, hx, hz, cap, under, PlanterTop, VoxelPalette.Curb);
            b.Quad(new V3(-hx + cap, PlanterSoil, -hz + cap), new V3(hx - cap, PlanterSoil, -hz + cap), new V3(hx - cap, PlanterSoil, hz - cap), new V3(-hx + cap, PlanterSoil, hz - cap),
                new V3(0, 1, 0), TrimPart, VoxelPalette.Soil);
            AddShrub(b, new V3(0, PlanterSoil, 0), 2.0f, 0.95f, 1.04f, seed, true);
            return b.Build(BuildingModels.PaletteUv);
        }
        // Four bevelled stones round a rectangle, `width` wide, from y0 to y1: the long ones front and back.
        private static void Frame(PolyBuilder b, float hx, float hz, float width, float y0, float y1, byte color)
        {
            const float bevel = 0.02f;
            b.Box(new V3(-hx, y0, -hz), new V3(hx, y1, -hz + width), bevel, TrimPart, color);
            b.Box(new V3(-hx, y0, hz - width), new V3(hx, y1, hz), bevel, TrimPart, color);
            b.Box(new V3(-hx, y0, -hz + width), new V3(-hx + width, y1, hz - width), bevel, TrimPart, color);
            b.Box(new V3(hx - width, y0, -hz + width), new V3(hx, y1, hz - width), bevel, TrimPart, color);
        }

        // ---------- Lawns ----------
        // Lawn of the Exchange square, 6 × 5 m: a stone kerb 0.3 m high round mown grass at 0.2 m (the wall material) with
        // clumps of flowers, each clump a low mound of leaves and flowers of one colour.
        public static PolyMesh Lawn(int seed)
        {
            var b = NewBuilder();
            const float hx = LawnWidth / 2, hz = LawnDepth / 2, kerb = 0.12f;
            Frame(b, hx, hz, kerb, 0, LawnKerb, VoxelPalette.StoneLight);
            b.Quad(new V3(-hx + kerb, LawnGrass, -hz + kerb), new V3(hx - kerb, LawnGrass, -hz + kerb), new V3(hx - kerb, LawnGrass, hz - kerb), new V3(-hx + kerb, LawnGrass, hz - kerb),
                new V3(0, 1, 0), WallPart, 0);
            // The kerb stones show their inner faces down to the grass; flower clumps on a jittered 6 × 5 grid, a few cells
            // left as plain grass.
            const int columns = 6, rows = 5;
            for (int gx = 0; gx < columns; gx++)
                for (int gz = 0; gz < rows; gz++)
                {
                    int cell = gx * rows + gz;
                    if (Rand(seed, 800 + cell) < 0.15f) continue;
                    float x = -hx + 0.45f + (gx + 0.15f + 0.7f * Rand(seed, 840 + cell)) * (2 * hx - 0.9f) / columns;
                    float z = -hz + 0.45f + (gz + 0.15f + 0.7f * Rand(seed, 880 + cell)) * (2 * hz - 0.9f) / rows;
                    Clump(b, new V3(x, LawnGrass, z), seed * 31 + cell);
                }
            return b.Build(BuildingModels.PaletteUv);
        }
        private static void Clump(PolyBuilder b, V3 at, int seed)
        {
            float r = 0.18f + 0.1f * Rand(seed, 1), h = 0.1f + 0.05f * Rand(seed, 2), rz = r * (0.8f + 0.3f * Rand(seed, 3));
            PolyShapes.Lump(b, at, r, h, rz, 8, 4, 0.1f, seed, TrimPart, VoxelPalette.LeafMid, 1, true);
            byte color = BedColors[(int)(Rand(seed, 4) * BedColors.Length)];
            int n = 6 + (int)(Rand(seed, 5) * 4);
            for (int f = 0; f < n; f++)
            {
                double a = 2 * Math.PI * (f + Rand(seed, 10 + f)) / n; float d = 0.25f + 0.45f * Rand(seed, 30 + f);
                float x = r * d * (float)Math.Cos(a), z = rz * d * (float)Math.Sin(a);
                Flower(b, at + new V3(x, h * (float)Math.Sqrt(Math.Max(0, 1 - d * d)) + 0.01f, z), color, true);
            }
        }
    }
}
