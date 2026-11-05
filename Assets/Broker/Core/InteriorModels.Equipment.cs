using System;
using System.Collections.Generic;

namespace Broker.Core
{
    // Desk equipment and the espresso machine: dark monitors round the scene's own screens, a keyboard with every key, a
    // mouse, the broker's telephone (navy, or brass once the golden upgrade is bought), loose papers with a pen, the
    // machine of the coffee corner and the café, and a cup on its saucer.
    public static partial class InteriorModels
    {
        // The scene's screen slab fills z ∈ [0, ScreenDepth] inside the bezel's opening and keeps its own green or amber.
        public const float ScreenDepth = 0.025f, BezelSide = 0.03f, BezelChin = 0.045f, BezelProud = 0.006f;
        public const float KeyboardWidth = 0.82f, KeyboardDepth = 0.24f, KeyboardCase = 0.022f, KeyTop = 0.036f;
        public const float PhoneHeight = 0.28f;
        public const int PhoneKeys = 12;
        private const float KeyUnit = 0.0385f, KeyPitch = 0.042f, KeyGap = 0.0045f;
        // Rows of the main block from the front (space bar) to the back (numbers), key widths in units; the numeric pad on
        // the right is four keys wide and five deep with a double-width zero.
        private static readonly float[][] KeyRows =
        {
            new[] { 1.25f, 1.25f, 1.25f, 6.25f, 1.25f, 1.25f, 1.25f, 1.25f },
            new[] { 2.25f, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2.75f },
            new[] { 1.75f, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2.25f },
            new[] { 1.5f, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1.5f },
            new[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2f },
        };
        private static readonly float[][] PadRows = { new[] { 2f, 1, 1 }, new[] { 1f, 1, 1, 1 }, new[] { 1f, 1, 1, 1 }, new[] { 1f, 1, 1, 1 }, new[] { 1f, 1, 1, 1 } };
        public static int KeyCount { get { int n = 0; foreach (var row in KeyRows) n += row.Length; foreach (var row in PadRows) n += row.Length; return n; } }

        // ---------- Monitor ----------
        // Flat monitor round a screen `screenW` × `screenH` centred `screenCentre` over the desk: a thin bezel whose opening
        // leaves the slab z ∈ [0, ScreenDepth] to the scene's screen, a back plate and a tapered back, a neck down to a round
        // foot and a green power light in the chin. Pivot on the desk under the screen's face (z = 0); the screen faces −Z.
        public static PolyMesh Monitor(float screenW, float screenH, float screenCentre)
        {
            float hw = screenW / 2, y0 = screenCentre - screenH / 2, y1 = screenCentre + screenH / 2;
            if (!(screenW >= 0.2f) || screenW > 1.5f || !(screenH >= 0.15f) || !(y0 - BezelChin >= 0.025f)) throw new ArgumentOutOfRangeException(nameof(screenW));
            var b = NewBuilder(0.05f);
            byte shell = VoxelPalette.Ink;
            float left = -hw - BezelSide, right = hw + BezelSide, bottom = y0 - BezelChin, top = y1 + BezelSide, back = ScreenDepth + 0.005f;
            Frame(b, left, bottom, right, top, -hw, y0, hw, y1, -BezelProud, back, 0.008f, TrimPart, shell);
            b.Box(new V3(left, bottom, back), new V3(right, top, back + 0.014f), 0.006f, TrimPart, shell, 0.92f);
            // The tapered back round the neck's mount.
            float zb = back + 0.014f, zt = zb + 0.045f, ym = screenCentre - screenH * 0.08f;
            PolyShapes.Block(b,
                new[] { new V3(-hw * 0.7f, bottom + 0.03f, zb), new V3(hw * 0.7f, bottom + 0.03f, zb), new V3(hw * 0.7f, top - 0.04f, zb), new V3(-hw * 0.7f, top - 0.04f, zb) },
                new[] { new V3(-hw * 0.4f, ym - screenH * 0.25f, zt), new V3(hw * 0.4f, ym - screenH * 0.25f, zt), new V3(hw * 0.4f, ym + screenH * 0.2f, zt), new V3(-hw * 0.4f, ym + screenH * 0.2f, zt) },
                TrimPart, shell, 0.9f);
            // Neck and foot.
            float foot = 0.09f + 0.12f * (screenW - 0.5f), zf = zt - 0.005f, neck = 0.035f;
            PolyShapes.Block(b,
                new[] { new V3(-neck, 0.012f, zf - 0.012f), new V3(neck, 0.012f, zf - 0.012f), new V3(neck, 0.012f, zf + 0.012f), new V3(-neck, 0.012f, zf + 0.012f) },
                new[] { new V3(-neck, ym, zt - 0.03f), new V3(neck, ym, zt - 0.03f), new V3(neck, ym, zt - 0.006f), new V3(-neck, ym, zt - 0.006f) }, TrimPart, VoxelPalette.MetalDark);
            PolyShapes.Lathe(b, 0, zf, new[] { (0f, foot), (0.006f, foot), (0.013f, foot - 0.012f) }, 14, TrimPart, VoxelPalette.MetalDark, capTop: true);
            FrontDisc(b, hw - 0.025f, bottom + BezelChin * 0.5f, -BezelProud - 0.0005f, 0.0045f, 6, GlowPart, VoxelPalette.SignalGreen);
            return b.Build(BuildingModels.PaletteUv);
        }

        // ---------- Keyboard and mouse ----------
        // Keyboard KeyboardWidth × KeyboardDepth: a dark case KeyboardCase high with five rows of cream keys up to KeyTop and
        // a numeric pad; the space bar towards −Z (the typist).
        public static PolyMesh Keyboard()
        {
            var b = NewBuilder(0.02f);
            float hw = KeyboardWidth / 2, hd = KeyboardDepth / 2;
            b.Box(new V3(-hw, 0, -hd), new V3(hw, KeyboardCase, hd), 0.006f, TrimPart, VoxelPalette.StoneDark);
            float main = 15 * KeyUnit, pad = 4 * KeyUnit, gap = 0.5f * KeyUnit;
            float x0 = -(main + gap + pad) / 2, z0 = -2.5f * KeyPitch;
            void Row(float[] widths, float xStart, int row)
            {
                float x = xStart, z = z0 + row * KeyPitch;
                foreach (float w in widths)
                {
                    Cap(b, x + KeyGap / 2, x + w * KeyUnit - KeyGap / 2, z + KeyGap / 2, z + KeyPitch - KeyGap / 2, KeyboardCase, KeyTop, 0.0035f,
                        TrimPart, VoxelPalette.Cream, row % 2 == 0 ? 1 : 0.97f);
                    x += w * KeyUnit;
                }
            }
            for (int r = 0; r < KeyRows.Length; r++) Row(KeyRows[r], x0, r);
            for (int r = 0; r < PadRows.Length; r++) Row(PadRows[r], x0 + main + gap, r);
            return b.Build(BuildingModels.PaletteUv);
        }
        // Mouse 6.4 × 10 cm: a cream dome with a dark wheel towards −Z.
        public static PolyMesh Mouse()
        {
            var b = NewBuilder(0.02f);
            PolyShapes.Lump(b, new V3(0, 0, 0.005f), 0.032f, 0.03f, 0.05f, 10, 6, 0, 0, TrimPart, VoxelPalette.Cream, dome: true);
            PolyShapes.Limb(b, new V3(-0.004f, 0.026f, -0.021f), new V3(0.004f, 0.026f, -0.021f), 0.0075f, 0.0075f, 6, TrimPart, VoxelPalette.StoneDark);
            return b.Build(BuildingModels.PaletteUv);
        }

        // ---------- Telephone ----------
        // Desk telephone 0.64 × 0.4 m and PhoneHeight high: a base with a console sloping to the caller (−Z) with twelve
        // cream keys and a green display, the handset across the top on two cradles and a coiled cord down the left side.
        // Navy, or polished brass for the golden upgrade.
        public static PolyMesh Phone(bool golden)
        {
            var b = NewBuilder(0.06f);
            int part = golden ? MetalPart : TrimPart;
            byte body = golden ? VoxelPalette.Brass : VoxelPalette.Navy;
            b.Box(new V3(-0.29f, 0, -0.17f), new V3(0.29f, 0.012f, 0.17f), 0, TrimPart, VoxelPalette.Ink);
            b.Box(new V3(-0.31f, 0.012f, -0.19f), new V3(0.31f, 0.065f, 0.19f), 0.018f, part, body);
            // Console: its front face slopes from the base's front edge up to the flat top.
            const float frontZ = -0.18f, frontY = 0.065f, slopeZ = -0.07f, slopeY = 0.15f;
            PolyShapes.Block(b,
                new[] { new V3(-0.29f, frontY, frontZ), new V3(0.29f, frontY, frontZ), new V3(0.29f, frontY, 0.18f), new V3(-0.29f, frontY, 0.18f) },
                new[] { new V3(-0.27f, slopeY, slopeZ), new V3(0.27f, slopeY, slopeZ), new V3(0.27f, 0.165f, 0.16f), new V3(-0.27f, 0.165f, 0.16f) }, part, body);
            // Keys and display on the slope: u runs up it, n out of it.
            float dz = slopeZ - frontZ, dy = slopeY - frontY, len = (float)Math.Sqrt(dz * dz + dy * dy);
            float tilt = (float)(Math.Atan2(dy, dz) * 180 / Math.PI);
            V3 u = new V3(0, dy / len, dz / len), n = new V3(0, dz / len, -dy / len);
            V3 On(float x, float t, float lift) => new V3(x, frontY, frontZ) + u * t + n * lift;
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 3; col++)
                {
                    b.Place = Pose(On(-0.15f + col * 0.045f, 0.026f + row * 0.03f, 0), 0, -tilt);
                    Cap(b, -0.016f, 0.016f, -0.0115f, 0.0115f, 0, 0.011f, 0.003f, TrimPart, VoxelPalette.Cream);
                }
            b.Place = null;
            V3 d0 = On(0.02f, 0.04f, 0.001f), d1 = On(0.22f, 0.04f, 0.001f), d2 = On(0.22f, 0.11f, 0.001f), d3 = On(0.02f, 0.11f, 0.001f);
            b.Quad(d0, d1, d2, d3, n, GlowPart, VoxelPalette.ScreenGreen);
            // Cradles under the handset's ends.
            foreach (float s in new[] { -1f, 1f })
            {
                float xa = s * 0.21f, xb = s * 0.28f;
                PolyShapes.Block(b,
                    new[] { new V3(Math.Min(xa, xb), 0.14f, -0.05f), new V3(Math.Max(xa, xb), 0.14f, -0.05f), new V3(Math.Max(xa, xb), 0.14f, 0.07f), new V3(Math.Min(xa, xb), 0.14f, 0.07f) },
                    new[] { new V3(Math.Min(xa, xb) + 0.01f, 0.196f, -0.035f), new V3(Math.Max(xa, xb) - 0.01f, 0.196f, -0.035f), new V3(Math.Max(xa, xb) - 0.01f, 0.196f, 0.055f), new V3(Math.Min(xa, xb) + 0.01f, 0.196f, 0.055f) },
                    part, body, 0.92f);
            }
            // Handset: ear and mouth cups facing down, the grip arching between them.
            foreach (float s in new[] { -1f, 1f })
                PolyShapes.Lathe(b, s * 0.25f, 0.01f, new[] { (0.196f, 0.036f), (0.201f, 0.047f), (0.216f, 0.051f), (0.234f, 0.047f), (0.246f, 0.032f), (0.25f, 0f) }, 10, part, body,
                    capBottom: true);
            var grip = new List<V3>();
            for (int i = 0; i <= 6; i++)
            {
                float t = i / 6f, x = -0.25f + 0.5f * t;
                grip.Add(new V3(x, 0.232f + 0.024f * (float)Math.Sin(Math.PI * t), 0.01f));
            }
            PolyShapes.Sweep(b, grip, 0.021f, 0.021f, 8, new V3(0, 0, 1), part, body);
            // Coiled cord from under the left cup into the base's left side.
            V3 from = new V3(-0.3f, 0.2f, 0.03f), to = new V3(-0.318f, 0.045f, 0.11f), axis = to - from;
            V3 e1 = V3.Cross(axis, new V3(1, 0, 0)).Normalized, e2 = V3.Cross(axis.Normalized, e1);
            var coil = new List<V3>();
            const int turns = 5, perTurn = 6;
            for (int i = 0; i <= turns * perTurn; i++)
            {
                float t = i / (float)(turns * perTurn); double a = 2 * Math.PI * i / perTurn;
                float r = 0.012f * Math.Min(1, Math.Min(t, 1 - t) * 8);
                coil.Add(from + axis * t + e1 * (r * (float)Math.Cos(a)) + e2 * (r * (float)Math.Sin(a)));
            }
            PolyShapes.Sweep(b, coil, 0.0045f, 0.0045f, 4, axis, TrimPart, VoxelPalette.Ink);
            return b.Build(BuildingModels.PaletteUv);
        }

        // ---------- Papers ----------
        // A loose stack of five sheets inside `width` × `depth`, each a little askew, lines of type on the top one and a
        // navy pen with a brass clip across it.
        public static PolyMesh Papers(float width, float depth, int seed)
        {
            if (!(width >= 0.15f) || width > 1 || !(depth >= 0.1f) || depth > 1) throw new ArgumentOutOfRangeException(nameof(width));
            var b = NewBuilder(0.012f);
            const int sheets = 5; const float sheet = 0.0035f;
            float sw = width * 0.78f, sd = depth * 0.8f;
            for (int i = 0; i < sheets; i++)
            {
                float turn = (Rand(seed, i) - 0.5f) * 9, dx = (Rand(seed, i + 10) - 0.5f) * width * 0.05f, dz = (Rand(seed, i + 20) - 0.5f) * depth * 0.05f;
                b.Place = Pose(new V3(dx, i * sheet, dz), turn);
                b.Box(new V3(-sw / 2, 0, -sd / 2), new V3(sw / 2, sheet, sd / 2), 0, TrimPart, i % 2 == 0 ? VoxelPalette.CarWhite : VoxelPalette.Cream, i % 2 == 0 ? 1 : 0.97f);
            }
            // Type on the top sheet: a heading and lines of uneven length.
            float y = sheet + 0.0006f, line = sd / 12;
            void Line(float x0, float x1, float z, float h, byte color)
                => b.Quad(new V3(x0, y, z), new V3(x1, y, z), new V3(x1, y, z + h), new V3(x0, y, z + h), new V3(0, 1, 0), TrimPart, color);
            Line(-sw * 0.38f, sw * 0.05f, sd * 0.36f, line * 0.45f, VoxelPalette.Navy);
            for (int k = 0; k < 7; k++)
            {
                float z = sd * 0.26f - k * line, end = sw * (0.15f + 0.22f * Rand(seed, 30 + k));
                Line(-sw * 0.38f, end, z, line * 0.22f, VoxelPalette.StoneDark);
            }
            b.Place = null;
            // The pen lies across the stack.
            float top = sheets * sheet + 0.006f;
            V3 p0 = new V3(-width * 0.3f, top, depth * 0.18f), p1 = new V3(width * 0.22f, top, -depth * 0.2f);
            PolyShapes.Limb(b, p0, p1, 0.006f, 0.006f, 6, TrimPart, VoxelPalette.Navy);
            V3 dir = (p1 - p0).Normalized;
            PolyShapes.Limb(b, p1, p1 + dir * 0.022f, 0.006f, 0.0015f, 6, MetalPart, VoxelPalette.Brass, caps: false);
            PolyShapes.Limb(b, p0 + dir * 0.012f + new V3(0, 0.006f, 0), p0 + dir * 0.05f + new V3(0, 0.0075f, 0), 0.0022f, 0.0022f, 4, MetalPart, VoxelPalette.Brass);
            return b.Build(BuildingModels.PaletteUv);
        }

        // ---------- Espresso machine ----------
        // Espresso machine 0.7 × 0.6 × 0.55 m: a dark body with a brass panel carrying the pressure gauge and the green and
        // amber lamps, two chrome group heads with their portafilters over a drip tray with two cups, a steam wand on the
        // right and a cup warmer with a rail on top. Front −Z.
        public static PolyMesh CoffeeMachine()
        {
            var b = NewBuilder(0.2f);
            const float face = -0.1f;
            b.Box(new V3(-0.33f, 0, face), new V3(0.33f, 0.5f, 0.27f), 0.025f, TrimPart, VoxelPalette.Ink);
            b.Box(new V3(-0.305f, 0.31f, face - 0.012f), new V3(0.305f, 0.475f, face + 0.004f), 0.006f, MetalPart, VoxelPalette.Brass);
            // Gauge in a chrome bezel; lamps either side.
            float panel = face - 0.012f, gy = 0.393f;
            PolyShapes.Limb(b, new V3(0, gy, panel), new V3(0, gy, panel - 0.008f), 0.047f, 0.047f, 14, MetalPart, VoxelPalette.Chrome, caps: false);
            FrontRing(b, 0, gy, panel - 0.008f, 0.039f, 0.047f, 14, MetalPart, VoxelPalette.Chrome);
            FrontDisc(b, 0, gy, panel - 0.005f, 0.039f, 14, TrimPart, VoxelPalette.Cream);
            b.Quad(new V3(-0.002f, gy - 0.003f, panel - 0.0055f), new V3(0.026f, gy + 0.018f, panel - 0.0055f), new V3(0.023f, gy + 0.021f, panel - 0.0055f),
                new V3(-0.004f, gy + 0.001f, panel - 0.0055f), new V3(0, 0, -1), TrimPart, VoxelPalette.CarRed);
            FrontDisc(b, 0.215f, gy, panel - 0.001f, 0.016f, 8, GlowPart, VoxelPalette.SignalGreen);
            FrontDisc(b, -0.215f, gy, panel - 0.001f, 0.016f, 8, GlowPart, VoxelPalette.SignalAmber);
            // Group heads with portafilters, cups underneath.
            foreach (float x in new[] { -0.13f, 0.13f })
            {
                b.Box(new V3(x - 0.05f, 0.255f, -0.17f), new V3(x + 0.05f, 0.3f, face), 0.008f, MetalPart, VoxelPalette.Chrome);
                PolyShapes.Lathe(b, x, -0.15f, new[] { (0.212f, 0.046f), (0.255f, 0.046f) }, 10, MetalPart, VoxelPalette.Chrome, capBottom: true);
                PolyShapes.Lathe(b, x, -0.15f, new[] { (0.186f, 0.034f), (0.212f, 0.042f) }, 10, MetalPart, VoxelPalette.Chrome, capBottom: true);
                PolyShapes.Limb(b, new V3(x, 0.188f, -0.15f), new V3(x, 0.168f, -0.15f), 0.009f, 0.007f, 6, MetalPart, VoxelPalette.Chrome);
                PolyShapes.Limb(b, new V3(x, 0.199f, -0.19f), new V3(x, 0.186f, -0.26f), 0.011f, 0.013f, 6, TrimPart, VoxelPalette.Ink);
                Cup(b, x, -0.16f, 0.04f, 0.026f, 0.045f, 8, false, VoxelPalette.CarWhite);
            }
            // Drip tray with its grille.
            b.Box(new V3(-0.26f, 0, -0.27f), new V3(0.26f, 0.04f, face), 0.008f, MetalPart, VoxelPalette.Chrome);
            for (int k = 0; k < 6; k++)
            {
                float z = -0.252f + k * 0.024f;
                b.Quad(new V3(-0.22f, 0.0402f, z), new V3(0.22f, 0.0402f, z), new V3(0.22f, 0.0402f, z + 0.009f), new V3(-0.22f, 0.0402f, z + 0.009f),
                    new V3(0, 1, 0), TrimPart, VoxelPalette.Ink);
            }
            // Steam wand.
            PolyShapes.Limb(b, new V3(0.285f, 0.3f, face - 0.005f), new V3(0.29f, 0.29f, -0.14f), 0.011f, 0.011f, 6, MetalPart, VoxelPalette.Chrome);
            PolyShapes.Limb(b, new V3(0.29f, 0.29f, -0.14f), new V3(0.3f, 0.12f, -0.19f), 0.007f, 0.006f, 6, MetalPart, VoxelPalette.Chrome);
            // Cup warmer and rail.
            b.Box(new V3(-0.31f, 0.5f, -0.08f), new V3(0.31f, 0.512f, 0.25f), 0, MetalPart, VoxelPalette.Chrome);
            var rail = new List<V3> { new V3(-0.3f, 0.565f, -0.07f), new V3(-0.3f, 0.565f, 0.2f), new V3(-0.29f, 0.565f, 0.232f), new V3(-0.26f, 0.565f, 0.24f),
                new V3(0.26f, 0.565f, 0.24f), new V3(0.29f, 0.565f, 0.232f), new V3(0.3f, 0.565f, 0.2f), new V3(0.3f, 0.565f, -0.07f) };
            PolyShapes.Sweep(b, rail, 0.007f, 0.007f, 6, new V3(0, 1, 0), MetalPart, VoxelPalette.Chrome);
            foreach (var p in new[] { new V3(-0.3f, 0, -0.07f), new V3(0.3f, 0, -0.07f), new V3(-0.26f, 0, 0.24f), new V3(0.26f, 0, 0.24f) })
                PolyShapes.Limb(b, new V3(p.X, 0.512f, p.Z), new V3(p.X, 0.568f, p.Z), 0.006f, 0.006f, 6, MetalPart, VoxelPalette.Chrome, caps: false);
            return b.Build(BuildingModels.PaletteUv);
        }
        // A white cup of coffee on its saucer, 13 cm across.
        public static PolyMesh CoffeeCup()
        {
            var b = NewBuilder(0.02f);
            Cup(b, 0, 0, 0, 0.036f, 0.06f, 10, true, VoxelPalette.CarWhite);
            return b.Build(BuildingModels.PaletteUv);
        }
    }
}
