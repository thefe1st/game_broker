using System;
using System.Collections.Generic;

namespace Broker.Core
{
    // The executive wing west of the office (stage 6, the author: «добавить комнаты в стиле волк с уолл стрит, только не
    // слишком масштабно»): a meeting room and a lounge in dark wood, oxblood leather and brass. The boardroom table, the
    // credenza with the whisky and the brass bull, the trading screen on the wall; the chesterfield sofa and armchair, the
    // coffee table, the aquarium, the floor lamp, the yacht in its gilt frame and the rug; the wood panelling, the brass
    // casings round the doorways and the banker's lamp on the director's desk. Fronts face −Z and pivots sit at the bottom centre unless noted.
    public static partial class InteriorModels
    {
        public const float SofaSeat = 0.45f, SofaDepth = 0.9f, SofaBackTop = 0.86f, SofaArmTop = 0.72f;
        public const float CoffeeTableHeight = 0.42f, CredenzaHeight = 0.8f, CredenzaDepth = 0.46f;
        public const float AquariumStand = 0.75f, AquariumTank = 0.62f, AquariumDepth = 0.5f, AquariumWater = 0.56f;
        public const float WallScreenDepth = 0.06f, PaintingDepth = 0.05f, CasingWidth = 0.09f, CasingProud = 0.025f;
        public const float FloorLampHeight = 1.62f, BankersLampHeight = 0.44f, RugHeight = 0.012f, PanelRail = 0.95f, PanelProud = 0.015f;

        // ---------- Pieces ----------
        // A flat slab between y0 and y1 over a convex outline in the XZ plane, in a smoothing group of its own: rounded
        // outlines read round, the top and bottom stay flat.
        private static void Slab(PolyBuilder b, IList<(float x, float z)> outline, float y0, float y1, int part, byte color, bool bottom = true, float shade = 1)
        {
            int group = b.Group; b.Group = b.NextGroup();
            int n = outline.Count; float cx = 0, cz = 0;
            var top = new V3[n]; var low = new V3[n];
            for (int i = 0; i < n; i++)
            {
                top[i] = new V3(outline[i].x, y1, outline[i].z); low[i] = new V3(outline[i].x, y0, outline[i].z);
                cx += outline[i].x / n; cz += outline[i].z / n;
            }
            b.Polygon(top, new V3(0, 1, 0), part, color, shade);
            if (bottom) b.Polygon(low, new V3(0, -1, 0), part, color, shade * 0.85f);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                b.Quad(low[i], low[j], top[j], top[i], new V3((outline[i].x + outline[j].x) / 2 - cx, 0, (outline[i].z + outline[j].z) / 2 - cz), part, color, shade * 0.92f);
            }
            b.Group = group;
        }
        // A racetrack outline `2·hw` × `2·hd`, its ends half circles of `steps` segments.
        private static List<(float x, float z)> Stadium(float hw, float hd, int steps)
        {
            var points = new List<(float x, float z)>();
            float r = hd, cx = hw - r;
            for (int i = 0; i <= steps; i++) { double a = -Math.PI / 2 + Math.PI * i / steps; points.Add((cx + r * (float)Math.Cos(a), r * (float)Math.Sin(a))); }
            for (int i = 0; i <= steps; i++) { double a = Math.PI / 2 + Math.PI * i / steps; points.Add((-cx + r * (float)Math.Cos(a), r * (float)Math.Sin(a))); }
            return points;
        }
        // A turned wooden foot under furniture, `h` high.
        private static void TurnedFoot(PolyBuilder b, float x, float z, float h, float r, byte color)
            => PolyShapes.Lathe(b, x, z, new[] { (0f, r * 0.7f), (h * 0.45f, r), (h, r * 1.1f) }, 6, TrimPart, color, capBottom: true, capTop: true);

        // ---------- Meeting room ----------
        // Boardroom table `width` × `depth` at DeskHeight: a dark top with rounded ends and a brass line inlaid along it, on
        // two plinths with brass feet.
        public static PolyMesh ConferenceTable(float width, float depth)
        {
            if (!(width >= 1.2f) || width > 6 || !(depth >= 0.6f) || depth > 2 || depth > width) throw new ArgumentOutOfRangeException(nameof(width));
            var b = NewBuilder();
            float hw = width / 2, hd = depth / 2, top = DeskHeight;
            Slab(b, Stadium(hw, hd, 8), top - 0.06f, top, TrimPart, VoxelPalette.WoodDark);
            TopQuad(b, -hw + hd * 0.6f, -0.025f, hw - hd * 0.6f, 0.025f, top + 0.0015f, MetalPart, VoxelPalette.Brass);
            foreach (float s in new[] { -1f, 1f })
            {
                float px = s * hw * 0.5f;
                b.Box(new V3(px - 0.2f, 0.06f, -hd * 0.42f), new V3(px + 0.2f, top - 0.06f, hd * 0.42f), 0.012f, TrimPart, VoxelPalette.Wood);
                b.Box(new V3(px - 0.27f, 0, -hd * 0.52f), new V3(px + 0.27f, 0.06f, hd * 0.52f), 0.01f, MetalPart, VoxelPalette.Brass);
            }
            return b.Build(BuildingModels.PaletteUv);
        }
        // Low sideboard `width` × CredenzaDepth × CredenzaHeight: a light top over a dark body with three raised doors,
        // brass knobs and four turned feet.
        public static PolyMesh Credenza(float width)
        {
            if (!(width >= 0.8f) || width > 4) throw new ArgumentOutOfRangeException(nameof(width));
            var b = NewBuilder();
            float hw = width / 2, hd = CredenzaDepth / 2, top = CredenzaHeight;
            b.Box(new V3(-hw, top - 0.04f, -hd), new V3(hw, top, hd), 0.012f, TrimPart, VoxelPalette.Wood);
            b.Box(new V3(-hw + 0.03f, 0.1f, -hd + 0.02f), new V3(hw - 0.03f, top - 0.04f, hd), 0.008f, TrimPart, VoxelPalette.WoodDark);
            float door = (width - 0.06f - 0.04f) / 3;
            for (int k = 0; k < 3; k++)
            {
                float x0 = -hw + 0.05f + k * (door + 0.0f), x1 = x0 + door - 0.02f;
                b.Box(new V3(x0, 0.14f, -hd), new V3(x1, top - 0.08f, -hd + 0.02f), 0.006f, TrimPart, VoxelPalette.WoodDark, 1.1f);
                float knob = k == 2 ? x0 + 0.06f : x1 - 0.06f;
                PolyShapes.Limb(b, new V3(knob, 0.5f, -hd), new V3(knob, 0.5f, -hd - 0.025f), 0.013f, 0.016f, 6, MetalPart, VoxelPalette.Brass);
            }
            foreach (float x in new[] { -hw + 0.08f, hw - 0.08f })
                foreach (float z in new[] { -hd + 0.07f, hd - 0.07f })
                    TurnedFoot(b, x, z, 0.1f, 0.03f, VoxelPalette.WoodDark);
            return b.Build(BuildingModels.PaletteUv);
        }
        // Whisky on a brass tray: a decanter with its stopper and two tumblers, 0.42 × 0.26 m.
        public static PolyMesh Decanter()
        {
            var b = NewBuilder(0.05f);
            b.Box(new V3(-0.21f, 0, -0.13f), new V3(0.21f, 0.012f, 0.13f), 0.004f, MetalPart, VoxelPalette.Brass);
            const float y = 0.012f;
            PolyShapes.Lathe(b, -0.08f, 0, new[] { (y, 0.055f), (y + 0.02f, 0.065f), (y + 0.15f, 0.065f), (y + 0.19f, 0.03f), (y + 0.23f, 0.02f), (y + 0.24f, 0.024f) },
                12, TrimPart, VoxelPalette.Amber, capBottom: true, capTop: true);
            PolyShapes.Blob(b, new V3(-0.08f, y + 0.27f, 0), 0.03f, 0.03f, 0.03f, 8, 5, MetalPart, VoxelPalette.Chrome);
            foreach (float z in new[] { -0.055f, 0.06f })
            {
                PolyShapes.Lathe(b, 0.1f, z, new[] { (y, 0.034f), (y + 0.08f, 0.038f) }, 10, TrimPart, VoxelPalette.Amber, capBottom: true, capTop: true);
                PolyShapes.Disc(b, 0.1f, y + 0.0801f, z, 0.03f, 10, true, TrimPart, VoxelPalette.WoodLight);
            }
            return b.Build(BuildingModels.PaletteUv);
        }
        // The brass bull of Wall Street on a dark wooden base, 0.45 m long: the Exchange's bull at a sixth of its size.
        // Heads −Z.
        public static PolyMesh BullFigure()
        {
            var b = NewBuilder(0.05f);
            const float k = 0.16f, lift = 0.04f;
            V3 P(float x, float y, float z) => new V3(x * k, lift + (y - 1.0f) * k, z * k);
            b.Box(new V3(-0.11f, 0, -0.225f), new V3(0.11f, lift, 0.225f), 0.008f, TrimPart, VoxelPalette.WoodDark);
            byte metal = VoxelPalette.Brass;
            foreach (float lx in new[] { -0.35f, 0.35f })
                foreach (float lz in new[] { -0.55f, 0.65f })
                    PolyShapes.Limb(b, P(lx, 1.0f, lz), P(lx * 0.92f, 1.72f, lz * 0.95f), 0.12f * k, 0.16f * k, 6, MetalPart, metal);
            PolyShapes.Blob(b, P(0, 1.85f, 0.1f), 0.55f * k, 0.4f * k, 0.9f * k, 10, 6, MetalPart, metal);
            PolyShapes.Blob(b, P(0, 2.05f, -0.4f), 0.5f * k, 0.36f * k, 0.45f * k, 10, 6, MetalPart, metal);
            PolyShapes.Blob(b, P(0, 1.65f, -0.9f), 0.3f * k, 0.3f * k, 0.34f * k, 8, 5, MetalPart, metal);
            PolyShapes.Blob(b, P(0, 1.5f, -1.12f), 0.2f * k, 0.14f * k, 0.14f * k, 6, 4, MetalPart, metal);
            foreach (float side in new[] { -1f, 1f })
            {
                PolyShapes.Limb(b, P(side * 0.22f, 1.85f, -0.95f), P(side * 0.42f, 1.95f, -1.0f), 0.06f * k, 0.05f * k, 5, TrimPart, VoxelPalette.Cream);
                PolyShapes.Limb(b, P(side * 0.42f, 1.95f, -1.0f), P(side * 0.5f, 2.12f, -1.12f), 0.05f * k, 0.015f * k, 5, TrimPart, VoxelPalette.Cream);
            }
            PolyShapes.Limb(b, P(0, 2.0f, 0.95f), P(0, 1.48f, 1.12f), 0.04f * k, 0.03f * k, 5, MetalPart, metal);
            return b.Build(BuildingModels.PaletteUv);
        }
        // Trading screen `width` × `height` on the wall: a dark bezel WallScreenDepth deep, a dark field with grid lines,
        // glowing candles — green up, red down — and a rising line over them. Pivot at the middle of its back (z = 0).
        public static PolyMesh WallScreen(float width, float height)
        {
            if (!(width >= 0.6f) || width > 4 || !(height >= 0.4f) || height > 2.5f) throw new ArgumentOutOfRangeException(nameof(width));
            var b = NewBuilder(0);
            float hw = width / 2, hh = height / 2, d = WallScreenDepth, face = -d - 0.001f, glow = face - 0.002f;
            b.Box(new V3(-hw, -hh, -d), new V3(hw, hh, 0), 0.01f, TrimPart, VoxelPalette.Ink);
            float ix = hw - 0.04f, iy = hh - 0.04f;
            FrontQuad(b, -ix, -iy, ix, iy, face, TrimPart, VoxelPalette.Navy, 0.75f);
            for (int k = 1; k < 4; k++)
            {
                float y = -iy + 2 * iy * k / 4;
                FrontQuad(b, -ix + 0.03f, y - 0.003f, ix - 0.03f, y + 0.003f, face - 0.001f, TrimPart, VoxelPalette.Ink);
            }
            const int candles = 12;
            float step = 2 * (ix - 0.08f) / candles, level = -iy * 0.55f;
            for (int k = 0; k < candles; k++)
            {
                bool down = k == 3 || k == 4 || k == 8;
                float x = -ix + 0.08f + (k + 0.5f) * step, body = 0.05f + 0.04f * Rand(17, k), w = step * 0.32f;
                float y0 = level + (down ? -body : 0), y1 = level + (down ? 0 : body);
                byte c = down ? VoxelPalette.SignalRed : VoxelPalette.SignalGreen;
                FrontQuad(b, x - w, y0, x + w, y1, glow, GlowPart, c);
                FrontQuad(b, x - 0.004f, y0 - 0.03f, x + 0.004f, y1 + 0.03f, glow + 0.0005f, GlowPart, c);
                level += down ? -body * 0.7f : body * 0.75f;
            }
            var line = new List<(float x, float y)>();
            for (int k = 0; k <= 8; k++)
            {
                float t = k / 8f;
                line.Add((-ix + 0.08f + t * 2 * (ix - 0.08f), -iy * 0.35f + t * iy * 1.15f + (k % 3 == 1 ? 0.05f : k % 3 == 2 ? -0.03f : 0)));
            }
            Strokes(b, line, 0.012f, glow - 0.001f, GlowPart, VoxelPalette.ScreenGreen);
            return b.Build(BuildingModels.PaletteUv);
        }

        // ---------- Lounge ----------
        // Chesterfield `width` wide, SofaDepth deep: an oxblood leather body on turned feet, rolled arms at SofaArmTop and a
        // rolled back at SofaBackTop, buttoned in two rows, and one cushion per seat at SofaSeat. One seat is an armchair.
        public static PolyMesh Sofa(float width)
        {
            if (!(width >= 0.9f) || width > 3.2f) throw new ArgumentOutOfRangeException(nameof(width));
            var b = NewBuilder();
            byte leather = VoxelPalette.BrickDark;
            float hw = width / 2, hd = SofaDepth / 2, arm = 0.19f, backFront = hd - 0.24f;
            foreach (float x in new[] { -hw + 0.1f, hw - 0.1f })
                foreach (float z in new[] { -hd + 0.1f, hd - 0.1f })
                    TurnedFoot(b, x, z, 0.08f, 0.03f, VoxelPalette.WoodDark);
            b.Box(new V3(-hw + 0.02f, 0.08f, -hd + 0.03f), new V3(hw - 0.02f, 0.32f, hd - 0.02f), 0.02f, TrimPart, leather, 0.95f);
            int seats = Math.Max(1, (int)Math.Round((width - 2 * arm) / 0.62f));
            float seatW = (width - 2 * arm) / seats;
            for (int k = 0; k < seats; k++)
            {
                float x0 = -hw + arm + k * seatW + 0.005f, x1 = x0 + seatW - 0.01f;
                b.Box(new V3(x0, 0.32f, -hd + 0.01f), new V3(x1, SofaSeat, backFront), 0.035f, TrimPart, leather);
            }
            const float roll = 0.12f;
            b.Box(new V3(-hw + arm, 0.32f, backFront), new V3(hw - arm, SofaBackTop - roll, hd), 0.02f, TrimPart, leather);
            PolyShapes.Limb(b, new V3(-hw + arm, SofaBackTop - roll, hd - roll), new V3(hw - arm, SofaBackTop - roll, hd - roll), roll, roll, 10, TrimPart, leather);
            foreach (float s in new[] { -1f, 1f })
            {
                float x0 = s < 0 ? -hw : hw - arm, x1 = s < 0 ? -hw + arm : hw;
                b.Box(new V3(x0, 0.3f, -hd + 0.02f), new V3(x1, SofaArmTop - 0.08f, hd), 0.02f, TrimPart, leather);
                float cx = s * (hw - arm / 2 + 0.01f);
                PolyShapes.Limb(b, new V3(cx, SofaArmTop - 0.085f, -hd + 0.02f), new V3(cx, SofaArmTop - 0.085f, hd), 0.085f, 0.085f, 10, TrimPart, leather);
            }
            // Buttons in diamonds on the back's face.
            for (int row = 0; row < 2; row++)
            {
                float y = 0.55f + row * 0.13f, off = row * 0.09f;
                for (float x = -hw + arm + 0.08f + off; x < hw - arm - 0.06f; x += 0.18f)
                    FrontDisc(b, x, y, backFront - 0.002f, 0.013f, 6, TrimPart, VoxelPalette.BarkDark);
            }
            return b.Build(BuildingModels.PaletteUv);
        }
        // Low table `width` × `depth` at CoffeeTableHeight: a dark top on four brass legs with a shelf between them, a cigar
        // box with a brass clasp and a brass ashtray on top.
        public static PolyMesh CoffeeTable(float width, float depth)
        {
            if (!(width >= 0.6f) || width > 2.5f || !(depth >= 0.4f) || depth > 1.5f) throw new ArgumentOutOfRangeException(nameof(width));
            var b = NewBuilder();
            float hw = width / 2, hd = depth / 2, top = CoffeeTableHeight;
            b.Box(new V3(-hw, top - 0.045f, -hd), new V3(hw, top, hd), 0.012f, TrimPart, VoxelPalette.WoodDark);
            b.Box(new V3(-hw + 0.08f, 0.1f, -hd + 0.08f), new V3(hw - 0.08f, 0.125f, hd - 0.08f), 0.006f, TrimPart, VoxelPalette.Wood);
            foreach (float x in new[] { -hw + 0.07f, hw - 0.07f })
                foreach (float z in new[] { -hd + 0.07f, hd - 0.07f })
                    PolyShapes.Limb(b, new V3(x, 0, z), new V3(x, top - 0.045f, z), 0.02f, 0.016f, 6, MetalPart, VoxelPalette.Brass);
            b.Box(new V3(-hw * 0.6f, top, -0.09f), new V3(-hw * 0.6f + 0.26f, top + 0.06f, 0.07f), 0.006f, TrimPart, VoxelPalette.Wood);
            b.Box(new V3(-hw * 0.6f + 0.11f, top + 0.03f, -0.093f), new V3(-hw * 0.6f + 0.15f, top + 0.05f, -0.09f), 0, MetalPart, VoxelPalette.Brass);
            PolyShapes.Lathe(b, hw * 0.45f, 0.05f, new[] { (top, 0.05f), (top + 0.02f, 0.075f), (top + 0.03f, 0.075f), (top + 0.03f, 0.055f), (top + 0.012f, 0.045f) },
                12, MetalPart, VoxelPalette.Brass, capBottom: true, capTop: true);
            return b.Build(BuildingModels.PaletteUv);
        }
        // Aquarium `width` wide on its cabinet: a dark stand AquariumStand high with two doors, a tank AquariumTank high in
        // a brass frame under a dark lid; the water — the back and both sides — glows, the front is open to the room. Pale
        // gravel, weed and four fish inside.
        public static PolyMesh Aquarium(float width)
        {
            if (!(width >= 0.8f) || width > 3) throw new ArgumentOutOfRangeException(nameof(width));
            var b = NewBuilder();
            float hw = width / 2, hd = AquariumDepth / 2, y0 = AquariumStand, y1 = AquariumStand + AquariumTank, water = y0 + AquariumWater;
            b.Box(new V3(-hw, 0, -hd), new V3(hw, y0, hd), 0.01f, TrimPart, VoxelPalette.WoodDark);
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box(new V3(s < 0 ? -hw + 0.05f : 0.02f, 0.08f, -hd - 0.015f), new V3(s < 0 ? -0.02f : hw - 0.05f, y0 - 0.08f, -hd), 0.005f, TrimPart, VoxelPalette.WoodDark, 1.1f);
                PolyShapes.Limb(b, new V3(s * 0.07f, y0 / 2, -hd - 0.015f), new V3(s * 0.07f, y0 / 2, -hd - 0.04f), 0.012f, 0.015f, 6, MetalPart, VoxelPalette.Brass);
            }
            // The water: the back wall facing in, each side facing in and out.
            const float t = 0.03f;
            float wx = hw - t, wz = hd - t;
            FrontQuad(b, -wx, y0 + 0.01f, wx, water, wz, GlowPart, VoxelPalette.ScreenGreen);
            foreach (float s in new[] { -1f, 1f })
                foreach (float face in new[] { -1f, 1f })
                {
                    float x = s * (face < 0 ? wx : hw - 0.002f);
                    b.Quad(new V3(x, y0 + 0.01f, -wz), new V3(x, y0 + 0.01f, wz), new V3(x, water, wz), new V3(x, water, -wz), new V3(face < 0 ? -s : s, 0, 0), GlowPart, VoxelPalette.ScreenGreen);
                }
            b.Quad(new V3(-hw + 0.002f, y0 + 0.01f, hd - 0.002f), new V3(hw - 0.002f, y0 + 0.01f, hd - 0.002f), new V3(hw - 0.002f, water, hd - 0.002f),
                new V3(-hw + 0.002f, water, hd - 0.002f), new V3(0, 0, 1), GlowPart, VoxelPalette.ScreenGreen);
            TopQuad(b, -wx, -wz, wx, wz, y0 + 0.04f, TrimPart, VoxelPalette.Cream);
            // Weed and fish.
            for (int k = 0; k < 5; k++)
            {
                float x = -wx + 0.12f + k * (2 * wx - 0.24f) / 4, z = wz - 0.08f - 0.1f * Rand(31, k), h = 0.22f + 0.2f * Rand(31, k + 7);
                PolyShapes.Limb(b, new V3(x, y0 + 0.04f, z), new V3(x + 0.04f * (k % 2 == 0 ? 1 : -1), y0 + 0.04f + h, z), 0.02f, 0.006f, 5, TrimPart, VoxelPalette.LeafMid);
            }
            byte[] fish = { VoxelPalette.Amber, VoxelPalette.CarYellow, VoxelPalette.CarRed, VoxelPalette.Amber };
            for (int k = 0; k < 4; k++)
            {
                float x = -wx * 0.6f + k * wx * 0.4f, y = y0 + 0.18f + 0.3f * Rand(37, k), z = -0.02f + 0.08f * Rand(37, k + 5), dir = k % 2 == 0 ? 1 : -1;
                PolyShapes.Blob(b, new V3(x, y, z), 0.045f, 0.024f, 0.012f, 8, 4, TrimPart, fish[k]);
                b.Triangle(new V3(x - dir * 0.04f, y, z), new V3(x - dir * 0.075f, y + 0.022f, z), new V3(x - dir * 0.075f, y - 0.022f, z), new V3(0, 0, -1), TrimPart, fish[k]);
                b.Triangle(new V3(x - dir * 0.04f, y, z), new V3(x - dir * 0.075f, y + 0.022f, z), new V3(x - dir * 0.075f, y - 0.022f, z), new V3(0, 0, 1), TrimPart, fish[k]);
            }
            // The brass frame: corner posts, rails at the foot and the rim, then the lid.
            foreach (float x in new[] { -hw, hw - t })
                foreach (float z in new[] { -hd, hd - t })
                    b.Box(new V3(x, y0, z), new V3(x + t, y1, z + t), 0, MetalPart, VoxelPalette.Brass);
            foreach (float y in new[] { y0, y1 - 0.035f })
            {
                b.Box(new V3(-hw + t, y, -hd), new V3(hw - t, y + 0.035f, -hd + t), 0, MetalPart, VoxelPalette.Brass);
                b.Box(new V3(-hw + t, y, hd - t), new V3(hw - t, y + 0.035f, hd), 0, MetalPart, VoxelPalette.Brass);
            }
            b.Box(new V3(-hw - 0.01f, y1, -hd - 0.01f), new V3(hw + 0.01f, y1 + 0.05f, hd + 0.01f), 0.01f, TrimPart, VoxelPalette.Ink);
            return b.Build(BuildingModels.PaletteUv);
        }
        // Floor lamp FloorLampHeight tall: a brass foot and pole, a cream drum shade open at both ends round a warm bulb.
        public static PolyMesh FloorLamp()
        {
            var b = NewBuilder();
            PolyShapes.Lathe(b, 0, 0, new[] { (0f, 0.17f), (0.02f, 0.17f), (0.05f, 0.06f) }, 14, MetalPart, VoxelPalette.Brass, capBottom: true, capTop: true);
            PolyShapes.Limb(b, new V3(0, 0.05f, 0), new V3(0, FloorLampHeight - 0.2f, 0), 0.014f, 0.012f, 6, MetalPart, VoxelPalette.Brass);
            float y0 = FloorLampHeight - 0.34f, y1 = FloorLampHeight;
            PolyShapes.Lathe(b, 0, 0, new[] { (y0, 0.24f), (y1, 0.19f) }, 16, TrimPart, VoxelPalette.Cream);
            PolyShapes.Lathe(b, 0, 0, new[] { (y1 - 0.004f, 0.185f), (y0 + 0.004f, 0.235f) }, 16, TrimPart, VoxelPalette.Cream, 0.8f);
            PolyShapes.Blob(b, new V3(0, y0 + 0.14f, 0), 0.06f, 0.075f, 0.06f, 8, 5, GlowPart, VoxelPalette.LampWarm);
            return b.Build(BuildingModels.PaletteUv);
        }
        // Painting `width` × `height` in a gilt frame: a yacht under sail on a teal sea, a pale sky and a low sun. Pivot at
        // the middle of its back on the wall (z = 0); PaintingDepth deep.
        public static PolyMesh Painting(float width, float height)
        {
            if (!(width >= 0.4f) || width > 3 || !(height >= 0.3f) || height > 2) throw new ArgumentOutOfRangeException(nameof(width));
            var b = NewBuilder(0);
            float hw = width / 2, hh = height / 2, f = 0.07f, canvas = -0.02f, paint = canvas - 0.001f;
            Frame(b, -hw, -hh, hw, hh, -hw + f, -hh + f, hw - f, hh - f, -PaintingDepth, 0, 0.012f, MetalPart, VoxelPalette.Brass);
            float ix = hw - f, iy = hh - f, horizon = -iy + (2 * iy) * 0.42f;
            FrontQuad(b, -ix, horizon, ix, iy, canvas, TrimPart, VoxelPalette.GlassSky);
            FrontQuad(b, -ix, -iy, ix, horizon, canvas, TrimPart, VoxelPalette.Awning);
            FrontDisc(b, ix * 0.55f, horizon + iy * 0.5f, paint, iy * 0.16f, 14, TrimPart, VoxelPalette.Amber);
            float s = Math.Min(ix, iy), cx = -ix * 0.15f, hull = horizon - s * 0.06f;
            Fill(b, paint, TrimPart, VoxelPalette.CarWhite, (cx - s * 0.5f, hull + s * 0.1f), (cx + s * 0.55f, hull + s * 0.1f), (cx + s * 0.38f, hull - s * 0.05f), (cx - s * 0.4f, hull - s * 0.05f));
            Fill(b, paint, TrimPart, VoxelPalette.Cream, (cx - s * 0.03f, hull + s * 0.14f), (cx - s * 0.03f, hull + s * 0.95f), (cx - s * 0.42f, hull + s * 0.14f));
            Fill(b, paint, TrimPart, VoxelPalette.Cream, (cx + s * 0.03f, hull + s * 0.14f), (cx + s * 0.03f, hull + s * 0.8f), (cx + s * 0.4f, hull + s * 0.14f));
            Stroke(b, cx, hull + s * 0.1f, cx, hull + s * 1.0f, s * 0.025f, paint - 0.0005f, TrimPart, VoxelPalette.WoodDark);
            for (int k = 0; k < 3; k++)
            {
                float y = -iy + (horizon + iy) * (0.25f + 0.22f * k), x0 = -ix * (0.8f - 0.25f * k);
                Stroke(b, x0, y, x0 + ix * 0.5f, y, 0.008f, paint, TrimPart, VoxelPalette.CarWhite);
            }
            return b.Build(BuildingModels.PaletteUv);
        }
        // Rug `width` × `depth`, RugHeight thick: an oxblood field in a cream border with brass corners.
        public static PolyMesh Rug(float width, float depth)
        {
            if (!(width >= 0.6f) || width > 6 || !(depth >= 0.6f) || depth > 6) throw new ArgumentOutOfRangeException(nameof(width));
            var b = NewBuilder(0);
            float hw = width / 2, hd = depth / 2, y = RugHeight, top = y + 0.0008f;
            b.Box(new V3(-hw, 0, -hd), new V3(hw, y, hd), 0.003f, TrimPart, VoxelPalette.BrickDark);
            const float band = 0.06f, inset = 0.12f;
            TopQuad(b, -hw + inset, -hd + inset, hw - inset, -hd + inset + band, top, TrimPart, VoxelPalette.Cream);
            TopQuad(b, -hw + inset, hd - inset - band, hw - inset, hd - inset, top, TrimPart, VoxelPalette.Cream);
            TopQuad(b, -hw + inset, -hd + inset + band, -hw + inset + band, hd - inset - band, top, TrimPart, VoxelPalette.Cream);
            TopQuad(b, hw - inset - band, -hd + inset + band, hw - inset, hd - inset - band, top, TrimPart, VoxelPalette.Cream);
            TopQuad(b, -0.18f, -0.18f, 0.18f, 0.18f, top, TrimPart, VoxelPalette.Amber);
            return b.Build(BuildingModels.PaletteUv);
        }

        // Wood panelling `length` long on a wall `height` high: raised dark panels in two rows — a low one under the chair
        // rail at PanelRail, a tall one over it — PanelProud off the wall, whose own wood shows between them as the stiles
        // and rails; the chair rail with a brass bead under it and a crown rail under the ceiling. Pivot at the middle of its
        // foot on the wall's face; front −Z. The skirting runs under the panels.
        public static PolyMesh WallPanels(float length, float height)
        {
            if (!(length >= 0.5f) || length > 20 || !(height >= 2.4f) || height > 5) throw new ArgumentOutOfRangeException(nameof(length));
            var b = NewBuilder();
            float hl = length / 2;
            const float stile = 0.1f;
            int n = Math.Max(1, (int)Math.Round(length / 0.75f)); float pitch = length / n;
            for (int k = 0; k < n; k++)
            {
                float x0 = -hl + k * pitch + stile / 2, x1 = x0 + pitch - stile;
                b.Box(new V3(x0, 0.2f, -PanelProud), new V3(x1, PanelRail - 0.1f, 0), 0.006f, TrimPart, VoxelPalette.WoodDark);
                b.Box(new V3(x0, PanelRail + 0.16f, -PanelProud), new V3(x1, height - 0.45f, 0), 0.006f, TrimPart, VoxelPalette.WoodDark);
            }
            b.Box(new V3(-hl, PanelRail - 0.03f, -0.035f), new V3(hl, PanelRail + 0.04f, 0), 0.01f, TrimPart, VoxelPalette.Wood);
            b.Box(new V3(-hl, PanelRail - 0.045f, -0.022f), new V3(hl, PanelRail - 0.03f, 0), 0, MetalPart, VoxelPalette.Brass);
            b.Box(new V3(-hl, height - 0.25f, -0.04f), new V3(hl, height - 0.12f, 0), 0.01f, TrimPart, VoxelPalette.Wood);
            return b.Build(BuildingModels.PaletteUv);
        }

        // ---------- Doorways and the desk lamp ----------
        // Brass casing round a doorway `width` × `height` on a wall: two jambs and a head CasingWidth wide, CasingProud
        // proud. Pivot at the middle of the opening's foot on the wall's face; front −Z.
        public static PolyMesh DoorCasing(float width, float height)
        {
            if (!(width >= 0.6f) || width > 4 || !(height >= 1.8f) || height > 3.5f) throw new ArgumentOutOfRangeException(nameof(width));
            var b = NewBuilder();
            float hw = width / 2, c = CasingWidth;
            foreach (float s in new[] { -1f, 1f })
                b.Box(new V3(s < 0 ? -hw - c : hw, 0, -CasingProud), new V3(s < 0 ? -hw : hw + c, height, 0), 0.006f, MetalPart, VoxelPalette.Brass);
            b.Box(new V3(-hw - c, height, -CasingProud), new V3(hw + c, height + c, 0), 0.006f, MetalPart, VoxelPalette.Brass);
            return b.Build(BuildingModels.PaletteUv);
        }
        // The banker's lamp, BankersLampHeight tall: a round brass foot, a stem and a green shade along X over a warm bulb.
        public static PolyMesh BankersLamp()
        {
            var b = NewBuilder(0.05f);
            PolyShapes.Lathe(b, 0, 0, new[] { (0f, 0.1f), (0.015f, 0.1f), (0.04f, 0.05f) }, 14, MetalPart, VoxelPalette.Brass, capBottom: true, capTop: true);
            PolyShapes.Limb(b, new V3(0, 0.04f, 0), new V3(0, BankersLampHeight - 0.11f, 0), 0.012f, 0.012f, 6, MetalPart, VoxelPalette.Brass);
            const float half = 0.19f, r = 0.085f; const int steps = 8;
            float cy = BankersLampHeight - r - 0.002f;
            int group = b.Group; b.Group = b.NextGroup();
            for (int k = 0; k < steps; k++)
            {
                double a0 = Math.PI * (-0.12 + 1.24 * k / steps), a1 = Math.PI * (-0.12 + 1.24 * (k + 1) / steps);
                float y0 = cy + r * (float)Math.Sin(a0), z0 = r * (float)Math.Cos(a0), y1 = cy + r * (float)Math.Sin(a1), z1 = r * (float)Math.Cos(a1);
                var mid = new V3(0, (float)Math.Sin((a0 + a1) / 2), (float)Math.Cos((a0 + a1) / 2));
                b.Quad(new V3(-half, y0, z0), new V3(half, y0, z0), new V3(half, y1, z1), new V3(-half, y1, z1), mid, TrimPart, VoxelPalette.PineDark);
                b.Quad(new V3(-half, y0, z0 * 0.97f), new V3(half, y0, z0 * 0.97f), new V3(half, y1, z1 * 0.97f), new V3(-half, y1, z1 * 0.97f), mid * -1, TrimPart, VoxelPalette.Cream, 0.9f);
                foreach (float x in new[] { -half, half })
                    b.Triangle(new V3(x, cy, 0), new V3(x, y0, z0), new V3(x, y1, z1), new V3(x, 0, 0), MetalPart, VoxelPalette.Brass);
            }
            b.Group = group;
            PolyShapes.Blob(b, new V3(0, cy - 0.02f, 0), 0.05f, 0.025f, 0.03f, 8, 4, GlowPart, VoxelPalette.LampWarm);
            return b.Build(BuildingModels.PaletteUv);
        }
    }
}
