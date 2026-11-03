using System;

namespace Broker.Core
{
    public enum CarStyle { Sedan, Suv, Taxi }
    public enum PersonStyle { Lena, Max, Orlov }
    public enum TowerStyle { Brick, Glass, Plaster }
    public enum ShopStyle { BrickShop, Cafe, Office }
    public enum StandStyle { Map, Companies }

    // Four parts so Unity can turn the head around the neck and swing arms around the shoulders.
    // Every part has its pivot at the joint; offsets are in metres from the figure's feet.
    public sealed class VoxelFigure
    {
        public VoxelModel Body { get; } public VoxelModel Head { get; } public VoxelModel LeftArm { get; } public VoxelModel RightArm { get; }
        public float NeckY { get; } public float ShoulderX { get; } public float ShoulderY { get; }
        internal VoxelFigure(VoxelModel body, VoxelModel head, VoxelModel left, VoxelModel right, float neckY, float shoulderX, float shoulderY)
        { Body = body; Head = head; LeftArm = left; RightArm = right; NeckY = neckY; ShoulderX = shoulderX; ShoulderY = shoulderY; }
    }

    // Deterministic voxel scenery and furniture. No trigonometry: the same seed gives the same cells everywhere.
    // Fronts face −Z unless noted; cars face +X. Pivots are at the bottom centre of the footprint.
    public static class VoxelProps
    {
        public const float NatureVoxel = 0.25f, CarVoxel = 0.15f, PersonVoxel = 0.0625f, FurnitureVoxel = 0.05f, TowerVoxel = 0.5f, ShopVoxel = 0.25f;
        private const byte E = VoxelPalette.Empty;

        private static float Rand(int seed, int salt) => VoxelNoise.Value(salt, 7, 3, seed);
        private static int RandInt(int seed, int salt, int min, int maxExclusive)
            => min + (int)(VoxelNoise.Hash(salt, 11, 5, seed) % (uint)(maxExclusive - min));
        private static int Round(float value) => Math.Max(1, (int)Math.Floor(value + 0.5f));

        // ---------- Nature ----------
        public static VoxelModel RoundTree(int seed)
        {
            var m = new VoxelModel(16, 26, 16, NatureVoxel);
            int trunkTop = 8 + RandInt(seed, 1, 0, 3);
            for (int y = 0; y <= trunkTop + 2; y++)
                for (int z = 7; z <= 8; z++) for (int x = 7; x <= 8; x++)
                    m.Set(x, y, z, VoxelNoise.Value(x, y, z, seed) < 0.3f ? VoxelPalette.BarkDark : VoxelPalette.Bark);
            m.Set(6, 0, 7, VoxelPalette.Bark); m.Set(9, 0, 8, VoxelPalette.BarkDark); m.Set(7, 0, 9, VoxelPalette.Bark); m.Set(8, 0, 6, VoxelPalette.BarkDark);
            m.Set(6, trunkTop, 8, VoxelPalette.Bark); m.Set(5, trunkTop + 1, 8, VoxelPalette.Bark);
            m.Set(9, trunkTop + 1, 7, VoxelPalette.Bark); m.Set(10, trunkTop + 2, 7, VoxelPalette.Bark);
            float cy = trunkTop + 6.8f;
            Crown(m, 8, cy, 8, 6.4f, 5.4f, 6.4f, seed, 0);
            for (int i = 0; i < 3; i++)
            {
                float r = 3.2f + Rand(seed, 40 + i) * 1.4f;
                Crown(m, 8 + (Rand(seed, 10 + i) - 0.5f) * 6, cy + (Rand(seed, 30 + i) - 0.35f) * 4, 8 + (Rand(seed, 20 + i) - 0.5f) * 6, r, r * 0.85f, r, seed, i + 1);
            }
            return m;
        }
        private static void Crown(VoxelModel m, float cx, float cy, float cz, float rx, float ry, float rz, int seed, int salt)
        {
            float bottom = cy - ry, span = 2 * ry;
            m.Ellipsoid(cx, cy, cz, rx, ry, rz, (x, y, z, d) =>
            {
                float n = VoxelNoise.Value(x, y, z, seed * 31 + salt);
                if (d > 0.7f && n < 0.3f) return E; // ragged silhouette
                float shade = (y + 0.5f - bottom) / span * 0.75f + n * 0.45f;
                return shade > 0.8f ? VoxelPalette.LeafLight : shade > 0.42f ? VoxelPalette.LeafMid : VoxelPalette.LeafDark;
            });
        }
        public static VoxelModel Conifer(int seed)
        {
            var m = new VoxelModel(12, 30, 12, NatureVoxel);
            for (int y = 0; y < 8; y++) for (int z = 5; z <= 6; z++) for (int x = 5; x <= 6; x++)
                m.Set(x, y, z, VoxelNoise.Value(x, y, z, seed) < 0.3f ? VoxelPalette.BarkDark : VoxelPalette.Bark);
            for (int tier = 0; tier < 5; tier++)
            {
                float radius = 5.7f - tier * 0.95f; int y0 = 4 + tier * 5;
                for (int y = y0; y < y0 + 6; y++)
                {
                    float r = radius * (1 - (y - y0) / 7f * 0.8f);
                    for (int z = 0; z < 12; z++) for (int x = 0; x < 12; x++)
                    {
                        float dx = Math.Abs(x + 0.5f - 6), dz = Math.Abs(z + 0.5f - 6);
                        float dist = Math.Max(dx, dz) * 0.5f + (float)Math.Sqrt(dx * dx + dz * dz) * 0.5f;
                        if (dist > r) continue;
                        float n = VoxelNoise.Value(x, y, z, seed + 77);
                        if (dist > r - 1 && n < 0.22f) continue;
                        m.Set(x, y, z, y == y0 || (dist > r - 1 && n < 0.55f) ? VoxelPalette.PineDark : n > 0.9f ? VoxelPalette.LeafMid : VoxelPalette.PineMid);
                    }
                }
            }
            m.Box(5, 29, 5, 2, 1, 2, VoxelPalette.PineMid);
            return m;
        }
        // A dome of leaves; the flat bottom sits on soil or a planter.
        public static VoxelModel Shrub(int seed, int width, int height, int depth, bool flowers)
        {
            var m = new VoxelModel(width, height, depth, NatureVoxel);
            PaintShrub(m, width / 2f, 0, depth / 2f, width / 2f, height, depth / 2f, seed, flowers);
            return m;
        }
        private static void PaintShrub(VoxelModel m, float cx, float baseY, float cz, float rx, float ry, float rz, int seed, bool flowers)
        {
            m.Ellipsoid(cx, baseY, cz, rx, ry, rz, (x, y, z, d) =>
            {
                float n = VoxelNoise.Value(x, y, z, seed);
                if (d > 0.72f && n < 0.25f) return E;
                if (flowers && d > 0.55f && y + 0.5f > baseY + ry * 0.45f && n > 0.93f)
                    return n > 0.975f ? VoxelPalette.FlowerWhite : n > 0.955f ? VoxelPalette.FlowerYellow : VoxelPalette.FlowerPink;
                float shade = (y + 0.5f - baseY) / ry * 0.7f + n * 0.4f;
                return shade > 0.78f ? VoxelPalette.LeafLight : shade > 0.4f ? VoxelPalette.LeafMid : VoxelPalette.LeafDark;
            });
        }
        // Plaza planter: concrete trough with a clipped shrub, 2.25 × 1.25 m footprint.
        public static VoxelModel Planter(int seed)
        {
            var m = new VoxelModel(18, 13, 10, 0.125f);
            m.Box(0, 0, 0, 18, 5, 10, VoxelPalette.Concrete);
            m.Box(0, 0, 0, 18, 1, 10, VoxelPalette.StoneDark);
            m.Box(0, 4, 0, 18, 1, 10, VoxelPalette.Curb);
            m.Box(1, 4, 1, 16, 1, 8, VoxelPalette.Soil);
            PaintShrub(m, 9, 5, 5, 8, 7.5f, 4.2f, seed, true);
            return m;
        }
        public static VoxelModel PottedPlant(int seed)
        {
            var m = new VoxelModel(12, 22, 12, 0.075f);
            m.Box(3, 0, 3, 6, 6, 6, VoxelPalette.BrickRed);
            m.Box(2, 5, 2, 8, 1, 8, VoxelPalette.BrickDark);
            m.Box(3, 5, 3, 6, 1, 6, VoxelPalette.Soil);
            m.Box(5, 6, 5, 2, 5, 2, VoxelPalette.Bark);
            m.Ellipsoid(6, 14, 6, 5.2f, 6.8f, 5.2f, (x, y, z, d) =>
            {
                float n = VoxelNoise.Value(x, y, z, seed);
                if (d > 0.6f && n < 0.35f) return E;
                return n > 0.7f ? VoxelPalette.LeafLight : n > 0.3f ? VoxelPalette.LeafMid : VoxelPalette.LeafDark;
            });
            return m;
        }

        // ---------- Street ----------
        // 5.25 m pole; the arm reaches +Z. Pivot at the pole.
        public static VoxelModel StreetLamp()
        {
            var m = new VoxelModel(4, 42, 14, 0.125f) { PivotX = 2, PivotZ = 2 };
            m.Box(0, 0, 0, 4, 2, 4, VoxelPalette.MetalDark);
            m.Box(1, 2, 1, 2, 38, 2, VoxelPalette.Metal);
            m.Box(1, 9, 1, 2, 1, 2, VoxelPalette.MetalDark);
            m.Box(1, 38, 3, 2, 2, 7, VoxelPalette.Metal);
            m.Box(0, 36, 9, 4, 3, 5, VoxelPalette.MetalDark);
            m.Box(1, 36, 10, 2, 1, 3, VoxelPalette.LampWarm);
            return m;
        }
        // Plaza information board, 2.2 m wide: navy panel (front −Z) in a wooden frame on two posts, brass header, small roof.
        // The lower band hints at the content (map blocks or a share chart); the middle stays plain for the world label.
        public static VoxelModel InfoStand(StandStyle style)
        {
            const int w = 22, h = 25, d = 6;
            var m = new VoxelModel(w, h, d, 0.1f) { PivotX = w / 2f, PivotZ = 3 };
            foreach (int x in new[] { 2, 18 }) { m.Box(x, 0, 3, 2, 23, 2, VoxelPalette.MetalDark); m.Box(x - 1, 0, 2, 4, 1, 4, VoxelPalette.StoneDark); }
            m.Box(1, 8, 3, 20, 14, 1, VoxelPalette.Navy);
            for (int x = 0; x < w; x++) for (int y = 7; y <= 22; y++)
                if (x == 0 || x == w - 1 || y == 7 || y == 22) m.Box(x, y, 2, 1, 1, 2, VoxelPalette.WoodDark);
            m.Box(1, 20, 3, 20, 2, 1, VoxelPalette.Brass);
            m.Box(0, 23, 1, w, 1, 4, VoxelPalette.RoofDark); m.Box(1, 24, 2, w - 2, 1, 2, VoxelPalette.RoofDark);
            if (style == StandStyle.Map)
            {
                for (int x = 2; x < 20; x++) for (int y = 9; y <= 13; y++)
                {
                    bool road = x % 6 == 1 || y == 11;
                    byte c = road ? VoxelPalette.StoneLight : (x / 6 + y) % 3 == 0 ? VoxelPalette.GrassMid : VoxelPalette.Stone;
                    if (x >= 14 && y <= 10) c = VoxelPalette.GlassSky;
                    m.Set(x, y, 2, c);
                }
                m.Set(9, 12, 1, VoxelPalette.SignalRed);
            }
            else
            {
                int[] bars = { 2, 3, 2, 4, 3, 5, 4, 5 };
                for (int i = 0; i < bars.Length; i++)
                    m.Box(3 + i * 2, 9, 2, 1, bars[i], 1, i == 2 || i == 4 ? VoxelPalette.CarRed : VoxelPalette.GrassLight);
                m.Box(2, 9, 2, 18, 1, 1, VoxelPalette.StoneLight);
            }
            return m;
        }
        public static VoxelModel Bench(int length, bool back)
        {
            if (length < 8) throw new ArgumentOutOfRangeException(nameof(length));
            var m = new VoxelModel(length, back ? 9 : 5, back ? 7 : 6, 0.1f);
            foreach (int x in new[] { 1, length - 3 })
            {
                m.Box(x, 0, 0, 2, 4, 1, VoxelPalette.MetalDark); m.Box(x, 0, 4, 2, 4, 1, VoxelPalette.MetalDark);
                m.Box(x, 1, 1, 2, 1, 3, VoxelPalette.MetalDark);
                if (back) m.Box(x, 4, 5, 2, 5, 1, VoxelPalette.MetalDark);
            }
            for (int z = 0; z < 5; z++) m.Box(0, 4, z, length, 1, 1, z % 2 == 0 ? VoxelPalette.Wood : VoxelPalette.WoodLight);
            if (back) for (int y = 5; y < 9; y++) if (y != 6) m.Box(0, y, 6, length, 1, 1, y % 2 == 0 ? VoxelPalette.WoodLight : VoxelPalette.Wood);
            return m;
        }
        public static VoxelModel Dumpster()
        {
            var m = new VoxelModel(12, 12, 16, 0.1f);
            foreach (int x in new[] { 1, 9 }) foreach (int z in new[] { 1, 13 }) m.Box(x, 0, z, 2, 2, 2, VoxelPalette.Tire);
            m.Box(0, 2, 1, 12, 8, 14, VoxelPalette.CarGreen);
            for (int z = 2; z < 15; z += 4) { m.Box(0, 2, z, 1, 8, 1, VoxelPalette.GrassDark); m.Box(11, 2, z, 1, 8, 1, VoxelPalette.GrassDark); }
            m.Box(0, 10, 0, 12, 1, 16, VoxelPalette.MetalDark);
            m.Box(1, 11, 1, 10, 1, 14, VoxelPalette.MetalDark);
            m.Box(3, 6, 0, 6, 1, 1, VoxelPalette.Metal);
            return m;
        }
        // Signal head faces −Z; only the green lamp is lit.
        public static VoxelModel TrafficLight()
        {
            var m = new VoxelModel(6, 42, 5, 0.1f) { PivotX = 3, PivotZ = 3 };
            m.Box(1, 0, 1, 4, 1, 4, VoxelPalette.MetalDark);
            m.Box(2, 1, 2, 2, 30, 2, VoxelPalette.MetalDark);
            m.Box(2, 12, 1, 2, 3, 1, VoxelPalette.Amber);
            m.Box(1, 30, 1, 4, 12, 3, VoxelPalette.Ink);
            byte[] lamps = { VoxelPalette.BrickDark, VoxelPalette.WoodDark, VoxelPalette.SignalGreen };
            for (int i = 0; i < 3; i++)
            {
                int y = 38 - i * 3;
                m.Box(2, y, 1, 2, 2, 1, lamps[i]);
                m.Box(1, y + 2, 0, 4, 1, 1, VoxelPalette.Ink);
            }
            return m;
        }
        // 2.2 m board along X with diagonal red/white stripes and a warning lamp.
        public static VoxelModel ConstructionBarrier()
        {
            var m = new VoxelModel(22, 11, 4, 0.1f);
            m.Box(0, 0, 0, 4, 1, 4, VoxelPalette.MetalDark); m.Box(18, 0, 0, 4, 1, 4, VoxelPalette.MetalDark);
            m.Box(1, 1, 1, 2, 9, 2, VoxelPalette.Metal); m.Box(19, 1, 1, 2, 9, 2, VoxelPalette.Metal);
            foreach (int y0 in new[] { 4, 7 })
                for (int y = y0; y < y0 + 2; y++) for (int x = 0; x < 22; x++)
                    for (int z = 1; z <= 2; z++) m.Set(x, y, z, (x + y) / 2 % 2 == 0 ? VoxelPalette.CarRed : VoxelPalette.CarWhite);
            m.Box(1, 10, 1, 2, 1, 2, VoxelPalette.SignalAmber);
            return m;
        }
        public static VoxelModel Cone()
        {
            var m = new VoxelModel(5, 8, 5, 0.1f);
            m.Box(0, 0, 0, 5, 1, 5, VoxelPalette.Tire);
            m.Box(1, 1, 1, 3, 3, 3, VoxelPalette.Amber);
            m.Box(1, 3, 1, 3, 1, 3, VoxelPalette.CarWhite);
            m.Box(2, 4, 2, 1, 3, 1, VoxelPalette.Amber);
            m.Box(2, 5, 2, 1, 1, 1, VoxelPalette.CarWhite);
            return m;
        }
        public static VoxelModel Bollard()
        {
            var m = new VoxelModel(3, 9, 3, 0.1f);
            m.Box(0, 0, 0, 3, 8, 3, VoxelPalette.MetalDark);
            m.Box(0, 6, 0, 3, 1, 3, VoxelPalette.Amber);
            m.Box(1, 8, 1, 1, 1, 1, VoxelPalette.Metal);
            return m;
        }
        public static VoxelModel Bin()
        {
            var m = new VoxelModel(5, 9, 5, 0.1f);
            m.Box(0, 0, 0, 5, 8, 5, VoxelPalette.MetalDark);
            m.Box(0, 8, 0, 5, 1, 5, VoxelPalette.Metal);
            m.Box(1, 8, 1, 3, 1, 3, VoxelPalette.Ink);
            m.Box(0, 4, 0, 5, 1, 5, VoxelPalette.Metal);
            return m;
        }

        // ---------- Cars: 3.6 × 1.65 m, front +X ----------
        public static VoxelModel Car(CarStyle style, int seed)
        {
            var m = new VoxelModel(24, 11, 11, CarVoxel);
            byte[] paints = { VoxelPalette.CarRed, VoxelPalette.CarBlue, VoxelPalette.CarWhite, VoxelPalette.CarGreen, VoxelPalette.CarBlack, VoxelPalette.Navy };
            byte paint = style == CarStyle.Taxi ? VoxelPalette.CarYellow : paints[RandInt(seed, 3, 0, paints.Length)];
            bool suv = style == CarStyle.Suv;
            int top = suv ? 5 : 4;
            m.Box(1, 1, 1, 22, top, 9, paint);
            m.Box(0, 1, 1, 1, 2, 9, VoxelPalette.MetalDark); m.Box(23, 1, 1, 1, 2, 9, VoxelPalette.MetalDark);
            m.Box(0, 3, 1, 1, top - 2, 9, paint); m.Box(23, 3, 1, 1, top - 2, 9, paint);
            m.Box(23, 2, 4, 1, 2, 3, VoxelPalette.MetalDark);
            m.Box(0, 2, 4, 1, 1, 3, VoxelPalette.Cream); m.Box(23, 1, 4, 1, 1, 3, VoxelPalette.Cream);
            foreach (int z in new[] { 2, 7 }) { m.Box(23, top - 1, z, 1, 1, 2, VoxelPalette.Headlight); m.Box(0, top - 1, z, 1, 1, 2, VoxelPalette.TailLight); }
            // Cabin steps in at the windscreen and rear window.
            int cabin0 = suv ? 3 : 6, cabin1 = suv ? 19 : 17, rows = suv ? 3 : 3;
            for (int r = 0; r < rows + 1; r++)
            {
                int y = top + 1 + r; bool roof = r == rows;
                int x0 = suv ? cabin0 + (r == rows ? 1 : 0) : cabin0 + r, x1 = suv ? cabin1 - (r >= rows - 1 ? 1 : 0) : cabin1 - r;
                if (y >= m.SizeY) break;
                for (int x = x0; x <= x1; x++) for (int z = 2; z <= 8; z++)
                {
                    bool edgeX = x == x0 || x == x1, side = z == 2 || z == 8;
                    bool pillar = x == x0 || x == x1 || x == (x0 + x1) / 2;
                    byte c = roof ? paint : edgeX ? VoxelPalette.GlassDark : side && !pillar ? VoxelPalette.Glass : paint;
                    if (!roof && edgeX && side) c = paint;
                    m.Set(x, y, z, c);
                }
            }
            if (suv) { m.Box(4, 9, 2, 14, 1, 1, VoxelPalette.MetalDark); m.Box(4, 9, 8, 14, 1, 1, VoxelPalette.MetalDark); }
            if (style == CarStyle.Taxi)
            {
                m.Box(10, top + 5, 4, 3, 1, 3, VoxelPalette.SignalAmber);
                for (int x = 1; x < 23; x++) { byte c = x % 2 == 0 ? VoxelPalette.Ink : VoxelPalette.Cream; m.Set(x, top - 1, 1, c); m.Set(x, top - 1, 9, c); }
            }
            int mirrorX = suv ? cabin1 - 1 : cabin1 - 2;
            m.Set(mirrorX, top + 1, 1, paint); m.Set(mirrorX, top + 1, 9, paint);
            m.Set(mirrorX, top + 1, 0, VoxelPalette.MetalDark); m.Set(mirrorX, top + 1, 10, VoxelPalette.MetalDark);
            foreach (int cx in new[] { 3, 17 })
                foreach (int z in new[] { 0, 9 })
                    for (int y = 0; y < 4; y++) for (int x = cx; x < cx + 4; x++)
                    {
                        bool corner = (y == 0 || y == 3) && (x == cx || x == cx + 3);
                        if (corner) continue;
                        m.Set(x, y, z, VoxelPalette.Tire); m.Set(x, y, z + 1, VoxelPalette.Tire);
                        bool hub = y >= 1 && y <= 2 && x >= cx + 1 && x <= cx + 2;
                        if (hub) m.Set(x, y, z == 0 ? 0 : 10, VoxelPalette.Chrome);
                    }
            return m;
        }

        // ---------- People: 1.875 m, face −Z ----------
        public static VoxelFigure Person(PersonStyle style)
        {
            byte suit = style == PersonStyle.Max ? VoxelPalette.Amber : VoxelPalette.Navy;
            byte tie = style == PersonStyle.Lena ? VoxelPalette.Brass : style == PersonStyle.Max ? VoxelPalette.Navy : VoxelPalette.BrickRed;
            byte hair = style == PersonStyle.Max ? VoxelPalette.HairBrown : VoxelPalette.HairDark;
            var body = new VoxelModel(8, 22, 5, PersonVoxel);
            if (style == PersonStyle.Lena)
            {
                foreach (int x in new[] { 1, 5 }) { body.Box(x, 1, 1, 2, 6, 2, VoxelPalette.SkinShade); body.Box(x, 0, 0, 2, 1, 3, VoxelPalette.Ink); }
                body.Box(0, 7, 0, 8, 4, 5, VoxelPalette.Navy);
            }
            else
            {
                foreach (int x in new[] { 0, 5 }) { body.Box(x, 1, 1, 3, 10, 3, VoxelPalette.Navy); body.Box(x, 0, 0, 3, 1, 4, VoxelPalette.Ink); }
                body.Box(0, 10, 0, 8, 1, 5, VoxelPalette.Ink);
            }
            body.Box(0, 11, 0, 8, 11, 5, suit);
            body.Box(2, 17, 0, 4, 5, 1, VoxelPalette.Cream);
            body.Box(3, 13, 0, 2, 7, 1, tie);
            body.Set(2, 21, 0, suit); body.Set(5, 21, 0, suit);
            if (style == PersonStyle.Lena) body.Box(3, 13, 0, 2, 4, 1, suit);
            var head = style == PersonStyle.Lena ? new VoxelModel(7, 10, 7, PersonVoxel) { PivotY = 2 } : new VoxelModel(7, 9, 7, PersonVoxel);
            int b = style == PersonStyle.Lena ? 2 : 0;
            head.Box(2, b, 2, 3, 1, 3, VoxelPalette.SkinShade);
            head.Box(0, b + 1, 0, 7, 6, 7, VoxelPalette.Skin);
            head.Set(2, b + 4, 0, VoxelPalette.Ink); head.Set(4, b + 4, 0, VoxelPalette.Ink);
            head.Set(3, b + 3, 0, VoxelPalette.SkinShade); head.Box(2, b + 2, 0, 3, 1, 1, VoxelPalette.SkinShade);
            head.Box(0, b + 7, 0, 7, 1, 7, hair);
            head.Box(0, b + 5, 4, 7, 2, 3, hair);
            head.Box(0, b + 5, 1, 1, 2, 6, hair); head.Box(6, b + 5, 1, 1, 2, 6, hair);
            head.Box(0, b + 6, 0, 7, 1, 1, hair); head.Set(3, b + 6, 0, VoxelPalette.Skin);
            if (style == PersonStyle.Orlov) { head.Box(0, b + 4, 2, 1, 2, 4, VoxelPalette.StoneLight); head.Box(6, b + 4, 2, 1, 2, 4, VoxelPalette.StoneLight); }
            if (style == PersonStyle.Max)
                for (int z = 0; z < 7; z++) for (int x = 0; x < 7; x++) if (VoxelNoise.Value(x, 8, z, 5) > 0.55f) head.Set(x, 8, z, hair);
            if (style == PersonStyle.Lena) { head.Box(0, 0, 4, 7, 7, 3, hair); head.Box(0, 1, 3, 1, 6, 1, hair); head.Box(6, 1, 3, 1, 6, 1, hair); }
            VoxelModel Arm()
            {
                var arm = new VoxelModel(2, 10, 3, PersonVoxel) { PivotX = 1, PivotY = 10, PivotZ = 1.5f };
                arm.Box(0, 2, 0, 2, 8, 3, suit); arm.Box(0, 2, 0, 2, 1, 3, VoxelPalette.Cream); arm.Box(0, 0, 0, 2, 2, 3, VoxelPalette.Skin);
                return arm;
            }
            return new VoxelFigure(body, head, Arm(), Arm(), 22 * PersonVoxel, 5 * PersonVoxel, 22 * PersonVoxel);
        }

        // ---------- Office ----------
        // Desk in 5 cm voxels; the modesty panel faces −Z, drawers face the sitter at +Z.
        public static VoxelModel Desk(float width, float height, float depth, byte top, byte frame)
        {
            int w = Round(width / FurnitureVoxel), h = Round(height / FurnitureVoxel), d = Round(depth / FurnitureVoxel);
            if (w < 12 || h < 8 || d < 8) throw new ArgumentOutOfRangeException(nameof(width));
            var m = new VoxelModel(w, h, d, FurnitureVoxel);
            m.Box(0, h - 2, 0, w, 2, d, top);
            for (int x = 10; x < w - 4; x += 14) m.Box(x, h - 1, 0, 1, 1, d, VoxelPalette.WoodDark);
            m.Box(0, h - 2, 0, w, 1, 1, VoxelPalette.WoodDark);
            m.Box(1, 0, 1, 2, h - 2, d - 2, frame); m.Box(w - 3, 0, 1, 2, h - 2, d - 2, frame);
            m.Box(3, h / 3, 2, w - 6, h - 2 - h / 3, 1, frame);
            int px = w - 3 - 9;
            m.Box(px, 1, d - 9, 9, h - 4, 7, frame);
            for (int y = 2; y < h - 4; y += 4) { m.Box(px + 1, y, d - 3, 7, 3, 1, VoxelPalette.WoodLight); m.Box(px + 4, y + 1, d - 2, 1, 1, 1, VoxelPalette.Brass); }
            return m;
        }
        public static VoxelModel OfficeChair(byte fabric)
        {
            var m = new VoxelModel(14, 25, 14, FurnitureVoxel);
            m.Box(1, 1, 6, 12, 1, 2, VoxelPalette.MetalDark); m.Box(6, 1, 1, 2, 1, 12, VoxelPalette.MetalDark);
            foreach (var p in new[] { (1, 6), (11, 6), (6, 1), (6, 11) }) m.Box(p.Item1, 0, p.Item2, 2, 1, 2, VoxelPalette.Tire);
            m.Box(6, 2, 6, 2, 7, 2, VoxelPalette.Metal);
            m.Box(1, 9, 1, 12, 2, 11, fabric);
            m.Box(6, 9, 11, 2, 3, 2, VoxelPalette.MetalDark);
            m.Box(2, 12, 11, 10, 11, 2, fabric); m.Box(3, 23, 11, 8, 1, 2, fabric);
            m.Box(3, 15, 12, 8, 1, 1, VoxelPalette.Ink);
            foreach (int x in new[] { 0, 13 }) { m.Box(x, 13, 3, 1, 1, 7, VoxelPalette.Ink); m.Box(x, 11, 6, 1, 2, 1, VoxelPalette.Ink); }
            return m;
        }
        public static VoxelModel Keyboard()
        {
            var m = new VoxelModel(34, 2, 10, 0.025f);
            m.Box(0, 0, 0, 34, 1, 10, VoxelPalette.StoneDark);
            for (int z = 1; z < 9; z += 2) for (int x = 1; x < 33; x++) if (x % 3 != 0) m.Set(x, 1, z, VoxelPalette.Cream);
            m.Box(10, 1, 1, 14, 1, 1, VoxelPalette.Cream);
            return m;
        }
        public static VoxelModel Mouse()
        {
            var m = new VoxelModel(4, 2, 6, 0.02f);
            m.Box(0, 0, 0, 4, 1, 6, VoxelPalette.Cream); m.Box(1, 1, 1, 2, 1, 4, VoxelPalette.Cream);
            m.Set(1, 1, 1, VoxelPalette.StoneDark);
            return m;
        }
        public static VoxelModel CoffeeMachine()
        {
            var m = new VoxelModel(14, 12, 11, FurnitureVoxel);
            m.Box(0, 0, 0, 14, 12, 11, VoxelPalette.Ink);
            m.Box(0, 11, 0, 14, 1, 11, VoxelPalette.MetalDark);
            m.Box(1, 6, 0, 12, 4, 1, VoxelPalette.Brass);
            m.Box(4, 1, 0, 6, 5, 3, E);
            m.Box(4, 0, 0, 6, 1, 3, VoxelPalette.MetalDark);
            m.Box(6, 5, 1, 2, 1, 2, VoxelPalette.Metal);
            m.Box(10, 8, 0, 2, 1, 1, VoxelPalette.ScreenGreen);
            m.Box(2, 8, 0, 2, 1, 1, VoxelPalette.SignalAmber);
            m.Box(9, 11, 6, 4, 1, 4, VoxelPalette.GlassSky);
            return m;
        }

        // ---------- Buildings ----------
        // City tower in 0.5 m voxels with recessed windows; one voxel of margin carries the cornice and canopy.
        public static VoxelModel Tower(float width, float depth, float height, TowerStyle style, int seed, bool roofUnits = true, bool shopFloor = true)
        {
            const float s = TowerVoxel;
            int w = Round(width / s), d = Round(depth / s), h = Round(height / s);
            if (w < 6 || d < 6 || h < 10 || w + 2 > VoxelModel.MaxSize || d + 2 > VoxelModel.MaxSize || h + 5 > VoxelModel.MaxSize)
                throw new ArgumentOutOfRangeException(nameof(width));
            var m = new VoxelModel(w + 2, h + 5, d + 2, s);
            byte wall, band, trim;
            switch (style)
            {
                case TowerStyle.Brick: wall = VoxelPalette.BrickRed; band = VoxelPalette.BrickDark; trim = VoxelPalette.StoneLight; break;
                case TowerStyle.Glass: wall = VoxelPalette.Concrete; band = VoxelPalette.Stone; trim = VoxelPalette.MetalLight; break;
                default:
                    wall = seed % 2 == 0 ? VoxelPalette.PlasterWarm : VoxelPalette.PlasterSage; band = VoxelPalette.Plaster; trim = VoxelPalette.Trim; break;
            }
            int groundH = shopFloor ? 8 : 2;
            m.Box(1, 0, 1, w, h, d, wall);
            m.Box(1, h - 1, 1, w, 1, d, VoxelPalette.RoofDark);
            // Facades: front (−Z) and both sides get windows; the unseen back keeps bands only.
            for (int face = 0; face < 4; face++)
            {
                int length = face < 2 ? w : d;
                for (int u = 0; u < length; u++)
                    for (int y = 0; y < h - 1; y++)
                    {
                        Facade(face, u, w, d, out int fx, out int fz, out int ix, out int iz);
                        int kind = Cell(style, u, y, length, groundH, h, face == 1);
                        if (kind == 0) continue;
                        if (kind == 1) { m.Set(fx, y, fz, band); continue; }
                        if (kind == 2) { m.Set(fx, y, fz, trim); continue; }
                        if (kind == 4) { m.Set(fx, y, fz, VoxelPalette.StoneDark); continue; }
                        // Window: the outer cell opens and the glass sits one voxel deeper.
                        int group = style == TowerStyle.Glass ? u / 4 : u / 3;
                        int floor = y < groundH ? -1 : (y - groundH) / 6;
                        float n = VoxelNoise.Value(face * 97 + group, floor, 0, seed);
                        byte glass = kind == 5 ? VoxelPalette.GlassDark
                            : n < 0.12f ? VoxelPalette.WindowLit : n < 0.55f ? VoxelPalette.GlassDark : n < 0.88f ? VoxelPalette.Glass : VoxelPalette.GlassSky;
                        m.Set(fx, y, fz, E); m.Set(ix, y, iz, glass);
                    }
            }
            // Cornice ring, parapet and the plinth.
            for (int x = 0; x < w + 2; x++) for (int z = 0; z < d + 2; z++)
            {
                bool outer = x == 0 || z == 0 || x == w + 1 || z == d + 1;
                bool rim = x == 1 || z == 1 || x == w || z == d;
                if (outer) m.Set(x, h - 1, z, trim);
                if (rim && !outer) { m.Set(x, h, z, trim); m.Set(x, h - 2, z, m.Get(x, h - 2, z) == E ? E : band); }
            }
            if (shopFloor)
            {
                int mid = w / 2 + 1;
                m.Box(mid - 3, groundH - 2, 0, 6, 1, 1, trim);
            }
            if (roofUnits)
            {
                int units = 1 + RandInt(seed, 50, 0, 3);
                for (int i = 0; i < units; i++)
                {
                    int uw = 2 + RandInt(seed, 60 + i, 0, 3), ud = 2 + RandInt(seed, 70 + i, 0, 3);
                    int ux = 2 + RandInt(seed, 80 + i, 0, Math.Max(1, w - uw - 2)), uz = 2 + RandInt(seed, 90 + i, 0, Math.Max(1, d - ud - 2));
                    m.Box(ux, h, uz, uw, 1 + RandInt(seed, 100 + i, 0, 2), ud, i == 0 ? VoxelPalette.MetalDark : VoxelPalette.Metal);
                }
                if (style == TowerStyle.Brick) { m.Cylinder(w - 2.5f, d - 2.5f, 1.6f, h, 3, VoxelPalette.Wood); m.Cylinder(w - 2.5f, d - 2.5f, 1.6f, h + 3, 1, VoxelPalette.WoodDark); }
                if (h >= 56) m.Box(w / 2, h, d / 2, 1, 4, 1, VoxelPalette.MetalLight);
            }
            m.PivotX = (w + 2) / 2f; m.PivotZ = (d + 2) / 2f;
            return m;
        }
        // Maps facade coordinate u to grid cells: outer facade cell and the glass cell behind it.
        private static void Facade(int face, int u, int w, int d, out int fx, out int fz, out int ix, out int iz)
        {
            switch (face)
            {
                case 0: fx = 1 + u; fz = 1; ix = fx; iz = 2; break;          // front −Z
                case 1: fx = 1 + u; fz = d; ix = fx; iz = d - 1; break;      // back +Z
                case 2: fx = 1; fz = 1 + u; ix = 2; iz = fz; break;          // left −X
                default: fx = w; fz = 1 + u; ix = w - 1; iz = fz; break;     // right +X
            }
        }
        // 0 wall, 1 band, 2 trim, 3 window, 4 plinth, 5 door.
        private static int Cell(TowerStyle style, int u, int y, int length, int groundH, int h, bool back)
        {
            if (u == 0 || u == length - 1) return y == 0 ? 4 : 0;
            if (y >= h - 3) return y == h - 2 ? 1 : 0;
            if (y < groundH)
            {
                if (y == 0) return 4;
                if (groundH <= 2) return 0;
                int mid = length / 2;
                if (!back && u >= mid - 1 && u <= mid && y < 5) return 5;
                if (back) return y == groundH - 1 ? 1 : 0;
                if (y == groundH - 1) return 1;
                return y >= 1 && y <= 5 && u % 6 != 0 && u % 6 != 5 ? 3 : 0;
            }
            int r = (y - groundH) % 6;
            if (back) return r == 0 ? 1 : 0;
            switch (style)
            {
                case TowerStyle.Glass:
                    if (r == 0) return 1;
                    return r <= 4 && u % 4 != 0 ? 3 : 0;
                case TowerStyle.Brick:
                    if (r == 1) return 2;
                    if (r == 5) return 1;
                    return r >= 2 && r <= 4 && u % 3 != 0 ? 3 : 0;
                default:
                    if (r == 0) return 1;
                    return r >= 2 && r <= 4 && u % 3 != 0 ? 3 : 0;
            }
        }

        // Narrow street building, 6 × 9 × 8 m in 0.25 m voxels; the awning porch extends 2 m in front (−Z).
        public static VoxelModel Shop(ShopStyle style, int seed)
        {
            const int w = 24, h = 36, d = 32, porch = 8;
            var m = new VoxelModel(w, h + 3, d + porch, ShopVoxel) { PivotX = w / 2f, PivotZ = porch + d / 2f };
            byte wall = style == ShopStyle.BrickShop ? VoxelPalette.BrickRed : style == ShopStyle.Cafe ? VoxelPalette.PlasterWarm : VoxelPalette.Concrete;
            byte band = style == ShopStyle.BrickShop ? VoxelPalette.BrickDark : style == ShopStyle.Cafe ? VoxelPalette.Plaster : VoxelPalette.Stone;
            byte trim = style == ShopStyle.Office ? VoxelPalette.MetalLight : VoxelPalette.Trim;
            m.Box(0, 0, porch, w, h, d, wall);
            m.Box(0, h - 1, porch, w, 1, d, VoxelPalette.RoofDark);
            m.Box(0, 0, porch, w, 1, 1, VoxelPalette.StoneDark);
            int f = porch;
            // Ground floor: a two-pane display window (sky band, diagonal glints, warm interior for cafés), door and a sign band.
            bool litShop = style == ShopStyle.Cafe || VoxelNoise.Value(seed, 7, 3, seed) < 0.5f;
            for (int y = 2; y <= 9; y++) for (int x = 2; x <= 13; x++)
            {
                m.Set(x, y, f, E);
                byte pane = x == 8 ? trim : y >= 8 ? VoxelPalette.GlassSky : (x + y + seed) % 7 == 0 ? VoxelPalette.Glass
                    : litShop && y <= 5 ? VoxelPalette.WindowLit : VoxelPalette.GlassDark;
                m.Set(x, y, f + 1, pane);
            }
            m.Box(2, 1, f, 12, 1, 1, trim);
            for (int y = 0; y <= 8; y++) for (int x = 16; x <= 19; x++)
            {
                m.Set(x, y, f, E);
                m.Set(x, y, f + 1, y >= 4 && x >= 17 && x <= 18 ? VoxelPalette.GlassDark : VoxelPalette.WoodDark);
            }
            m.Set(17, 4, f + 1, VoxelPalette.Brass);
            byte sign = style == ShopStyle.Office ? VoxelPalette.Navy : style == ShopStyle.Cafe ? VoxelPalette.WoodDark : VoxelPalette.Ink;
            m.Box(1, style == ShopStyle.Office ? 12 : 10, f, 22, style == ShopStyle.Office ? 1 : 2, 1, sign);
            // Upper floors with sills and lintels.
            for (int floor = 0; floor < 2; floor++)
            {
                int y0 = 12 + floor * 12;
                m.Box(0, y0, f, w, 1, 1, band);
                foreach (int x0 in new[] { 2, 10, 18 })
                {
                    for (int y = y0 + 3; y <= y0 + 8; y++) for (int x = x0; x < x0 + 4; x++)
                    {
                        m.Set(x, y, f, E);
                        float n = VoxelNoise.Value(x0, floor, 2, seed);
                        m.Set(x, y, f + 1, n < 0.18f ? VoxelPalette.WindowLit : y == y0 + 8 ? VoxelPalette.GlassSky : VoxelPalette.GlassDark);
                    }
                    m.Box(x0 - 1, y0 + 2, f - 1, 6, 1, 1, trim);
                    if (style != ShopStyle.Office) m.Box(x0 - 1, y0 + 9, f, 6, 1, 1, band);
                }
            }
            // Cornice and parapet.
            m.Box(0, h - 2, f - 1, w, 1, 1, trim);
            m.Box(0, h - 1, f - 1, w, 1, 1, band);
            for (int x = 0; x < w; x++) for (int z = f; z < f + d; z++)
                if (x == 0 || x == w - 1 || z == f || z == f + d - 1) m.Set(x, h, z, trim);
            m.Box(3, h, f + d - 8, 4, 2, 4, VoxelPalette.MetalDark);
            if (style == ShopStyle.Office)
            {
                m.Box(15, 9, f - 3, 6, 1, 3, VoxelPalette.MetalLight);
            }
            else
            {
                byte a = style == ShopStyle.Cafe ? VoxelPalette.CarRed : VoxelPalette.Awning, b = VoxelPalette.AwningStripe;
                for (int step = 0; step < 3; step++)
                {
                    int y = 11 - step;
                    for (int x = 1; x < 15; x++) for (int dz = 0; dz < 2; dz++) m.Set(x, y, f - 1 - step * 2 - dz, x / 2 % 2 == 0 ? a : b);
                }
                for (int x = 1; x < 15; x += 2) m.Set(x, 8, f - 6, x / 2 % 2 == 0 ? a : b);
            }
            return m;
        }
    }
}
