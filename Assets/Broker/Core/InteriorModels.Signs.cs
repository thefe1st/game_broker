using System;
using System.Collections.Generic;

namespace Broker.Core
{
    // What a floating badge says without a word: the person is free to talk or to take work, is on the telephone finding
    // clients, is writing a dossier, or is on a coffee break.
    public enum BadgeKind { Talk, Calls, Dossier, Break }
    // The city's signs without words: the café's cup, the press kiosk's newspaper, the map stand's pin and the company
    // stand's rising chart.
    public enum Pictogram { Cup, Newspaper, Pin, Chart }

    // Signs instead of inscriptions (stage 6, the author: «надписи все пора убирать», fewer words, clearer icons): flat
    // pictograms in the XY plane facing −Z, the badges floating over the people the player deals with, the exit sign over
    // the office door and the lift's arrows. A glyph's unit `s` is its full height; strokes lie a hair in front of their
    // board.
    public static partial class InteriorModels
    {
        public const float BadgeRadius = 0.17f, LogoStretch = 1.6f;
        public const float ExitSignWidth = 0.46f, ExitSignHeight = 0.2f, ExitSignDepth = 0.07f;
        private static readonly V3 Facing = new V3(0, 0, -1);

        // ---------- Glyph pieces ----------
        // A straight stroke `w` wide from (x0, y0) to (x1, y1) at depth z, carried on by half its width at both ends.
        private static void Stroke(PolyBuilder b, float x0, float y0, float x1, float y1, float w, float z, int part, byte color)
        {
            float dx = x1 - x0, dy = y1 - y0, len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-5f) return;
            float ux = dx / len * w / 2, uy = dy / len * w / 2;
            b.Quad(new V3(x0 - ux + uy, y0 - uy - ux, z), new V3(x0 - ux - uy, y0 - uy + ux, z), new V3(x1 + ux - uy, y1 + uy + ux, z),
                new V3(x1 + ux + uy, y1 + uy - ux, z), Facing, part, color);
        }
        private static void Strokes(PolyBuilder b, IList<(float x, float y)> path, float w, float z, int part, byte color)
        {
            for (int i = 0; i + 1 < path.Count; i++) Stroke(b, path[i].x, path[i].y, path[i + 1].x, path[i + 1].y, w, z, part, color);
        }
        // An arc of radius r round (cx, cy) from angle a0 to a1 (degrees, anticlockwise from +X) as `steps` strokes.
        private static void Arc(PolyBuilder b, float cx, float cy, float r, float a0, float a1, int steps, float w, float z, int part, byte color)
        {
            var path = new List<(float, float)>();
            for (int i = 0; i <= steps; i++)
            {
                double a = (a0 + (a1 - a0) * i / steps) * Math.PI / 180;
                path.Add((cx + r * (float)Math.Cos(a), cy + r * (float)Math.Sin(a)));
            }
            Strokes(b, path, w, z, part, color);
        }
        private static void Fill(PolyBuilder b, float z, int part, byte color, params (float x, float y)[] points)
        {
            var ring = new V3[points.Length];
            for (int i = 0; i < points.Length; i++) ring[i] = new V3(points[i].x, points[i].y, z);
            b.Polygon(ring, Facing, part, color);
        }

        // ---------- Glyphs ----------
        // A cup on its saucer with two wisps of steam, the handle to the right.
        private static void CupGlyph(PolyBuilder b, float cx, float cy, float s, float z, int part, byte color)
        {
            (float, float) P(float x, float y) => (cx + x * s, cy + y * s);
            Fill(b, z, part, color, P(-0.25f, -0.2f), P(0.13f, -0.2f), P(0.2f, 0.14f), P(-0.32f, 0.14f));
            Arc(b, cx + 0.2f * s, cy - 0.02f * s, 0.1f * s, -80, 80, 6, 0.055f * s, z, part, color);
            Fill(b, z, part, color, P(-0.42f, -0.3f), P(0.3f, -0.3f), P(0.3f, -0.245f), P(-0.42f, -0.245f));
            foreach (float x in new[] { -0.15f, 0.03f })
                Strokes(b, new[] { P(x, 0.22f), P(x + 0.05f, 0.31f), P(x, 0.4f), P(x + 0.04f, 0.47f) }, 0.045f * s, z, part, color);
        }
        // A telephone handset tipped on its side: a curved grip between the ear and the mouth pieces.
        private static void HandsetGlyph(PolyBuilder b, float cx, float cy, float s, float z, int part, byte color)
        {
            Arc(b, cx + 0.2f * s, cy + 0.2f * s, 0.4f * s, 185, 265, 6, 0.12f * s, z, part, color);
            foreach (var (x, y, a) in new[] { (-0.2f, 0.17f, 175f), (0.17f, -0.2f, 275f) })
            {
                double r = a * Math.PI / 180; float ux = (float)Math.Cos(r), uy = (float)Math.Sin(r), w = 0.11f * s, l = 0.1f * s;
                float px = cx + x * s, py = cy + y * s;
                Fill(b, z, part, color, (px - uy * w, py + ux * w), (px + uy * w, py - ux * w), (px + uy * w + ux * l, py - ux * w + uy * l),
                    (px - uy * w + ux * l, py + ux * w + uy * l));
            }
        }
        // A page with a turned corner and four lines of writing.
        private static void PageGlyph(PolyBuilder b, float cx, float cy, float s, float z, int part, byte page, byte ink)
        {
            (float, float) P(float x, float y) => (cx + x * s, cy + y * s);
            Fill(b, z, part, page, P(-0.27f, -0.4f), P(0.27f, -0.4f), P(0.27f, 0.24f), P(0.11f, 0.4f), P(-0.27f, 0.4f));
            Fill(b, z - 0.001f, part, VoxelPalette.Cream, P(0.11f, 0.4f), P(0.11f, 0.24f), P(0.27f, 0.24f));
            for (int k = 0; k < 4; k++)
            {
                float y = 0.14f - k * 0.13f, x1 = k == 3 ? 0.04f : 0.17f;
                Stroke(b, cx - 0.16f * s, cy + y * s, cx + x1 * s, cy + y * s, 0.045f * s, z - 0.001f, part, ink);
            }
        }
        // A speech bubble with three dots.
        private static void BubbleGlyph(PolyBuilder b, float cx, float cy, float s, float z, int part, byte bubble, byte dots)
        {
            FrontDisc(b, cx, cy + 0.06f * s, z, 0.36f * s, 20, part, bubble);
            Fill(b, z, part, bubble, (cx - 0.26f * s, cy - 0.12f * s), (cx - 0.02f * s, cy - 0.26f * s), (cx - 0.34f * s, cy - 0.42f * s));
            for (int k = -1; k <= 1; k++) FrontDisc(b, cx + k * 0.16f * s, cy + 0.06f * s, z - 0.001f, 0.055f * s, 10, part, dots);
        }
        // A folded newspaper: the page, a dark masthead and two columns of lines.
        private static void NewspaperGlyph(PolyBuilder b, float cx, float cy, float s, float z, int part, byte page, byte ink)
        {
            (float, float) P(float x, float y) => (cx + x * s, cy + y * s);
            Fill(b, z, part, page, P(-0.36f, -0.34f), P(0.36f, -0.34f), P(0.36f, 0.34f), P(-0.36f, 0.34f));
            Fill(b, z - 0.001f, part, ink, P(-0.29f, 0.16f), P(0.29f, 0.16f), P(0.29f, 0.27f), P(-0.29f, 0.27f));
            for (int k = 0; k < 4; k++)
                foreach (float x0 in new[] { -0.29f, 0.04f })
                    Stroke(b, cx + x0 * s, cy + (0.06f - k * 0.1f) * s, cx + (x0 + 0.25f) * s, cy + (0.06f - k * 0.1f) * s, 0.035f * s, z - 0.001f, part, ink);
        }
        // A map pin: a round head with a hole over its point.
        private static void PinGlyph(PolyBuilder b, float cx, float cy, float s, float z, int part, byte pin, byte hole)
        {
            FrontDisc(b, cx, cy + 0.13f * s, z, 0.27f * s, 20, part, pin);
            Fill(b, z, part, pin, (cx - 0.235f * s, cy), (cx + 0.235f * s, cy), (cx, cy - 0.44f * s));
            FrontDisc(b, cx, cy + 0.13f * s, z - 0.001f, 0.1f * s, 12, part, hole);
        }
        // Three rising bars under a rising arrow: the market going up. `wide` stretches it into the company's mark.
        private static void ChartGlyph(PolyBuilder b, float cx, float cy, float s, float wide, float z, int part, byte bars, byte arrow)
            => ChartGlyph(b, cx, cy, s, wide, z, part, bars, part, arrow);
        private static void ChartGlyph(PolyBuilder b, float cx, float cy, float s, float wide, float z, int barPart, byte bars, int arrowPart, byte arrow)
        {
            (float, float) P(float x, float y) => (cx + x * s * wide, cy + y * s);
            for (int k = 0; k < 3; k++)
            {
                float x0 = -0.33f + k * 0.25f, top = -0.18f + k * 0.17f;
                Fill(b, z, barPart, bars, P(x0, -0.42f), P(x0 + 0.15f, -0.42f), P(x0 + 0.15f, top), P(x0, top));
            }
            var line = new[] { P(-0.44f, -0.06f), P(-0.17f, 0.12f), P(0.03f, 0.02f), P(0.3f, 0.3f) };
            Strokes(b, line, 0.07f * s, z - 0.001f, arrowPart, arrow);
            float hx = cx + 0.36f * s * wide, hy = cy + 0.36f * s, h = 0.17f * s;
            Fill(b, z - 0.001f, arrowPart, arrow, (hx + 0.04f * s, hy + 0.04f * s), (hx - h, hy - 0.01f * s), (hx - 0.01f * s, hy - h));
        }
        // Out through the door: a door frame on the right and an arrow running into it.
        private static void ExitGlyph(PolyBuilder b, float cx, float cy, float s, float z, int part, byte color)
        {
            (float, float) P(float x, float y) => (cx + x * s, cy + y * s);
            Strokes(b, new[] { P(0.04f, -0.42f), P(0.04f, 0.42f), P(0.42f, 0.42f), P(0.42f, -0.42f) }, 0.07f * s, z, part, color);
            Stroke(b, cx - 0.5f * s, cy, cx + 0.12f * s, cy, 0.1f * s, z - 0.001f, part, color);
            Fill(b, z - 0.001f, part, color, P(0.3f, 0), P(0.08f, 0.2f), P(0.08f, -0.2f));
        }
        // A pictogram of the city's signs at (cx, cy), `s` high.
        private static void PictogramGlyph(PolyBuilder b, Pictogram kind, float cx, float cy, float s, float z, int part, byte color, byte ink)
        {
            switch (kind)
            {
                case Pictogram.Cup: CupGlyph(b, cx, cy, s, z, part, color); break;
                case Pictogram.Newspaper: NewspaperGlyph(b, cx, cy, s, z, part, color, ink); break;
                case Pictogram.Pin: PinGlyph(b, cx, cy, s, z, part, color, ink); break;
                default: ChartGlyph(b, cx, cy, s, 1, z, part, color, ink); break;
            }
        }

        // ---------- Signs ----------
        // A pictogram alone, `size` high, centred on its pivot at z = 0 facing −Z: for sign bands that already have a board.
        public static PolyMesh PictogramSign(Pictogram kind, float size, byte color, byte ink)
        {
            if (!(size >= 0.1f) || size > 3) throw new ArgumentOutOfRangeException(nameof(size));
            var b = NewBuilder(0);
            PictogramGlyph(b, kind, 0, 0, size, 0, TrimPart, color, ink);
            return b.Build(BuildingModels.PaletteUv);
        }
        // A badge floating over a person the player deals with, BadgeRadius round: a glowing disc in the badge's colour with
        // a dark rim, a pointer down to the head and the glyph; its back closed in ink. Pivot at the disc's centre; it faces
        // −Z and the scene turns it to the camera.
        public static PolyMesh Badge(BadgeKind kind)
        {
            var b = NewBuilder(0);
            const float r = BadgeRadius, rim = 0.018f;
            byte face = kind == BadgeKind.Talk ? VoxelPalette.Cream : kind == BadgeKind.Break ? VoxelPalette.SignalAmber : VoxelPalette.SignalGreen;
            FrontDisc(b, 0, 0, 0, r, 24, GlowPart, face);
            FrontRing(b, 0, 0, 0, r, r + rim, 24, TrimPart, VoxelPalette.Ink);
            Fill(b, 0, TrimPart, VoxelPalette.Ink, (-0.055f, -r - rim + 0.004f), (0.055f, -r - rim + 0.004f), (0, -r - 0.075f));
            var centre = new V3(0, 0, 0.004f);
            for (int k = 0; k < 24; k++)
            {
                double a0 = 2 * Math.PI * k / 24, a1 = 2 * Math.PI * (k + 1) / 24; float R = r + rim;
                b.Triangle(centre, new V3(R * (float)Math.Cos(a0), R * (float)Math.Sin(a0), 0.004f), new V3(R * (float)Math.Cos(a1), R * (float)Math.Sin(a1), 0.004f),
                    new V3(0, 0, 1), TrimPart, VoxelPalette.Ink);
            }
            const float z = -0.003f, s = r * 1.45f;
            switch (kind)
            {
                case BadgeKind.Talk: BubbleGlyph(b, 0, -0.01f, s, z, TrimPart, VoxelPalette.CarWhite, VoxelPalette.Ink); break;
                case BadgeKind.Calls: HandsetGlyph(b, 0, 0, s, z, TrimPart, VoxelPalette.Ink); break;
                case BadgeKind.Dossier: PageGlyph(b, 0, 0, s, z, TrimPart, VoxelPalette.CarWhite, VoxelPalette.Ink); break;
                default: CupGlyph(b, 0.01f, 0, s, z, TrimPart, VoxelPalette.Ink); break;
            }
            return b.Build(BuildingModels.PaletteUv);
        }
        // The company's mark instead of its name: three brass bars rising under a cream arrow, `height` high and about 1.35
        // times as wide, flat at z = 0 facing −Z. On the plaque over the office's middle window and over the lift on the plaza.
        public static PolyMesh CompanyLogo(float height)
        {
            if (!(height >= 0.1f) || height > 2) throw new ArgumentOutOfRangeException(nameof(height));
            var b = NewBuilder(0);
            ChartGlyph(b, 0.05f * height, 0, height, LogoStretch, 0, MetalPart, VoxelPalette.Brass, TrimPart, VoxelPalette.Cream);
            return b.Build(BuildingModels.PaletteUv);
        }
        // Exit sign over the office door, ExitSignWidth × ExitSignHeight: a pale housing ExitSignDepth deep off the wall with
        // a glowing green face and the white door-and-arrow. Pivot at the middle of its back on the wall (z = 0); front −Z.
        public static PolyMesh ExitSign()
        {
            var b = NewBuilder(0);
            float hw = ExitSignWidth / 2, hh = ExitSignHeight / 2, d = ExitSignDepth;
            b.Box(new V3(-hw, -hh, -d), new V3(hw, hh, 0), 0.008f, TrimPart, VoxelPalette.MetalLight);
            FrontQuad(b, -hw + 0.015f, -hh + 0.015f, hw - 0.015f, hh - 0.015f, -d - 0.001f, GlowPart, VoxelPalette.SignalGreen);
            ExitGlyph(b, 0.02f, 0, ExitSignHeight * 0.72f, -d - 0.003f, GlowPart, VoxelPalette.Headlight);
            return b.Build(BuildingModels.PaletteUv);
        }
    }
}
