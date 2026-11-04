using System;
using System.Collections.Generic;

namespace Broker.Core
{
    // The crossing's signals and push button in the street furniture's trim: a dark pole with clamps, a car head with three
    // round lenses under hoods, a walk head with the standing and the walking figure, the yellow button box. One model per
    // lit state, as the voxel ones (VoxelProps.SignalPost, CarSignalHead, PedSignalHead, CrosswalkButton): the lit lens on
    // the glow part, the others in dark tones of their colours. Pivots and bounds as the voxel models; fronts face −Z.
    public static partial class StreetModels
    {
        // Where BuildSignals hangs the heads and the button on the post: their pivots over its foot, and from the pole's
        // axis to their backs (SignalClamp, the clamps' radius) plus each model's depth behind its pivot.
        public const float SignalHeight = 3.6f, CarSignalY = 2.4f, WalkSignalY = 1.3f, ButtonY = 0.85f, SignalClamp = 0.1f;
        public const float CarSignalBack = 0.2f, WalkSignalBack = 0.15f, ButtonBack = 0.075f;
        // Lens centres of the car head over its pivot (red at the top, then amber and green), the walk head's windows
        // (the standing figure over the walking one) and the button's lamp.
        public const float CarLensTop = 1.0f, CarLensStep = 0.4f, CarLensRadius = 0.1f, WalkStandY = 0.8f, WalkGoY = 0.3f, ButtonLampY = 0.3f;
        public const float SignalBandBottom = 1.2f, SignalBandTop = 1.5f;
        // Heights of the brackets behind the heads and the button over their pivots; the post has a clamp at each.
        private static readonly float[] CarBrackets = { 0.3f, 0.9f }, WalkBrackets = { 0.25f, 0.85f }, ButtonBrackets = { 0.2f };
        private static readonly byte[] CarLit = { VoxelPalette.SignalRed, VoxelPalette.SignalAmber, VoxelPalette.SignalGreen };
        private static readonly byte[] CarDark = { VoxelPalette.BrickDark, VoxelPalette.WoodDark, VoxelPalette.PineDark };
        private const float PoleRadius = 0.07f;

        // The heads and the button hang in the air: no contact shade from their own feet.
        private static PolyBuilder HangingBuilder() => new PolyBuilder(BuildingModels.PartCount, SmoothAngle, WallPart, VoxelSurfaces.MetresPerTile);

        // Signal post 3.6 m: an octagonal foot, a dark pole with a yellow band at 1.2–1.5 m, a clamp at every bracket of
        // the heads and the button (their vertices touch the backs SignalClamp from the axis), a domed cap. Pivot at the pole.
        public static PolyMesh SignalPost()
        {
            var b = NewBuilder();
            const int sides = 8;
            byte paint = VoxelPalette.MetalDark;
            PolyShapes.Lathe(b, 0, 0, new[] { (0f, 0.17f), (0.05f, 0.17f), (0.08f, 0.13f), (0.3f, 0.085f), (0.36f, PoleRadius) }, sides, TrimPart, paint);
            PolyShapes.Lathe(b, 0, 0, new[] { (0.36f, PoleRadius), (SignalBandBottom, PoleRadius) }, sides, TrimPart, paint);
            PolyShapes.Lathe(b, 0, 0, new[] { (SignalBandBottom, PoleRadius), (SignalBandTop, PoleRadius) }, sides, TrimPart, VoxelPalette.CarYellow);
            PolyShapes.Lathe(b, 0, 0, new[] { (SignalBandTop, PoleRadius), (SignalHeight - 0.1f, PoleRadius) }, sides, TrimPart, paint);
            PolyShapes.Lathe(b, 0, 0, new[] { (SignalHeight - 0.1f, PoleRadius), (SignalHeight - 0.1f, 0.09f), (SignalHeight - 0.05f, 0.09f),
                (SignalHeight - 0.015f, 0.06f), (SignalHeight, 0) }, sides, TrimPart, paint);
            var clamps = new List<float>();
            foreach (float y in CarBrackets) clamps.Add(CarSignalY + y);
            foreach (float y in WalkBrackets) clamps.Add(WalkSignalY + y);
            foreach (float y in ButtonBrackets) clamps.Add(ButtonY + y);
            foreach (float y in clamps)
                PolyShapes.Lathe(b, 0, 0, new[] { (y - 0.035f, PoleRadius), (y - 0.035f, SignalClamp), (y + 0.035f, SignalClamp), (y + 0.035f, PoleRadius) },
                    sides, TrimPart, VoxelPalette.Metal);
            return b.Build(BuildingModels.PaletteUv);
        }

        // Car signal head 0.4 × 1.2 m: a dark housing with rounded edges, red over amber over green round lenses under
        // hoods, two brackets back to the post. lit: −1 none, 0 red, 1 amber, 2 green.
        public static PolyMesh CarSignal(int lit)
        {
            if (lit < -1 || lit > 2) throw new ArgumentOutOfRangeException(nameof(lit));
            var b = HangingBuilder();
            const float front = -0.08f, back = 0.13f;
            b.Box(new V3(-0.17f, 0, front), new V3(0.17f, 1.2f, back), 0.04f, TrimPart, VoxelPalette.Ink);
            for (int i = 0; i < 3; i++)
            {
                float y = CarLensTop - CarLensStep * i;
                Lens(b, 0, y, front, CarLensRadius, 12, lit == i, CarLit[i], CarDark[i]);
                Hood(b, 0, y, front, 0.115f, 0.13f, 0.12f, 0.035f);
            }
            Brackets(b, CarBrackets, back, CarSignalBack);
            return b.Build(BuildingModels.PaletteUv);
        }

        // Walk signal head 0.5 × 1.1 m: a dark housing with two dark windows under visors, the standing red figure over
        // the walking green one. lit: −1 none, 0 red, 1 green.
        public static PolyMesh WalkSignal(int lit)
        {
            if (lit < -1 || lit > 1) throw new ArgumentOutOfRangeException(nameof(lit));
            var b = HangingBuilder();
            const float front = -0.06f, back = 0.1f, window = front - 0.008f, figure = window - 0.006f;
            b.Box(new V3(-0.22f, 0, front), new V3(0.22f, 1.1f, back), 0.035f, TrimPart, VoxelPalette.Ink);
            for (int i = 0; i < 2; i++)
            {
                float y = i == 0 ? WalkStandY : WalkGoY;
                b.Box(new V3(-0.16f, y - 0.21f, window), new V3(0.16f, y + 0.21f, front + 0.01f), 0.006f, TrimPart, VoxelPalette.CarBlack);
                Visor(b, y + 0.215f, front, -WalkSignalBack, y - 0.12f);
                bool on = lit == i;
                int part = on ? GlowPart : TrimPart;
                if (i == 0) StandingFigure(b, y, figure, part, on ? VoxelPalette.SignalRed : VoxelPalette.BrickDark);
                else WalkingFigure(b, y, figure, part, on ? VoxelPalette.SignalGreen : VoxelPalette.PineDark);
            }
            Brackets(b, WalkBrackets, back, WalkSignalBack);
            return b.Build(BuildingModels.PaletteUv);
        }

        // Push-button box 0.25 × 0.4 m: yellow with rounded edges, a pale button in a dark ring and the «wait» lamp over it,
        // lit while a walker waits.
        public static PolyMesh CrossingButton(bool waiting)
        {
            var b = HangingBuilder();
            const float front = -0.05f;
            b.Box(new V3(-0.11f, 0, front), new V3(0.11f, 0.4f, ButtonBack), 0.025f, TrimPart, VoxelPalette.CarYellow);
            FrontDisc(b, 0, 0.14f, front - 0.005f, 0.058f, 12, 0, TrimPart, VoxelPalette.CarBlack);
            FrontDisc(b, 0, 0.14f, front - 0.01f, 0.034f, 12, 0.015f, TrimPart, VoxelPalette.MetalLight);
            FrontDisc(b, 0, ButtonLampY, front - 0.005f, 0.05f, 10, 0, TrimPart, VoxelPalette.CarBlack);
            Lens(b, 0, ButtonLampY, front - 0.005f, 0.036f, 10, waiting, VoxelPalette.SignalAmber, VoxelPalette.WoodDark);
            return b.Build(BuildingModels.PaletteUv);
        }

        // A lens facing −Z: a shallow dome over the face at z, on the glow part when lit.
        private static void Lens(PolyBuilder b, float cx, float cy, float z, float r, int sides, bool lit, byte on, byte off)
            => FrontDisc(b, cx, cy, z - 0.004f, r, sides, 0.016f, lit ? GlowPart : TrimPart, lit ? on : off);
        // Round face towards −Z at z, domed `dome` further out at its middle; its own smoothing group.
        private static void FrontDisc(PolyBuilder b, float cx, float cy, float z, float r, int sides, float dome, int part, byte color)
        {
            int group = b.Group; b.Group = b.NextGroup();
            V3 P(float radius, int k, float depth)
            {
                double a = 2 * Math.PI * k / sides;
                return new V3(cx + radius * (float)Math.Cos(a), cy + radius * (float)Math.Sin(a), z - depth);
            }
            var centre = new V3(cx, cy, z - dome);
            for (int k = 0; k < sides; k++)
            {
                if (dome > 0)
                {
                    V3 a0 = P(r, k, 0), a1 = P(r, k + 1, 0), m1 = P(0.6f * r, k + 1, 0.75f * dome), m0 = P(0.6f * r, k, 0.75f * dome);
                    b.Quad(a0, a1, m1, m0, (a0 + a1 + m1 + m0) * 0.25f - new V3(cx, cy, z + r), part, color);
                    b.Triangle(centre, m0, m1, new V3(0, 0, -1), part, color);
                }
                else b.Triangle(centre, P(r, k, 0), P(r, k + 1, 0), new V3(0, 0, -1), part, color);
            }
            b.Group = group;
        }
        // Hood over a lens on the face at z: a bent strip from just below one side over the top to the other, `top` deep
        // over the lens and `side` deep at its ends.
        private static void Hood(PolyBuilder b, float cx, float cy, float z, float rIn, float rOut, float top, float side)
        {
            const int n = 10; const float spill = 0.25f;
            int group = b.Group; b.Group = b.NextGroup();
            double a0 = -spill, a1 = Math.PI + spill;
            V3 At(float r, double a, float depth) => new V3(cx + r * (float)Math.Cos(a), cy + r * (float)Math.Sin(a), z - depth);
            float Depth(double a) => side + (top - side) * (float)Math.Max(0, Math.Sin(a));
            for (int k = 0; k < n; k++)
            {
                double p = a0 + (a1 - a0) * k / n, q = a0 + (a1 - a0) * (k + 1) / n, m = (p + q) / 2;
                float dp = Depth(p), dq = Depth(q);
                var radial = new V3((float)Math.Cos(m), (float)Math.Sin(m), 0);
                b.Quad(At(rOut, p, 0), At(rOut, q, 0), At(rOut, q, dq), At(rOut, p, dp), radial, TrimPart, VoxelPalette.Ink);
                b.Quad(At(rIn, p, 0), At(rIn, q, 0), At(rIn, q, dq), At(rIn, p, dp), radial * -1, TrimPart, VoxelPalette.Ink, 0.8f);
                b.Quad(At(rIn, p, dp), At(rIn, q, dq), At(rOut, q, dq), At(rOut, p, dp), new V3(0, 0, -1), TrimPart, VoxelPalette.Ink);
            }
            foreach (var (a, sign) in new[] { (a0, -1f), (a1, 1f) })
            {
                float d = Depth(a);
                var along = new V3(-(float)Math.Sin(a), (float)Math.Cos(a), 0) * sign;
                b.Quad(At(rIn, a, 0), At(rOut, a, 0), At(rOut, a, d), At(rIn, a, d), along, TrimPart, VoxelPalette.Ink);
            }
            b.Group = group;
        }
        // Visor over a window of the walk head: a plate from the face at z out to `reach` at height y, cheeks tapering
        // down the window's sides to `foot`.
        private static void Visor(PolyBuilder b, float y, float z, float reach, float foot)
        {
            const float half = 0.17f, plate = 0.018f, cheek = 0.014f;
            b.Box(new V3(-half - cheek, y, reach), new V3(half + cheek, y + plate, z), 0.004f, TrimPart, VoxelPalette.Ink);
            foreach (float x in new[] { -half - cheek, half })
                PolyShapes.Block(b,
                    new[] { new V3(x, foot, z), new V3(x + cheek, foot, z), new V3(x + cheek, foot, z - 0.015f), new V3(x, foot, z - 0.015f) },
                    new[] { new V3(x, y, z), new V3(x + cheek, y, z), new V3(x + cheek, y, reach), new V3(x, y, reach) }, TrimPart, VoxelPalette.Ink);
        }
        private static void Brackets(PolyBuilder b, float[] heights, float from, float to)
        {
            foreach (float y in heights)
                b.Box(new V3(-0.035f, y - 0.035f, from - 0.01f), new V3(0.035f, y + 0.035f, to), 0, TrimPart, VoxelPalette.MetalDark);
        }

        // The standing figure, arms at its sides, in the window centred at y; flat on the plane z, facing −Z.
        private static void StandingFigure(PolyBuilder b, float y, float z, int part, byte color)
        {
            FrontDisc(b, 0, y + 0.13f, z, 0.04f, 10, 0, part, color);
            Flat(b, z, part, color, (-0.048f, y - 0.03f), (0.048f, y - 0.03f), (0.048f, y + 0.075f), (0.03f, y + 0.085f), (-0.03f, y + 0.085f), (-0.048f, y + 0.075f));
            foreach (float s in new[] { -1f, 1f })
            {
                Flat(b, z, part, color, (s * 0.056f, y - 0.03f), (s * 0.078f, y - 0.03f), (s * 0.078f, y + 0.065f), (s * 0.056f, y + 0.075f));
                Flat(b, z, part, color, (s * 0.006f, y - 0.17f), (s * 0.042f, y - 0.17f), (s * 0.042f, y - 0.03f), (s * 0.006f, y - 0.03f));
            }
        }
        // The walking figure, striding towards +X, in the window centred at y.
        private static void WalkingFigure(PolyBuilder b, float y, float z, int part, byte color)
        {
            FrontDisc(b, 0.02f, y + 0.135f, z, 0.037f, 10, 0, part, color);
            Flat(b, z, part, color, (-0.03f, y - 0.03f), (0.026f, y - 0.03f), (0.046f, y + 0.088f), (-0.01f, y + 0.088f));
            Limb2D(b, z, part, color, (0.032f, y + 0.07f), (0.09f, y - 0.005f), 0.026f);
            Limb2D(b, z, part, color, (-0.002f, y + 0.07f), (-0.072f, y + 0.0f), 0.026f);
            Limb2D(b, z, part, color, (0.012f, y - 0.02f), (0.078f, y - 0.17f), 0.034f);
            Limb2D(b, z, part, color, (-0.014f, y - 0.02f), (-0.072f, y - 0.17f), 0.034f);
        }
        // Convex polygon on the plane z, facing −Z.
        private static void Flat(PolyBuilder b, float z, int part, byte color, params (float x, float y)[] points)
        {
            var list = new List<V3>();
            foreach (var (x, y) in points) list.Add(new V3(x, y, z));
            b.Polygon(list, new V3(0, 0, -1), part, color);
        }
        // Straight stroke from p to q, `width` across, on the plane z.
        private static void Limb2D(PolyBuilder b, float z, int part, byte color, (float x, float y) p, (float x, float y) q, float width)
        {
            float dx = q.x - p.x, dy = q.y - p.y, len = (float)Math.Sqrt(dx * dx + dy * dy), nx = -dy / len * width / 2, ny = dx / len * width / 2;
            Flat(b, z, part, color, (p.x - nx, p.y - ny), (q.x - nx, q.y - ny), (q.x + nx, q.y + ny), (p.x + nx, p.y + ny));
        }
    }
}
