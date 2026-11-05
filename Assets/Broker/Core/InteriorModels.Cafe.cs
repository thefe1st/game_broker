using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Broker.Core
{
    // The café «Бык и медведь»: the counter with its pastry case and till, the wall shelf of coffee and syrups, round tables
    // with a cup, chairs, the chalk menu and the brass pendant lamps. Pivots and bounds as the voxel models they replace
    // (VoxelProps.CafeCounter, CafeShelf, CafeTable, CafeChair, MenuBoard, PendantLamp).
    public static partial class InteriorModels
    {
        public const float CounterTop = 1.05f, CounterHeight = 1.25f, ShelfHeight = 2.2f, CafeSeat = 0.4f, LampDrop = 0.7f;
        // The espresso machine stands on the counter over this rectangle (local x0..x1, z0..z1), which stays clear.
        public const float CounterMachineX0 = 0.35f, CounterMachineX1 = 1.05f, CounterMachineZ0 = -0.225f, CounterMachineZ1 = 0.325f;
        // Tops of the wall shelf's plinth and boards, which the goods stand on: two below the counter's back, two in view
        // over it.
        public static ReadOnlyCollection<float> ShelfLevels { get; } = Array.AsReadOnly(new[] { 0.08f, 0.7f, 1.32f, 1.78f });

        // Café counter 3.2 × 0.7 m, its stone top CounterTop high: a dark plinth, a wooden body whose front (−Z, the
        // customers' side) and ends carry dark raised fields, a pastry case with a glass top on the left and a till facing the
        // customers on the right; the espresso machine stands between them.
        public static PolyMesh CafeCounter()
        {
            var b = NewBuilder();
            const float hw = 1.6f, body = 1.594f, front = -0.33f;
            b.Box(new V3(-body, 0, -0.29f), new V3(body, 0.1f, 0.35f), 0, TrimPart, VoxelPalette.Ink);
            b.Box(new V3(-body, 0.1f, front), new V3(body, 0.96f, 0.35f), 0.006f, TrimPart, VoxelPalette.Wood);
            const int fields = 8; const float stile = 0.05f;
            float w = (2 * body - stile * (fields + 1)) / fields;
            for (int k = 0; k < fields; k++)
            {
                float x0 = -body + stile + k * (w + stile);
                b.Box(new V3(x0, 0.17f, front - 0.006f), new V3(x0 + w, 0.9f, front), 0.005f, TrimPart, VoxelPalette.WoodDark);
            }
            foreach (float s in new[] { -1f, 1f })
                b.Box(new V3(s < 0 ? -hw : body, 0.17f, front + 0.06f), new V3(s < 0 ? -body : hw, 0.9f, 0.29f), 0.003f, TrimPart, VoxelPalette.WoodDark);
            b.Box(new V3(-hw, 0.96f, -0.35f), new V3(hw, CounterTop, 0.35f), 0.012f, TrimPart, VoxelPalette.StoneLight);
            // Pastry case: a chrome tray, four posts and a glass top sloping to the customers; the pastries under it.
            const float cx0 = -1.42f, cx1 = -0.68f, cz0 = -0.25f, cz1 = 0.17f, tray = CounterTop + 0.025f, frontTop = 1.195f, backTop = 1.245f;
            b.Box(new V3(cx0, CounterTop, cz0), new V3(cx1, tray, cz1), 0.004f, MetalPart, VoxelPalette.Chrome);
            foreach (var (x, z, y) in new[] { (cx0 + 0.012f, cz0 + 0.012f, frontTop), (cx1 - 0.012f, cz0 + 0.012f, frontTop), (cx0 + 0.012f, cz1 - 0.012f, backTop),
                (cx1 - 0.012f, cz1 - 0.012f, backTop) })
                PolyShapes.Limb(b, new V3(x, tray, z), new V3(x, y, z), 0.007f, 0.007f, 6, MetalPart, VoxelPalette.Chrome, caps: false);
            PolyShapes.Block(b,
                new[] { new V3(cx0, frontTop, cz0), new V3(cx1, frontTop, cz0), new V3(cx1, backTop, cz1), new V3(cx0, backTop, cz1) },
                new[] { new V3(cx0, frontTop + 0.005f, cz0), new V3(cx1, frontTop + 0.005f, cz0), new V3(cx1, backTop + 0.005f, cz1), new V3(cx0, backTop + 0.005f, cz1) },
                GlassPart, 0);
            Croissant(b, -1.3f, tray, -0.14f, 20); Croissant(b, -1.07f, tray, -0.13f, -15);
            PolyShapes.Blob(b, new V3(-0.82f, tray + 0.022f, -0.14f), 0.05f, 0.03f, 0.05f, 7, 4, TrimPart, VoxelPalette.Amber);
            foreach (var (x, top) in new[] { (-1.25f, VoxelPalette.FlowerPink), (-1.0f, VoxelPalette.WoodDark) })
            {
                PolyShapes.Lathe(b, x, 0.06f, new[] { (tray, 0.055f), (tray + 0.05f, 0.055f) }, 8, TrimPart, VoxelPalette.Cream);
                PolyShapes.Lathe(b, x, 0.06f, new[] { (tray + 0.05f, 0.057f), (tray + 0.065f, 0.054f) }, 8, TrimPart, top, capTop: true);
            }
            PolyShapes.Blob(b, new V3(-0.78f, tray + 0.024f, 0.06f), 0.045f, 0.032f, 0.045f, 7, 4, TrimPart, VoxelPalette.WoodLight);
            // Till facing the customers.
            b.Box(new V3(1.2f, CounterTop, -0.13f), new V3(1.48f, CounterTop + 0.05f, 0.14f), 0.01f, TrimPart, VoxelPalette.Ink);
            b.Box(new V3(1.32f, CounterTop + 0.05f, 0), new V3(1.36f, CounterTop + 0.1f, 0.04f), 0, TrimPart, VoxelPalette.MetalDark);
            b.Place = Pose(new V3(1.34f, CounterTop + 0.125f, 0.02f), 0, 20);
            b.Box(new V3(-0.12f, -0.07f, -0.012f), new V3(0.12f, 0.07f, 0.012f), 0.008f, TrimPart, VoxelPalette.Ink);
            FrontQuad(b, -0.105f, -0.055f, 0.105f, 0.055f, -0.0125f, GlowPart, VoxelPalette.ScreenGreen);
            b.Place = null;
            return b.Build(BuildingModels.PaletteUv);
        }
        // A croissant lying at (x, y, z) turned by yaw: two tapered halves bent into a crescent.
        private static void Croissant(PolyBuilder b, float x, float y, float z, float yaw)
        {
            b.Place = Pose(new V3(x, y + 0.017f, z), yaw);
            foreach (int s in new[] { -1, 1 })
            {
                var path = new List<V3>();
                for (int i = 0; i <= 3; i++)
                {
                    double a = s * (1.25 - 1.25 * i / 3.0);
                    path.Add(new V3(0.05f * (float)Math.Sin(a), 0, 0.03f - 0.05f * (float)Math.Cos(a) + 0.02f));
                }
                PolyShapes.Sweep(b, path, 0.006f, 0.018f, 5, new V3(0, 1, 0), TrimPart, VoxelPalette.WoodLight);
            }
            b.Place = null;
        }

        // Wall shelf behind the counter, 3 × 2.2 m: dark sides, a wooden back against the wall (+Z), a plinth, three boards
        // and a top, and on each level a close row of coffee bags, tins, jars and syrup bottles chosen by the seed.
        public static PolyMesh CafeShelf(int seed)
        {
            var b = NewBuilder();
            const float hw = 1.5f, side = 0.08f, z0 = -0.2f, z1 = 0.2f, back = 0.16f, inner = hw - side;
            foreach (float s in new[] { -1f, 1f })
                b.Box(new V3(s < 0 ? -hw : inner, 0, z0), new V3(s < 0 ? -inner : hw, ShelfHeight, z1), 0.008f, TrimPart, VoxelPalette.WoodDark);
            b.Box(new V3(-inner, 0, back), new V3(inner, ShelfHeight - 0.04f, z1), 0, TrimPart, VoxelPalette.Wood, 0.9f);
            b.Box(new V3(-inner, 0, z0 + 0.02f), new V3(inner, ShelfLevels[0], back), 0, TrimPart, VoxelPalette.WoodDark, 0.9f);
            for (int k = 1; k < ShelfLevels.Count; k++)
                b.Box(new V3(-inner, ShelfLevels[k] - 0.04f, z0), new V3(inner, ShelfLevels[k], back), 0.006f, TrimPart, VoxelPalette.WoodDark);
            b.Box(new V3(-hw, ShelfHeight - 0.04f, z0), new V3(hw, ShelfHeight, z1), 0.008f, TrimPart, VoxelPalette.WoodDark);
            for (int level = 0; level < ShelfLevels.Count; level++)
            {
                float y = ShelfLevels[level], x = -inner + 0.06f + 0.06f * Rand(seed, level);
                for (int i = 0; ; i++)
                {
                    int salt = 100 + level * 40 + i * 4, kind = Math.Min(3, (int)(Rand(seed, salt) * 4));
                    float width = kind == 0 ? 0.13f : kind == 1 ? 0.09f : kind == 2 ? 0.1f : 0.07f;
                    if (x + width > inner - 0.05f) break;
                    Good(b, kind, x + width / 2, -0.03f, y, seed, salt);
                    x += width + 0.025f + 0.07f * Rand(seed, salt + 1);
                }
            }
            return b.Build(BuildingModels.PaletteUv);
        }
        private static readonly byte[] BagColors = { VoxelPalette.Cream, VoxelPalette.Amber, VoxelPalette.CarRed, VoxelPalette.WoodLight, VoxelPalette.Navy };
        private static readonly byte[] TinColors = { VoxelPalette.CarRed, VoxelPalette.Navy, VoxelPalette.CarGreen, VoxelPalette.Brass, VoxelPalette.Cream };
        private static readonly byte[] BottleColors = { VoxelPalette.CarRed, VoxelPalette.Amber, VoxelPalette.PineDark, VoxelPalette.BrickDark };
        // One item of the shelf standing on y: 0 a coffee bag with a folded top, 1 a tin with a lid, 2 a jar of beans with a
        // brass lid, 3 a syrup bottle.
        private static void Good(PolyBuilder b, int kind, float x, float z, float y, int seed, int salt)
        {
            float r = Rand(seed, salt + 2);
            int pick = (int)(Rand(seed, salt + 3) * 100);
            switch (kind)
            {
                case 0:
                {
                    float h = 0.19f + 0.07f * r;
                    byte c = BagColors[pick % BagColors.Length];
                    b.Box(new V3(x - 0.065f, y, z - 0.04f), new V3(x + 0.065f, y + h, z + 0.04f), 0, TrimPart, c);
                    PolyShapes.Block(b,
                        new[] { new V3(x - 0.065f, y + h, z - 0.04f), new V3(x + 0.065f, y + h, z - 0.04f), new V3(x + 0.065f, y + h, z + 0.04f), new V3(x - 0.065f, y + h, z + 0.04f) },
                        new[] { new V3(x - 0.06f, y + h + 0.035f, z - 0.008f), new V3(x + 0.06f, y + h + 0.035f, z - 0.008f), new V3(x + 0.06f, y + h + 0.035f, z + 0.008f),
                            new V3(x - 0.06f, y + h + 0.035f, z + 0.008f) }, TrimPart, c, 0.92f);
                    break;
                }
                case 1:
                {
                    float h = 0.11f + 0.07f * r, rad = 0.042f;
                    PolyShapes.Lathe(b, x, z, new[] { (y, rad), (y + h - 0.016f, rad) }, 6, TrimPart, TinColors[pick % TinColors.Length]);
                    PolyShapes.Lathe(b, x, z, new[] { (y + h - 0.016f, rad + 0.003f), (y + h, rad + 0.003f) }, 6, TrimPart, VoxelPalette.MetalLight, capTop: true);
                    break;
                }
                case 2:
                {
                    float h = 0.15f + 0.05f * r, rad = 0.048f;
                    PolyShapes.Lathe(b, x, z, new[] { (y, rad), (y + h * 0.8f, rad), (y + h * 0.86f, rad * 0.74f) }, 6, TrimPart, VoxelPalette.GlassSky);
                    PolyShapes.Lathe(b, x, z, new[] { (y + h * 0.86f, rad * 0.78f), (y + h, rad * 0.78f) }, 6, TrimPart, VoxelPalette.Brass, capTop: true);
                    break;
                }
                default:
                {
                    float h = 0.24f + 0.08f * r, rad = 0.032f;
                    PolyShapes.Lathe(b, x, z, new[] { (y, rad), (y + h * 0.58f, rad), (y + h * 0.72f, rad * 0.36f), (y + h, rad * 0.32f) }, 6, TrimPart,
                        BottleColors[pick % BottleColors.Length], capTop: true);
                    break;
                }
            }
        }

        // Round café table 0.8 m across, its wooden top PeopleModels.CafeTableTop high on a dark column and domed foot, with a
        // cup of coffee on its saucer.
        public static PolyMesh CafeTable()
        {
            var b = NewBuilder();
            const float top = PeopleModels.CafeTableTop;
            PolyShapes.Lathe(b, 0, 0, new[] { (0f, 0.22f), (0.015f, 0.22f), (0.03f, 0.16f), (0.05f, 0.05f), (0.06f, 0.034f) }, 14, TrimPart, VoxelPalette.MetalDark);
            PolyShapes.Lathe(b, 0, 0, new[] { (0.06f, 0.032f), (top - 0.05f, 0.032f), (top - 0.035f, 0.1f) }, 8, TrimPart, VoxelPalette.MetalDark);
            PolyShapes.Lathe(b, 0, 0, new[] { (top - 0.035f, 0.385f), (top - 0.028f, 0.4f), (top - 0.006f, 0.4f), (top, 0.39f) }, 18, TrimPart, VoxelPalette.Wood, capBottom: true);
            PolyShapes.Disc(b, 0, top, 0, 0.39f, 18, true, TrimPart, VoxelPalette.WoodLight);
            Cup(b, -0.1f, -0.1f, top, 0.034f, 0.05f, 8, true, VoxelPalette.CarWhite);
            return b.Build(BuildingModels.PaletteUv);
        }

        // Café chair: four dark metal legs splayed a little, a round wooden seat CafeSeat high and two posts carrying a wooden
        // backrest towards +Z; the sitter faces −Z.
        public static PolyMesh CafeChair()
        {
            var b = NewBuilder();
            byte metal = VoxelPalette.MetalDark;
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    // Splayed, so the foot's rim starts a hair up and never dips under the floor.
                    PolyShapes.Limb(b, new V3(sx * 0.18f, 0.0018f, sz * 0.18f), new V3(sx * 0.14f, CafeSeat - 0.03f, sz * 0.14f), 0.011f, 0.013f, 6, TrimPart, metal);
            PolyShapes.Lathe(b, 0, 0, new[] { (CafeSeat - 0.03f, 0.19f), (CafeSeat - 0.022f, 0.205f), (CafeSeat - 0.004f, 0.205f), (CafeSeat, 0.196f) }, 14, TrimPart,
                VoxelPalette.Wood, capBottom: true, capTop: true);
            foreach (float sx in new[] { -1f, 1f })
                PolyShapes.Limb(b, new V3(sx * 0.14f, CafeSeat - 0.03f, 0.14f), new V3(sx * 0.155f, 0.87f, 0.19f), 0.011f, 0.01f, 6, TrimPart, metal);
            b.Place = Pose(new V3(0, 0.77f, 0.18f), 0, 5.7f);
            b.Box(new V3(-0.175f, -0.065f, -0.012f), new V3(0.175f, 0.065f, 0.012f), 0.008f, TrimPart, VoxelPalette.Wood);
            b.Place = null;
            return b.Build(BuildingModels.PaletteUv);
        }

        // Chalk menu 1.2 × 0.9 m, its pivot in the middle of its depth: a wooden frame, a dark board, a cup drawn in chalk over
        // four lines of dishes with their prices in amber. Front −Z.
        public static PolyMesh MenuBoard()
        {
            var b = NewBuilder(0);
            Frame(b, -0.6f, 0, 0.6f, 0.9f, -0.54f, 0.06f, 0.54f, 0.84f, -0.05f, 0.05f, 0.012f, TrimPart, VoxelPalette.Wood, back: true);
            const float board = -0.03f, chalk = board - 0.0008f;
            b.Box(new V3(-0.54f, 0.06f, board), new V3(0.54f, 0.84f, 0.05f), 0, TrimPart, VoxelPalette.Ink);
            // The cup instead of «МЕНЮ», 0.15 high with its steam.
            CupGlyph(b, 0.01f, 0.705f, 0.15f, chalk, TrimPart, VoxelPalette.Cream);
            // Dishes in words of chalk, prices in amber.
            for (int line = 0; line < 4; line++)
            {
                float y = 0.54f - line * 0.12f, x = -0.46f;
                int words = 2 + (int)(Rand(5, line) * 2);
                for (int k = 0; k < words; k++)
                {
                    float w = 0.07f + 0.09f * Rand(5, line * 7 + k + 1);
                    FrontQuad(b, x, y, x + w, y + 0.022f, chalk, TrimPart, VoxelPalette.CarWhite);
                    x += w + 0.03f;
                }
                FrontQuad(b, 0.32f, y, 0.44f, y + 0.022f, chalk, TrimPart, VoxelPalette.Amber);
            }
            return b.Build(BuildingModels.PaletteUv);
        }

        // Pendant lamp over the counter: a brass canopy at the ceiling, a dark cord, a brass dome shade, cream inside, round a
        // warm glowing bulb. Pivot at the top (the ceiling); it hangs LampDrop.
        public static PolyMesh PendantLamp()
        {
            var b = NewBuilder(0);
            PolyShapes.Lathe(b, 0, 0, new[] { (-0.03f, 0.012f), (-0.024f, 0.045f), (0f, 0.05f) }, 10, MetalPart, VoxelPalette.Brass, capBottom: true);
            PolyShapes.Limb(b, new V3(0, -0.03f, 0), new V3(0, -0.44f, 0), 0.005f, 0.005f, 5, TrimPart, VoxelPalette.Ink, caps: false);
            PolyShapes.Lathe(b, 0, 0, new[] { (-0.64f, 0.15f), (-0.6f, 0.14f), (-0.53f, 0.092f), (-0.47f, 0.048f), (-0.445f, 0.026f), (-0.432f, 0.01f) }, 12, MetalPart,
                VoxelPalette.Brass, capTop: true);
            PolyShapes.Lathe(b, 0, 0, new[] { (-0.46f, 0.02f), (-0.46f, 0.039f) }, 12, TrimPart, VoxelPalette.Cream, 0.9f);
            PolyShapes.Lathe(b, 0, 0, new[] { (-0.46f, 0.02f), (-0.53f, 0.084f), (-0.6f, 0.133f), (-0.64f, 0.143f) }, 12, TrimPart, VoxelPalette.Cream, 0.95f);
            PolyShapes.Lathe(b, 0, 0, new[] { (-0.64f, 0.143f), (-0.64f, 0.15f) }, 12, MetalPart, VoxelPalette.Brass);
            PolyShapes.Lathe(b, 0, 0, new[] { (-0.56f, 0.018f), (-0.46f, 0.018f) }, 8, TrimPart, VoxelPalette.Ink, capBottom: true);
            PolyShapes.Blob(b, new V3(0, -0.6f, 0), 0.04f, 0.048f, 0.04f, 8, 4, GlowPart, VoxelPalette.LampWarm);
            return b.Build(BuildingModels.PaletteUv);
        }
    }
}
