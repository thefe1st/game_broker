using System;
using System.Collections.Generic;

namespace Broker.Core
{
    // The head of a low-poly person on its neck joint: a rounded skull, narrower at the jaw and flatter in front, with a
    // nose and ears; hair as a shell over the skull down to a hairline, fuller on top (short, long to the shoulders, messy,
    // a bun, grey temples; the staff's with a fringe, tufts or a parting of their own); a cap, a flat cap and a guard's
    // peaked cap; glasses. The face — small dark eyes, brows and a mouth — is a mesh of its own (Face) lying on the skull's
    // front, so the scene can switch its mood.
    public static partial class PeopleModels
    {
        // The skull: an ellipsoid round (0, SkullY, SkullZ) above the neck joint with radii SkullRx, SkullRy, SkullRz; below
        // the middle it narrows to JawTaper at the chin, in front it is flattened to FrontFlat.
        public const float SkullY = 0.155f, SkullZ = 0, SkullRx = 0.1f, SkullRy = 0.124f, SkullRz = 0.11f;
        private const float JawTaper = 0.8f, FrontFlat = 0.93f;
        // The features: the eyes EyeX either side of the middle at EyeY, the nose at NoseY, the mouth at MouthY.
        public const float EyeX = 0.035f, EyeY = SkullY + 0.004f, NoseY = SkullY - 0.026f, MouthY = SkullY - 0.07f;
        private static readonly V3 SkullCentre = new V3(0, SkullY, SkullZ);

        private static float Taper(double polar)
            => polar <= Math.PI / 2 ? 1 : 1 - (1 - JawTaper) * (float)Math.Pow((polar - Math.PI / 2) / (Math.PI / 2), 1.5);
        // A point of the skull grown by `grow`: `polar` from the crown (0) to the chin (π), `azimuth` round from +X, the face
        // at −π/2 (−Z).
        private static V3 SkullPoint(double polar, double azimuth, float grow = 0)
        {
            float s = (float)Math.Sin(polar) * Taper(polar), c = (float)Math.Cos(polar);
            float cx = (float)Math.Cos(azimuth), sz = (float)Math.Sin(azimuth);
            float rz = SkullRz * (sz < 0 ? FrontFlat : 1) + grow;
            return new V3((SkullRx + grow) * s * cx, SkullY + (SkullRy + grow) * c, SkullZ + rz * s * sz);
        }
        // The skull's front surface at (x, y) on the head's pivot: its z, negative (the face looks −Z).
        public static float FaceZ(float x, float y)
        {
            double polar = Math.Acos(Math.Max(-1, Math.Min(1, (y - SkullY) / SkullRy)));
            float s = (float)Math.Sin(polar) * Taper(polar), a = SkullRx * s, d = SkullRz * FrontFlat * s;
            if (a < 1e-5f) return SkullZ;
            float u = Math.Min(1, Math.Abs(x) / a);
            return SkullZ - d * (float)Math.Sqrt(1 - u * u);
        }
        // Half the skull's width at height y and depth z (front or back).
        private static float SkullX(float y, float z)
        {
            double polar = Math.Acos(Math.Max(-1, Math.Min(1, (y - SkullY) / SkullRy)));
            float s = (float)Math.Sin(polar) * Taper(polar), d = (z < SkullZ ? SkullRz * FrontFlat : SkullRz) * s;
            if (d < 1e-5f) return 0;
            float u = Math.Min(1, Math.Abs(z - SkullZ) / d);
            return SkullRx * s * (float)Math.Sqrt(1 - u * u);
        }

        // ---------- Head ----------
        private static PolyMesh HeadMesh(Outfit o)
        {
            var b = NewBuilder();
            int group = b.Group; b.Group = b.NextGroup();
            int rings = o.SkullRings, sides = o.SkullSides;
            for (int i = 0; i < rings; i++)
                for (int k = 0; k < sides; k++)
                {
                    double p0 = Math.PI * i / rings, p1 = Math.PI * (i + 1) / rings;
                    double a0 = 2 * Math.PI * k / sides, a1 = 2 * Math.PI * (k + 1) / sides;
                    V3 q00 = SkullPoint(p0, a0), q01 = SkullPoint(p0, a1), q11 = SkullPoint(p1, a1), q10 = SkullPoint(p1, a0);
                    b.Quad(q00, q01, q11, q10, (q00 + q01 + q11 + q10) * 0.25f - SkullCentre, TrimPart, o.Skin);
                }
            b.Group = group;
            PolyShapes.Limb(b, new V3(0, -0.05f, 0.012f), new V3(0, 0.07f, 0.008f), 0.059f, 0.054f, o.LimbSides, TrimPart, o.Skin, 0.86f);
            PolyShapes.Blob(b, new V3(0, NoseY, FaceZ(0, NoseY) + 0.004f), 0.012f, 0.017f, 0.012f, o.Round(8), o.Round(5), TrimPart, o.Skin, 0.97f);
            foreach (int sx in new[] { -1, 1 })
                PolyShapes.Blob(b, new V3(sx * (SkullRx - 0.004f), SkullY - 0.014f, SkullZ + 0.01f), 0.014f, 0.027f, 0.019f, o.Round(6), o.Round(4), TrimPart, o.Skin, 0.9f);
            HairOn(b, o);
            HatOn(b, o);
            if (o.Glasses) Glasses(b);
            return Done(b);
        }

        // ---------- Hair ----------
        // Hairline angles from the crown (radians) in front, at the sides and behind, blended round the head.
        private static double Hairline(double azimuth, double front, double side, double back)
        {
            double s = Math.Sin(azimuth);
            return side + (s < 0 ? front - side : back - side) * s * s;
        }
        // A bump of `height` round azimuth `at`, `width` wide (a fringe over the forehead, a parting's groove).
        private static double Bump(double azimuth, double at, double width, double height)
        {
            double d = Math.IEEERemainder(azimuth - at, 2 * Math.PI) / width;
            return height * Math.Exp(-d * d);
        }
        // The azimuth of the face (−Z).
        private const double Ahead = -Math.PI / 2;
        private static void HairOn(PolyBuilder b, Outfit o)
        {
            byte hair = o.HairColor; int sides = o.SkullSides, rows = o.HairRows;
            // Under a hat the hair lies flat; bare, it rises over the crown.
            float crown = o.Hat == Hat.None ? 1 : 0;
            switch (o.Hair)
            {
                case Hair.Short:
                    Shell(b, sides, rows, a => Hairline(a, 0.87, 1.4, 2.06), 0.011f, 0.013f * crown, 0, 0, (i, a) => hair);
                    break;
                case Hair.Messy:
                    Shell(b, sides, rows, a => Hairline(a, 1.0, 1.36, 2.02), 0.014f, 0.012f * crown, o.Fine ? 0.3f : 0.45f, 11, (i, a) => hair);
                    if (o.Fine) Tufts(b, o, hair);
                    break;
                case Hair.Temples:
                {
                    // Neat and short, parted on one side and fuller on the other, grey at the temples.
                    double part = Ahead - 0.52;
                    Func<double, double, float> parted = (t, a) => o.Fine
                        ? (float)(1 - (t > 0.12 && t < 0.97 ? Bump(a, part, 0.1, 0.4) : 0) + 0.18 * Math.Min(1, t / 0.4) * Bump(a, Ahead + 0.35, 0.7, 1)) : 1;
                    Shell(b, sides, rows, a => Hairline(a, 0.84, 1.4, 2.06) + (o.Fine ? Bump(a, part, 0.08, -0.035) : 0), 0.011f, 0.011f * crown, 0, 0,
                        (i, a) => Grey(i, rows, a) ? VoxelPalette.Stone : hair, growth: parted);
                    break;
                }
                case Hair.Bun:
                    Shell(b, sides, rows, a => Hairline(a, 0.84, 1.38, 2.1), 0.01f, 0.006f * crown, 0, 0, (i, a) => hair);
                    PolyShapes.Blob(b, new V3(0, SkullY + 0.1f, SkullZ + 0.072f), 0.042f, 0.038f, 0.04f, o.Round(8), o.Round(6), TrimPart, hair);
                    break;
                case Hair.Long:
                    if (o.Fine) { LongHair(b, o, hair); break; }
                    // To the shoulders behind, framing the face at the sides.
                    Shell(b, sides, rows, a => Hairline(a, 0.87, 1.42, 2.3), 0.012f, 0.012f * crown, 0, 0, (i, a) => hair);
                    PolyShapes.Lump(b, new V3(0, SkullY - 0.105f, SkullZ + 0.05f), 0.086f, 0.112f, 0.056f, 10, 6, 0.04f, 3, TrimPart, hair);
                    foreach (int sx in new[] { -1, 1 })
                        PolyShapes.Blob(b, new V3(sx * (SkullRx - 0.002f), SkullY - 0.042f, SkullZ + 0.014f), 0.023f, 0.086f, 0.054f, 8, 6, TrimPart, hair);
                    break;
            }
        }
        // Grey temples: the lowest row at the sides, just over the ears.
        private static bool Grey(int row, int rows, double azimuth) => row == rows - 1 && Math.Abs(Math.Cos(azimuth)) > 0.84;
        // Lena's hair: parted on one side, the fringe swept across the forehead above the brows, full over the crown, down
        // to the shoulders behind and in locks either side of the face.
        private static void LongHair(PolyBuilder b, Outfit o, byte hair)
        {
            double part = Ahead - 0.45, fringe = Ahead + 0.32;
            Func<double, double, float> volume = (t, a) =>
                (float)(1 - (t > 0.12 && t < 0.97 ? Bump(a, part, 0.09, 0.5) : 0) + 0.22 * Math.Min(1, t / 0.4) * Bump(a, fringe, 0.6, 1));
            Shell(b, o.SkullSides, o.HairRows, a => Hairline(a, 0.9, 1.5, 2.3) + Bump(a, fringe, 0.38, 0.22) + Bump(a, part, 0.07, -0.05), 0.013f, 0.014f, 0, 0,
                (i, a) => hair, growth: volume);
            PolyShapes.Lump(b, new V3(0, SkullY - 0.1f, SkullZ + 0.052f), 0.092f, 0.114f, 0.058f, 16, 10, 0.03f, 3, TrimPart, hair);
            foreach (int sx in new[] { -1, 1 })
                PolyShapes.Lump(b, new V3(sx * (SkullRx - 0.003f), SkullY - 0.05f, SkullZ + 0.004f), 0.024f, 0.096f, 0.058f, 12, 10, 0.03f, 5 + sx, TrimPart, hair);
        }
        // Max's tousled hair: flat locks lying along the head from the crown towards their tips, which stand a little off
        // it — forward over the fringe, back over the crown, down at the sides.
        private static void Tufts(PolyBuilder b, Outfit o, byte hair)
        {
            // Root from the crown (radians) and round from the face, length.
            var locks = new (double polar, double azimuth, float length)[]
            {
                (0.62, Ahead, 0.05f), (0.6, Ahead - 0.42, 0.048f), (0.6, Ahead + 0.44, 0.047f), (0.66, Ahead - 0.85, 0.042f), (0.66, Ahead + 0.86, 0.043f),
                (0.25, Ahead - 0.25, 0.055f), (0.27, Ahead + 0.5, 0.05f), (0.3, Ahead - 1.1, 0.05f),
                (0.8, Ahead - 1.45, 0.04f), (0.8, Ahead + 1.45, 0.04f), (0.45, Math.PI / 2 - 0.4, 0.05f), (0.45, Math.PI / 2 + 0.45, 0.05f),
            };
            foreach (var (polar, azimuth, length) in locks)
            {
                var root = SkullPoint(polar, azimuth, 0.02f);
                var along = (SkullPoint(polar + 0.05, azimuth, 0.02f) - root).Normalized;
                var outward = (root - SkullCentre).Normalized;
                Lock(b, root - along * 0.012f - outward * 0.004f, (along + outward * 0.42f).Normalized, outward, length, 0.036f, 0.016f, hair);
            }
        }
        // A lock of hair from `root` along `dir`: an ellipse `width` across and `thick` off the head (towards `up`), tapering
        // to a point.
        private static void Lock(PolyBuilder b, V3 root, V3 dir, V3 up, float length, float width, float thick, byte color)
        {
            var across = V3.Cross(dir, up).Normalized; var off = V3.Cross(across, dir).Normalized;
            const int sides = 6;
            var rings = new (float t, float size)[] { (0, 1), (0.4f, 0.88f), (0.75f, 0.52f) };
            int group = b.Group; b.Group = b.NextGroup();
            V3 P(int r, int k)
            {
                double a = 2 * Math.PI * k / sides; var (t, size) = rings[r];
                return root + dir * (length * t) + across * (0.5f * width * size * (float)Math.Cos(a)) + off * (0.5f * thick * size * (float)Math.Sin(a));
            }
            for (int r = 0; r + 1 < rings.Length; r++)
            {
                var axis = root + dir * (length * (rings[r].t + rings[r + 1].t) / 2);
                for (int k = 0; k < sides; k++)
                {
                    V3 a0 = P(r, k), a1 = P(r, k + 1), b1 = P(r + 1, k + 1), b0 = P(r + 1, k);
                    b.Quad(a0, a1, b1, b0, (a0 + a1 + b1 + b0) * 0.25f - axis, TrimPart, color);
                }
            }
            var tip = root + dir * length; var last = rings.Length - 1;
            for (int k = 0; k < sides; k++)
            {
                V3 a0 = P(last, k), a1 = P(last, k + 1);
                b.Triangle(a0, a1, tip, (a0 + a1 + tip) * (1f / 3) - (root + dir * (length * 0.85f)), TrimPart, color);
            }
            var cap = new List<V3>(); for (int k = 0; k < sides; k++) cap.Add(P(0, k));
            b.Polygon(cap, dir * -1, TrimPart, color, 0.9f);
            b.Group = group;
        }
        // A shell over the skull from the crown to the hairline (radians from the crown, by azimuth): `grow` off the skull,
        // `crown` more on top (the hair's volume), times `growth` by (row 0–1 from the crown, azimuth), roughened by `bumps`
        // from the seed; `rows` rows and `sides` round. Its rim runs down to the skull, so the edge has a thickness. Colour
        // per row (from the crown) and azimuth.
        private static void Shell(PolyBuilder b, int sides, int rows, Func<double, double> hairline, float grow, float crown, float bumps, int seed,
            Func<int, double, byte> color, float shade = 1, Func<double, double, float> growth = null)
        {
            int group = b.Group; b.Group = b.NextGroup();
            var edge = new double[sides + 1];
            for (int k = 0; k <= sides; k++)
            {
                double rough = bumps > 0 ? 1 + 0.06 * (2 * VoxelNoise.Value(k % sides, 1, 7, seed) - 1) : 1;
                edge[k] = hairline(2 * Math.PI * k / sides) * rough;
            }
            double Az(int k) => 2 * Math.PI * k / sides;
            V3 P(int i, int k)
            {
                float t = (float)i / rows, g = (grow + crown * (1 - t * t)) * (i == rows ? 0.7f : 1);
                if (bumps > 0 && i > 0 && i < rows) g *= 1 + bumps * (2 * VoxelNoise.Value(i, k % sides, 5, seed) - 1);
                if (growth != null) g *= growth(t, Az(k));
                return SkullPoint(edge[k] * i / rows, Az(k), g);
            }
            for (int i = 0; i < rows; i++)
                for (int k = 0; k < sides; k++)
                {
                    V3 p00 = P(i, k), p01 = P(i, k + 1), p11 = P(i + 1, k + 1), p10 = P(i + 1, k);
                    b.Quad(p00, p01, p11, p10, (p00 + p01 + p11 + p10) * 0.25f - SkullCentre, TrimPart, color(i, Az(k) + Math.PI / sides), shade);
                }
            for (int k = 0; k < sides; k++)
            {
                V3 e0 = P(rows, k), e1 = P(rows, k + 1);
                V3 s0 = SkullPoint(edge[k] + 0.04, Az(k)), s1 = SkullPoint(edge[k + 1] + 0.04, Az(k + 1));
                b.Quad(e0, e1, s1, s0, (e0 + e1 + s1 + s0) * 0.25f - SkullCentre, TrimPart, color(rows - 1, Az(k) + Math.PI / sides), shade * 0.85f);
            }
            b.Group = group;
        }

        // ---------- Hats ----------
        private static void HatOn(PolyBuilder b, Outfit o)
        {
            switch (o.Hat)
            {
                case Hat.Cap:
                {
                    // A baseball cap: a dome over the hair, a curved peak, a button on top.
                    const double front = 1.06, side = 1.3, back = 1.62; const float grow = 0.022f;
                    Shell(b, o.SkullSides, o.HairRows, a => Hairline(a, front, side, back), grow, 0, 0, 0, (i, a) => o.HatColor);
                    var inner = new List<V3>();
                    for (int j = 0; j <= 8; j++)
                    {
                        double a = -Math.PI / 2 + 0.95 * (j / 4.0 - 1);
                        inner.Add(SkullPoint(Hairline(a, front, side, back), a, grow * 0.7f));
                    }
                    Brim(b, inner, 0.072f, 0.014f, 0.006f, o.HatColor);
                    PolyShapes.Blob(b, new V3(0, SkullY + SkullRy + grow + 0.002f, SkullZ), 0.012f, 0.006f, 0.012f, o.Round(6), o.Round(4), TrimPart, o.HatColor, 0.9f);
                    break;
                }
                case Hat.FlatCap:
                {
                    // A flat cap: a flattened crown over the hair, wider than the head and pulled forward, a short peak.
                    PolyShapes.Blob(b, new V3(0, SkullY + 0.094f, SkullZ - 0.012f), SkullRx + 0.022f, 0.054f, SkullRz + 0.03f, o.Round(12), o.Round(6), TrimPart, o.HatColor);
                    var (x, front, _) = HatBand(0.058f, 0.015f);
                    Brim(b, BandFront(SkullY + 0.058f, x, front), 0.058f, 0.012f, 0.007f, o.HatColor);
                    break;
                }
                case Hat.Peaked:
                {
                    // The guard's peaked cap: a black band, a navy crown flaring out over it, a black peak and a brass badge.
                    var (x, front, back) = HatBand(0.05f, 0.017f);
                    var rings = new (float y, float x, float front, float back)[] { (SkullY + 0.05f, x, front, back), (SkullY + 0.088f, x, front, back),
                        (SkullY + 0.118f, x + 0.02f, front + 0.03f, back + 0.014f), (SkullY + 0.136f, x + 0.022f, front + 0.033f, back + 0.016f) };
                    HatLoft(b, o.SkullSides, rings, new[] { VoxelPalette.CarBlack, o.HatColor, o.HatColor }, SkullY + 0.142f, o.HatColor);
                    Brim(b, BandFront(rings[0].y, x, front), 0.062f, 0.018f, 0.006f, VoxelPalette.Ink);
                    float by = SkullY + 0.07f, bz = SkullZ - front - 0.003f;
                    var points = new List<V3>();
                    foreach (var (dx, dy) in new[] { (-0.012f, 0.012f), (0.012f, 0.012f), (0.012f, -0.003f), (0f, -0.015f), (-0.012f, -0.003f) })
                        points.Add(new V3(dx, by + dy, bz));
                    b.Polygon(points, new V3(0, 0, -1), MetalPart, VoxelPalette.Brass);
                    break;
                }
            }
        }
        // A hat's band `h` above the skull's middle: `room` clear of the skull (hair included) at its half-width, in front
        // and behind.
        private static (float x, float front, float back) HatBand(float h, float room)
        {
            float s = (float)Math.Sqrt(Math.Max(0, 1 - h * h / (SkullRy * SkullRy)));
            return (SkullRx * s + room, SkullRz * FrontFlat * s + room, SkullRz * s + room);
        }
        // A hat as rings of half-width x, half-depth `front` ahead of the middle and `back` behind, one colour per band
        // between them, closed on top at `top`.
        private static void HatLoft(PolyBuilder b, int sides, (float y, float x, float front, float back)[] rings, byte[] colors, float top, byte topColor)
        {
            int group = b.Group; b.Group = b.NextGroup();
            V3 P(int r, int k)
            {
                double a = 2 * Math.PI * k / sides; float s = (float)Math.Sin(a);
                return new V3(rings[r].x * (float)Math.Cos(a), rings[r].y, SkullZ + (s < 0 ? rings[r].front : rings[r].back) * s);
            }
            for (int r = 0; r + 1 < rings.Length; r++)
            {
                var axis = new V3(0, (rings[r].y + rings[r + 1].y) / 2, SkullZ);
                for (int k = 0; k < sides; k++)
                {
                    V3 a0 = P(r, k), a1 = P(r, k + 1), b1 = P(r + 1, k + 1), b0 = P(r + 1, k);
                    b.Quad(a0, a1, b1, b0, (a0 + a1 + b1 + b0) * 0.25f - axis, TrimPart, colors[r]);
                }
            }
            var lid = new List<V3>(); int last = rings.Length - 1;
            for (int k = 0; k < sides; k++) lid.Add(P(last, k));
            var centre = new V3(0, top, SkullZ);
            for (int k = 0; k < sides; k++) b.Triangle(centre, lid[k], lid[(k + 1) % sides], new V3(0, 1, 0), TrimPart, topColor, 1.04f);
            // Underneath, the band's inside, so the hat is not hollow seen from below.
            var under = new List<V3>();
            for (int k = 0; k < sides; k++) under.Add(P(0, k));
            b.Polygon(under, new V3(0, -1, 0), TrimPart, VoxelPalette.Ink, 0.6f);
            b.Group = group;
        }
        // The front of a hat's band (half-width x, half-depth `front`) at y, where a peak is attached.
        private static List<V3> BandFront(float y, float x, float front)
        {
            var points = new List<V3>();
            for (int j = 0; j <= 8; j++)
            {
                double a = -Math.PI / 2 + 0.95 * (j / 4.0 - 1);
                points.Add(new V3(x * (float)Math.Cos(a), y, SkullZ + front * (float)Math.Sin(a)));
            }
            return points;
        }
        // A peak from the attached edge `inner` outwards (longest, `length`, in the middle), dropping by `droop` at its tip,
        // `thick` thick.
        private static void Brim(PolyBuilder b, List<V3> inner, float length, float droop, float thick, byte color)
        {
            int group = b.Group; b.Group = b.NextGroup();
            int n = inner.Count;
            var outer = new V3[n];
            for (int j = 0; j < n; j++)
            {
                float u = 2f * j / (n - 1) - 1, l = Math.Max(0.004f, length * (float)Math.Pow(1 - u * u, 0.6));
                var p = inner[j]; var dir = new V3(p.X, 0, p.Z - SkullZ).Normalized;
                outer[j] = p + dir * l + new V3(0, -droop * l / length, 0);
            }
            var down = new V3(0, -thick, 0);
            for (int j = 0; j + 1 < n; j++)
            {
                V3 i0 = inner[j], i1 = inner[j + 1], o0 = outer[j], o1 = outer[j + 1];
                var outward = new V3((o0.X + o1.X) / 2, 0, (o0.Z + o1.Z) / 2 - SkullZ);
                b.Quad(i0, i1, o1, o0, new V3(0, 1, 0), TrimPart, color);
                b.Quad(i0 + down, i1 + down, o1 + down, o0 + down, new V3(0, -1, 0), TrimPart, color, 0.62f);
                b.Quad(o0, o1, o1 + down, o0 + down, outward, TrimPart, color, 0.85f);
            }
            foreach (int j in new[] { 0, n - 1 })
                b.Quad(inner[j], outer[j], outer[j] + down, inner[j] + down, new V3(inner[j].X, 0, 0), TrimPart, color, 0.85f);
            b.Group = group;
        }

        // ---------- Glasses ----------
        // Round black frames in front of the eyes, a bridge, and arms back to the ears along the side of the head.
        private static void Glasses(PolyBuilder b)
        {
            const float rx = 0.02f, ry = 0.0155f, off = 0.012f, wire = 0.0026f;
            foreach (int sx in new[] { -1, 1 })
            {
                var loop = new List<V3>();
                for (int k = 0; k <= 12; k++)
                {
                    double a = 2 * Math.PI * k / 12;
                    float x = sx * EyeX + rx * (float)Math.Cos(a), y = EyeY + ry * (float)Math.Sin(a);
                    loop.Add(new V3(x, y, FaceZ(x, y) - off));
                }
                PolyShapes.Sweep(b, loop, wire, wire, 4, new V3(0, 0, 1), TrimPart, VoxelPalette.Ink);
                float ex = sx * (EyeX + rx), ey = EyeY + 0.004f;
                var arm = new List<V3> { new V3(ex, ey, FaceZ(ex, ey) - off) };
                foreach (float z in new[] { -0.07f, -0.035f, 0.004f })
                    arm.Add(new V3(sx * (SkullX(ey, z) + 0.004f), ey - 0.002f * (z + 0.07f) / 0.074f, z));
                PolyShapes.Sweep(b, arm, wire, wire, 4, new V3(0, 1, 0), TrimPart, VoxelPalette.Ink);
            }
            float bx = EyeX - rx, bridgeY = EyeY + 0.006f;
            PolyShapes.Limb(b, new V3(-bx, bridgeY, FaceZ(bx, bridgeY) - off), new V3(bx, bridgeY, FaceZ(bx, bridgeY) - off), wire, wire, 4, TrimPart, VoxelPalette.Ink);
        }

        // ---------- Face ----------
        // The face on the skull's front for a mood: small dark eyes, brows in the given colour and a mouth (the author's
        // choice of the prototype's variants). On the head's pivot, a few millimetres off the skull, in palette trim.
        public static PolyMesh Face(FaceMood mood, byte brows)
        {
            var b = NewBuilder();
            foreach (int sx in new[] { -1, 1 })
            {
                Spot(b, sx * EyeX, EyeY, 0.0062f, 0.0085f, 0.0016f, VoxelPalette.Ink);
                Brow(b, sx, mood, brows);
            }
            Mouth(b, mood);
            return Done(b);
        }
        private static V3 OnFace(float x, float y, float lift) => new V3(x, y, FaceZ(x, y) - lift);
        // An ellipse on the face round (cx, cy).
        private static void Spot(PolyBuilder b, float cx, float cy, float rx, float ry, float lift, byte color, int sides = 10)
        {
            var points = new List<V3>();
            for (int k = 0; k < sides; k++)
            {
                double a = 2 * Math.PI * k / sides;
                points.Add(OnFace(cx + rx * (float)Math.Cos(a), cy + ry * (float)Math.Sin(a), lift));
            }
            b.Polygon(points, OnFace(cx, cy, lift) - SkullCentre, TrimPart, color);
        }
        // A brow from the inner end outwards, tapering: level and arched when calm, raised for a smile, its inner end lifted
        // when worried.
        private static void Brow(PolyBuilder b, int sx, FaceMood mood, byte color)
        {
            float inner, outer, arch;
            switch (mood)
            {
                case FaceMood.Smile: inner = EyeY + 0.0305f; outer = EyeY + 0.0295f; arch = 0.0055f; break;
                case FaceMood.Worry: inner = EyeY + 0.038f; outer = EyeY + 0.0235f; arch = 0.0015f; break;
                default: inner = EyeY + 0.027f; outer = EyeY + 0.0265f; arch = 0.004f; break;
            }
            const int n = 5;
            var upper = new List<V3>(); var lower = new List<V3>();
            for (int i = 0; i <= n; i++)
            {
                float t = (float)i / n, x = sx * (0.016f + 0.036f * t), y = inner + (outer - inner) * t + arch * 4 * t * (1 - t), half = 0.0032f - 0.0014f * t;
                upper.Add(OnFace(x, y + half, 0.0018f)); lower.Add(OnFace(x, y - half, 0.0018f));
            }
            for (int i = 0; i < n; i++)
                b.Quad(lower[i], lower[i + 1], upper[i + 1], upper[i], (upper[i] + lower[i + 1]) * 0.5f - SkullCentre, TrimPart, color);
        }
        // The mouth as a bow along MouthY, thicker in the middle: the corners a little up when calm, well up and wider for a
        // smile, down when worried.
        private static void Mouth(PolyBuilder b, FaceMood mood)
        {
            float half, rise, thick;
            switch (mood)
            {
                case FaceMood.Smile: half = 0.023f; rise = 0.009f; thick = 0.0042f; break;
                case FaceMood.Worry: half = 0.015f; rise = -0.006f; thick = 0.0026f; break;
                default: half = 0.017f; rise = 0.0025f; thick = 0.0024f; break;
            }
            const int n = 8;
            var upper = new List<V3>(); var lower = new List<V3>();
            for (int i = 0; i <= n; i++)
            {
                float u = 2f * i / n - 1, x = half * u, y = MouthY + rise * u * u, t = thick * (1 - 0.55f * u * u);
                // A smile opens below the line; the other moods keep it centred.
                float up = mood == FaceMood.Smile ? t * 0.35f : t * 0.5f, down = mood == FaceMood.Smile ? t * 1.65f : t * 0.5f;
                upper.Add(OnFace(x, y + up, 0.0018f)); lower.Add(OnFace(x, y - down, 0.0018f));
            }
            for (int i = 0; i < n; i++)
                b.Quad(lower[i], lower[i + 1], upper[i + 1], upper[i], (upper[i] + lower[i + 1]) * 0.5f - SkullCentre, TrimPart, VoxelPalette.BrickDark);
        }
    }
}
