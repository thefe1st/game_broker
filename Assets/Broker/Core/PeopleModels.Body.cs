using System;
using System.Collections.Generic;

namespace Broker.Core
{
    // The body of a low-poly person: a torso lofted through rounded cross-sections from the crotch to the neck, wide enough
    // at the shoulders to round over the tops of the arms; its clothes as bands round it and panels on its front and back,
    // a shirt's collar round the neck; arms and legs as tapered tubes bent a little at the elbow and the knee, mitten
    // hands (the staff's with fingers), shoes on a sole; bent legs for sitting.
    public static partial class PeopleModels
    {
        // Cross-sections above the hip joint: superellipses (exponent TorsoRound) round (0, y, Z), half-width A, half-depth B.
        private readonly struct Ring
        {
            public readonly float Y, A, B, Z;
            public Ring(float y, float a, float b, float z) { Y = y; A = a; B = b; Z = z; }
        }
        // From the crotch up: the hips, the waist, the chest, the shoulders — out past the arms' joints and over the tops of
        // the arms, so the arm hangs from a rounded shoulder — and the slope to the neck.
        private static readonly Ring[] TorsoProfile =
        {
            new Ring(-0.10f, 0.150f, 0.104f, 0.006f), new Ring(-0.04f, 0.170f, 0.112f, 0.004f), new Ring(0.05f, 0.176f, 0.113f, 0.002f),
            new Ring(0.17f, 0.164f, 0.107f, 0), new Ring(0.29f, 0.171f, 0.114f, -0.004f), new Ring(0.39f, 0.180f, 0.119f, -0.007f),
            new Ring(0.45f, 0.197f, 0.116f, -0.005f), new Ring(0.49f, 0.211f, 0.108f, -0.001f), new Ring(0.52f, 0.203f, 0.098f, 0.003f),
            new Ring(0.548f, 0.171f, 0.088f, 0.006f), new Ring(0.575f, 0.117f, 0.074f, 0.009f), new Ring(NeckRise, 0.061f, 0.059f, 0.010f),
        };
        private const float TorsoRound = 2.6f, Crotch = -0.10f;

        // The torso of one outfit: the profile, narrower above the waist for a narrow figure, a skirt or a coat's skirt below;
        // Sides round every section. The staff's torso has a section between every two, on a smooth curve through them.
        private sealed class TorsoShape
        {
            private readonly List<Ring> rings = new List<Ring>();
            public int Sides { get; }
            public TorsoShape(Outfit o)
            {
                Sides = o.TorsoSides;
                float skirt = o.SkirtTo > 0 ? -o.SkirtTo : o.Kind == Kind.Coat ? -o.CoatTo : 0;
                if (skirt < 0)
                {
                    // Hanging from the hips, a little wider towards the hem.
                    float span = Crotch - skirt;
                    rings.Add(new Ring(skirt, 0.192f, 0.136f, 0.006f));
                    if (span > 0.15f) rings.Add(new Ring(skirt + span * 0.5f, 0.186f, 0.13f, 0.006f));
                }
                foreach (var r in TorsoProfile)
                {
                    if (skirt < 0 && r.Y <= Crotch) { rings.Add(new Ring(r.Y, 0.178f, 0.122f, 0.006f)); continue; }
                    float k = o.Narrow ? (r.Y > 0.2f ? 0.93f : r.Y > 0.1f ? 0.95f : 1.02f) : 1;
                    rings.Add(new Ring(r.Y, r.A * k, r.B * (o.Narrow && r.Y > 0.2f ? 0.97f : 1), r.Z));
                }
                if (o.Fine) Smooth();
            }
            // Catmull-Rom midpoints of every two sections (heights kept in order).
            private void Smooth()
            {
                var fine = new List<Ring> { rings[0] };
                float Mid(float a, float b, float c, float d) => (-a + 9 * b + 9 * c - d) / 16;
                for (int i = 0; i + 1 < rings.Count; i++)
                {
                    Ring p0 = rings[Math.Max(0, i - 1)], p1 = rings[i], p2 = rings[i + 1], p3 = rings[Math.Min(rings.Count - 1, i + 2)];
                    float span = p2.Y - p1.Y, y = Math.Max(p1.Y + 0.3f * span, Math.Min(p2.Y - 0.3f * span, Mid(p0.Y, p1.Y, p2.Y, p3.Y)));
                    fine.Add(new Ring(y, Mid(p0.A, p1.A, p2.A, p3.A), Mid(p0.B, p1.B, p2.B, p3.B), Mid(p0.Z, p1.Z, p2.Z, p3.Z)));
                    fine.Add(p2);
                }
                rings.Clear(); rings.AddRange(fine);
            }
            public float Bottom => rings[0].Y;
            public bool Open => rings[0].Y < Crotch - 1e-4f;
            public IReadOnlyList<Ring> Rings => rings;
            public Ring At(float y)
            {
                if (y <= rings[0].Y) return rings[0];
                for (int i = 1; i < rings.Count; i++)
                    if (y <= rings[i].Y)
                    {
                        Ring a = rings[i - 1], c = rings[i]; float t = (y - a.Y) / (c.Y - a.Y);
                        return new Ring(y, a.A + (c.A - a.A) * t, a.B + (c.B - a.B) * t, a.Z + (c.Z - a.Z) * t);
                    }
                return rings[rings.Count - 1];
            }
            // The torso's surface in front (−Z) or behind at (x, y).
            public float FrontZ(float x, float y) => Surface(x, y, -1);
            public float BackZ(float x, float y) => Surface(x, y, 1);
            private float Surface(float x, float y, int side)
            {
                var r = At(y); float u = Math.Min(1, Math.Abs(x) / r.A);
                return r.Z + side * r.B * (float)Math.Pow(1 - Math.Pow(u, TorsoRound), 1 / TorsoRound);
            }
            // The height of the shoulder's top x out from the middle (between the chest and the neck).
            public float TopAt(float x)
            {
                for (float y = NeckRise; y > 0.4f; y -= 0.001f) if (At(y).A >= x) return y;
                return 0.4f;
            }
            public V3 Point(Ring r, int k, float inflate)
            {
                double a = 2 * Math.PI * k / Sides;
                float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
                float x = Math.Sign(c) * (float)Math.Pow(Math.Abs(c), 2 / TorsoRound), z = Math.Sign(s) * (float)Math.Pow(Math.Abs(s), 2 / TorsoRound);
                return new V3((r.A + inflate) * x, r.Y, r.Z + (r.B + inflate) * z);
            }
        }

        // ---------- Torso ----------
        private static PolyMesh TorsoMesh(Outfit o)
        {
            var b = NewBuilder();
            var shape = new TorsoShape(o);
            float hem = Hem(o.Kind);
            // Rings of the profile plus the hem of the top, so the colours change on a ring.
            var ys = new List<float>();
            foreach (var r in shape.Rings) ys.Add(r.Y);
            if (hem > shape.Bottom && !ys.Contains(hem)) ys.Add(hem);
            ys.Sort();
            byte Band(float y) => y >= hem || shape.Open ? o.Top : o.Trousers;
            for (int j = 0; j + 1 < ys.Count; j++)
            {
                Ring r0 = shape.At(ys[j]), r1 = shape.At(ys[j + 1]); float mid = (r0.Y + r1.Y) / 2;
                byte color = Band(mid);
                for (int k = 0; k < shape.Sides; k++)
                {
                    V3 a = shape.Point(r0, k, 0), c = shape.Point(r0, k + 1, 0), e = shape.Point(r1, k + 1, 0), g = shape.Point(r1, k, 0);
                    var m = (a + c + e + g) * 0.25f;
                    b.Quad(a, c, e, g, m - new V3(0, m.Y, (r0.Z + r1.Z) / 2), TrimPart, color);
                }
            }
            var top = shape.At(NeckRise);
            Cap(b, shape, top, true, o.Top, 1);
            if (shape.Open) Lining(b, shape, o.Top);
            else Cap(b, shape, shape.At(shape.Bottom), false, o.Trousers, 0.8f);
            Clothes(b, shape, o, hem);
            return Done(b);
        }
        // Where the top ends over the trousers: jackets and coats over the hips, shirts tucked in at the belt.
        private static float Hem(Kind kind)
        {
            switch (kind)
            {
                case Kind.Shirt: case Kind.Braces: return 0.06f;
                case Kind.Apron: return 0.04f;
                case Kind.Bomber: return -0.005f;
                case Kind.Tee: return -0.02f;
                case Kind.Holiday: return -0.03f;
                case Kind.Hoodie: return -0.045f;
                default: return -0.065f;
            }
        }
        private static void Cap(PolyBuilder b, TorsoShape shape, Ring r, bool up, byte color, float shade)
        {
            var points = new List<V3>();
            for (int k = 0; k < shape.Sides; k++) points.Add(shape.Point(r, k, 0));
            b.Polygon(points, new V3(0, up ? 1 : -1, 0), TrimPart, color, shade);
        }
        // Inside of a skirt's hem, seen from below.
        private static void Lining(PolyBuilder b, TorsoShape shape, byte color)
        {
            Ring r0 = shape.At(shape.Bottom), r1 = shape.At(shape.Bottom + 0.12f);
            for (int k = 0; k < shape.Sides; k++)
            {
                V3 a = shape.Point(r0, k, -0.004f), c = shape.Point(r0, k + 1, -0.004f), e = shape.Point(r1, k + 1, -0.004f), g = shape.Point(r1, k, -0.004f);
                var m = (a + c + e + g) * 0.25f;
                b.Quad(a, c, e, g, new V3(0, m.Y, r0.Z) - m, TrimPart, color, 0.55f);
            }
            var points = new List<V3>();
            for (int k = 0; k < shape.Sides; k++) points.Add(shape.Point(r1, k, -0.004f));
            b.Polygon(points, new V3(0, -1, 0), TrimPart, color, 0.5f);
        }
        // A band round the torso from y0 to y1, `proud` out from its surface (belts, stripes, collars, waistbands).
        private static void Belt(PolyBuilder b, TorsoShape shape, float y0, float y1, float proud, byte color, int part = TrimPart, float shade = 1)
        {
            int group = b.Group; b.Group = b.NextGroup();
            Ring r0 = shape.At(y0), r1 = shape.At(y1);
            for (int k = 0; k < shape.Sides; k++)
            {
                V3 a = shape.Point(r0, k, proud), c = shape.Point(r0, k + 1, proud), e = shape.Point(r1, k + 1, proud), g = shape.Point(r1, k, proud);
                var m = (a + c + e + g) * 0.25f;
                b.Quad(a, c, e, g, m - new V3(0, m.Y, (r0.Z + r1.Z) / 2), part, color, shade);
            }
            // The rims, so a band seen edge-on is not a hairline.
            for (int k = 0; k < shape.Sides; k++)
            {
                b.Quad(shape.Point(r1, k, 0), shape.Point(r1, k + 1, 0), shape.Point(r1, k + 1, proud), shape.Point(r1, k, proud), new V3(0, 1, 0), part, color, shade);
                b.Quad(shape.Point(r0, k, 0), shape.Point(r0, k + 1, 0), shape.Point(r0, k + 1, proud), shape.Point(r0, k, proud), new V3(0, -1, 0), part, color, shade * 0.8f);
            }
            b.Group = group;
        }
        // A panel on the torso's front (back: behind) between y0 and y1, `lift` off the surface; span(y) gives its left and
        // right edge. Rows follow the torso's curve. Below the torso (an apron) it hangs, flattening as it drops, clear of the legs.
        private static void Panel(PolyBuilder b, TorsoShape shape, float y0, float y1, Func<float, (float, float)> span, byte color, float lift,
            bool back = false, int part = TrimPart, float shade = 1, int rows = 5, int cols = 3)
        {
            int group = b.Group; b.Group = b.NextGroup();
            float side = back ? 1 : -1, foot = shape.Bottom;
            // Columns no wider than 3 cm and a row on every ring of the torso, so the panel never sinks under its curve.
            foreach (float y in new[] { y0, (y0 + y1) / 2, y1 }) { var (l, r) = span(y); cols = Math.Max(cols, (int)Math.Ceiling((r - l) / 0.03f)); }
            var ys = new List<float>();
            for (int i = 0; i <= rows; i++) ys.Add(y0 + (y1 - y0) * i / rows);
            foreach (var ring in shape.Rings)
                if (ring.Y > y0 + 0.005f && ring.Y < y1 - 0.005f && !ys.Exists(y => Math.Abs(y - ring.Y) < 0.005f)) ys.Add(ring.Y);
            ys.Sort(); rows = ys.Count - 1;
            V3 At(int i, int j)
            {
                float y = ys[i]; var (x0, x1) = span(y); float x = x0 + (x1 - x0) * j / cols;
                float z = back ? shape.BackZ(x, Math.Max(y, foot)) : shape.FrontZ(x, Math.Max(y, foot));
                if (y < foot) { float flat = back ? shape.BackZ(0, foot) : shape.FrontZ(0, foot); z += (flat - z) * Math.Min(1, (foot - y) / 0.08f); }
                return new V3(x, y, z + side * lift);
            }
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    b.Quad(At(i, j), At(i, j + 1), At(i + 1, j + 1), At(i + 1, j), new V3(0, 0, side), part, color, shade);
            b.Group = group;
        }
        private static (float, float) Strip(float x, float half) => (x - half, x + half);
        // A small disc on the front (buttons, badges).
        private static void Button(PolyBuilder b, TorsoShape shape, float x, float y, float r, byte color, int part = TrimPart, float lift = 0.006f)
        {
            var points = new List<V3>();
            for (int k = 0; k < 8; k++)
            {
                double a = 2 * Math.PI * k / 8; float px = x + r * (float)Math.Cos(a), py = y + r * (float)Math.Sin(a);
                points.Add(new V3(px, py, shape.FrontZ(px, py) - lift));
            }
            b.Polygon(points, new V3(0, 0, -1), part, color);
        }
        // Shirt front in a V from the neck, a tie, lapels.
        private static void ShirtFront(PolyBuilder b, TorsoShape shape, float bottom, float half, byte shirt)
        {
            const float top = 0.588f;
            Panel(b, shape, bottom, top, y => { float w = half * (y - bottom) / (top - bottom); return (-w, w); }, shirt, 0.003f, rows: 4, cols: 2);
        }
        private static void Lapels(PolyBuilder b, TorsoShape shape, float bottom, float half, byte jacket)
        {
            const float top = 0.588f;
            foreach (int sx in new[] { -1, 1 })
                Panel(b, shape, bottom, top, y =>
                {
                    float t = (y - bottom) / (top - bottom), w = half * t, width = 0.032f * (float)Math.Sqrt(t);
                    return sx < 0 ? (-w - width, -w) : (w, w + width);
                }, jacket, 0.005f, shade: 0.8f, rows: 4, cols: 1);
        }
        // A tie from its point up to the knot under the collar: the knot a small block, wider at the top.
        private static void Tie(PolyBuilder b, TorsoShape shape, float bottom, byte tie)
        {
            Panel(b, shape, bottom, 0.558f, y => { float t = (y - bottom) / 0.04f, w = t < 1 ? 0.021f * t : 0.021f - 0.005f * (y - bottom - 0.04f) / (0.518f - bottom); return (-w, w); },
                tie, 0.007f, rows: 6, cols: 1);
            float z0 = shape.FrontZ(0, 0.553f), z1 = shape.FrontZ(0, 0.584f);
            PolyShapes.Block(b,
                new[] { new V3(-0.011f, 0.553f, z0 - 0.003f), new V3(0.011f, 0.553f, z0 - 0.003f), new V3(0.011f, 0.553f, z0 - 0.014f), new V3(-0.011f, 0.553f, z0 - 0.014f) },
                new[] { new V3(-0.016f, 0.584f, z1 - 0.002f), new V3(0.016f, 0.584f, z1 - 0.002f), new V3(0.016f, 0.584f, z1 - 0.015f), new V3(-0.016f, 0.584f, z1 - 0.015f) },
                TrimPart, tie);
        }
        private static void Straps(PolyBuilder b, TorsoShape shape, float bottom, float x, float half, byte color, bool back)
        {
            foreach (int sx in new[] { -1, 1 })
                Panel(b, shape, bottom, 0.56f, y => Strip(sx * x, half), color, 0.006f, back, rows: 4, cols: 1);
        }
        // A backpack on the upper back, with a flap and a band.
        private static void Backpack(PolyBuilder b, TorsoShape shape, byte pack, byte trim, byte band)
        {
            float z0 = shape.BackZ(0, 0.3f) - 0.01f;
            b.Box(new V3(-0.13f, 0.1f, z0), new V3(0.13f, 0.47f, z0 + 0.15f), 0.035f, TrimPart, pack);
            b.Box(new V3(-0.135f, 0.4f, z0 - 0.005f), new V3(0.135f, 0.485f, z0 + 0.158f), 0.025f, TrimPart, trim);
            b.Box(new V3(-0.09f, 0.2f, z0 + 0.14f), new V3(0.09f, 0.235f, z0 + 0.162f), 0.008f, TrimPart, band);
        }

        private static void Clothes(PolyBuilder b, TorsoShape shape, Outfit o, float hem)
        {
            int round = o.Round(10);
            // A jacket's hem stands a little proud of the trousers.
            if (hem > shape.Bottom && !shape.Open && o.Kind != Kind.Shirt && o.Kind != Kind.Braces && o.Kind != Kind.Apron) Belt(b, shape, hem, hem + 0.025f, 0.004f, o.Top);
            switch (o.Kind)
            {
                case Kind.Shirt:
                    Belt(b, shape, 0.06f, 0.1f, 0.005f, VoxelPalette.Ink);
                    Tie(b, shape, 0.16f, o.Tie);
                    Collar(b, shape, VoxelPalette.CarWhite, o);
                    break;
                case Kind.Suit: case Kind.Blazer:
                    ShirtFront(b, shape, 0.33f, 0.062f, o.Shirt);
                    Lapels(b, shape, 0.33f, 0.062f, o.Top);
                    if (o.Kind == Kind.Suit) { Tie(b, shape, 0.2f, o.Tie); Collar(b, shape, o.Shirt, o); }
                    else Scarf(b, shape, o.Tie);
                    Button(b, shape, 0, 0.27f, 0.009f, VoxelPalette.Ink); if (o.Kind == Kind.Suit) Button(b, shape, 0, 0.19f, 0.009f, VoxelPalette.Ink);
                    // Flaps of the pockets over the hips; the staff's jackets with a breast pocket (Orlov's with a square).
                    foreach (int sx in new[] { -1, 1 })
                        Panel(b, shape, 0.035f, 0.062f, y => sx < 0 ? (-0.152f, -0.078f) : (0.078f, 0.152f), o.Top, 0.006f, shade: 0.82f, rows: 1, cols: 3);
                    if (o.Fine) BreastPocket(b, shape, o);
                    // The trader's pocket full of papers.
                    if (o.Detail != VoxelPalette.Empty)
                    {
                        Panel(b, shape, 0.12f, 0.21f, y => (0.07f, 0.135f), o.Detail, 0.006f, rows: 2, cols: 2);
                        Panel(b, shape, 0.1f, 0.13f, y => (0.06f, 0.145f), o.Top, 0.009f, shade: 0.85f, rows: 1, cols: 2);
                    }
                    break;
                case Kind.Tee:
                    Belt(b, shape, 0.3f, 0.34f, 0.004f, o.Detail);
                    Belt(b, shape, 0.578f, 0.597f, 0.004f, o.Detail);
                    break;
                case Kind.Coat:
                    Belt(b, shape, 0.13f, 0.17f, 0.006f, o.Cuff);
                    foreach (float y in new[] { 0.25f, 0.36f }) foreach (int sx in new[] { -1, 1 }) Button(b, shape, sx * 0.05f, y, 0.01f, o.Cuff);
                    Wrap(b, shape, o.Detail);
                    Panel(b, shape, 0.3f, 0.555f, y => (0.03f + 0.02f * (0.555f - y), 0.085f + 0.02f * (0.555f - y)), o.Detail, 0.016f, rows: 4, cols: 1);
                    break;
                case Kind.Bomber:
                    Belt(b, shape, 0.29f, 0.325f, 0.004f, o.Detail);
                    Belt(b, shape, -0.005f, 0.035f, 0.006f, o.Cuff);
                    Belt(b, shape, 0.575f, 0.598f, 0.006f, o.Cuff);
                    Straps(b, shape, 0.17f, 0.085f, 0.016f, VoxelPalette.Ink, false);
                    Backpack(b, shape, o.Pack, o.PackTrim, o.Detail);
                    break;
                case Kind.Hoodie:
                    Belt(b, shape, -0.045f, -0.005f, 0.006f, o.Cuff);
                    Panel(b, shape, 0.04f, 0.19f, y => { float w = 0.11f - 0.12f * (y - 0.04f); return (-w, w); }, o.Detail, 0.005f, rows: 2, cols: 3);
                    foreach (int sx in new[] { -1, 1 }) Panel(b, shape, 0.44f, 0.575f, y => Strip(sx * 0.028f, 0.0045f), VoxelPalette.CarWhite, 0.007f, rows: 3, cols: 1);
                    PolyShapes.Blob(b, new V3(0, 0.555f, shape.BackZ(0, 0.555f) - 0.012f), 0.115f, 0.055f, 0.055f, round, o.Round(6), TrimPart, o.Detail);
                    Straps(b, shape, 0.2f, 0.1f, 0.016f, VoxelPalette.PineDark, false);
                    Backpack(b, shape, o.Pack, o.PackTrim, o.PackTrim);
                    break;
                case Kind.Cardigan:
                    ShirtFront(b, shape, 0.41f, 0.046f, VoxelPalette.Cream);
                    Collar(b, shape, VoxelPalette.Cream, o);
                    foreach (float y in new[] { 0.36f, 0.27f, 0.18f }) Button(b, shape, 0, y, 0.0085f, VoxelPalette.Brass, MetalPart);
                    foreach (int sx in new[] { -1, 1 }) Panel(b, shape, 0.03f, 0.12f, y => Strip(sx * 0.1f, 0.038f), o.Top, 0.004f, shade: 0.82f, rows: 1, cols: 2);
                    break;
                case Kind.Holiday:
                    Belt(b, shape, 0.578f, 0.597f, 0.004f, o.Top, shade: 0.8f);
                    foreach (int sx in new[] { -1, 1 })
                        Panel(b, shape, 0.36f, 0.585f, y => Strip(sx * (0.045f + 0.03f * (y - 0.36f) / 0.225f), 0.008f), VoxelPalette.Ink, 0.006f, rows: 3, cols: 1);
                    Camera(b, shape);
                    break;
                case Kind.Braces:
                    Belt(b, shape, 0.06f, 0.1f, 0.005f, VoxelPalette.Ink);
                    foreach (bool back in new[] { false, true }) Straps(b, shape, 0.09f, 0.075f, 0.012f, o.Detail, back);
                    Bow(b, shape, o.Detail);
                    Collar(b, shape, o.Top, o);
                    Panel(b, shape, 0.38f, 0.415f, y => (-0.13f, -0.085f), VoxelPalette.Cream, 0.006f, rows: 1, cols: 1);
                    break;
                case Kind.Apron:
                    Panel(b, shape, -0.32f, 0.42f, y => y > 0.2f ? (-0.1f - 0.2f * (0.42f - y), 0.1f + 0.2f * (0.42f - y)) : (-0.145f, 0.145f), o.Detail, 0.006f, rows: 8, cols: 3);
                    Belt(b, shape, 0.215f, 0.24f, 0.005f, o.Detail);
                    foreach (int sx in new[] { -1, 1 }) Panel(b, shape, 0.42f, 0.592f, y => Strip(sx * (0.095f - 0.25f * (y - 0.42f)), 0.009f), o.Detail, 0.006f, rows: 3, cols: 1);
                    break;
                case Kind.Uniform:
                    ShirtFront(b, shape, 0.45f, 0.045f, VoxelPalette.Cream);
                    Tie(b, shape, 0.38f, o.Tie);
                    Collar(b, shape, VoxelPalette.Cream, o);
                    Belt(b, shape, 0.0f, 0.045f, 0.006f, VoxelPalette.MetalDark);
                    Panel(b, shape, 0.002f, 0.043f, y => (-0.022f, 0.022f), VoxelPalette.Brass, 0.009f, part: MetalPart, rows: 1, cols: 1);
                    foreach (float y in new[] { 0.32f, 0.23f, 0.14f }) Button(b, shape, 0, y, 0.008f, VoxelPalette.Brass, MetalPart);
                    Badge(b, shape);
                    // Epaulettes along the tops of the shoulders.
                    foreach (int sx in new[] { -1, 1 })
                    {
                        float x0 = 0.1f, x1 = 0.19f, y0 = shape.TopAt(x0), y1 = shape.TopAt(x1);
                        V3 P(float x, float y, float z) => new V3(sx * x, y, z);
                        PolyShapes.Block(b,
                            new[] { P(x0, y0 - 0.006f, -0.04f), P(x1, y1 - 0.006f, -0.04f), P(x1, y1 - 0.006f, 0.045f), P(x0, y0 - 0.006f, 0.045f) },
                            new[] { P(x0, y0 + 0.008f, -0.04f), P(x1, y1 + 0.008f, -0.04f), P(x1, y1 + 0.008f, 0.045f), P(x0, y0 + 0.008f, 0.045f) },
                            TrimPart, o.Detail);
                    }
                    break;
            }
        }
        // A shirt's collar: a band round the neck above the torso, higher behind, with its inside and its rim, and the two
        // points down over the chest either side of the knot.
        private static void Collar(PolyBuilder b, TorsoShape shape, byte color, Outfit o)
        {
            int sides = o.LimbSides + 2; const float bottom = 0.578f, r0 = 0.069f, r1 = 0.065f, wall = 0.004f;
            V3 Ring(int k, float r, bool top)
            {
                double a = 2 * Math.PI * k / sides; float s = (float)Math.Sin(a);
                return new V3(r * (float)Math.Cos(a), top ? 0.618f + 0.014f * s : bottom, 0.012f + r * s);
            }
            int group = b.Group; b.Group = b.NextGroup();
            var axis = new V3(0, 0.6f, 0.012f);
            for (int k = 0; k < sides; k++)
            {
                V3 a0 = Ring(k, r0, false), a1 = Ring(k + 1, r0, false), b1 = Ring(k + 1, r1, true), b0 = Ring(k, r1, true);
                b.Quad(a0, a1, b1, b0, (a0 + a1 + b1 + b0) * 0.25f - axis, TrimPart, color);
                V3 i0 = Ring(k, r0 - wall, false), i1 = Ring(k + 1, r0 - wall, false), j1 = Ring(k + 1, r1 - wall, true), j0 = Ring(k, r1 - wall, true);
                b.Quad(i0, i1, j1, j0, axis - (i0 + i1 + j1 + j0) * 0.25f, TrimPart, color, 0.7f);
                b.Quad(b0, b1, j1, j0, new V3(0, 1, 0), TrimPart, color, 0.9f);
            }
            b.Group = group;
            foreach (int sx in new[] { -1, 1 })
                Panel(b, shape, 0.546f, 0.594f, y => { float t = (y - 0.546f) / 0.048f, inner = 0.026f - 0.015f * t, outer = 0.036f + 0.028f * t; return sx < 0 ? (-outer, -inner) : (inner, outer); },
                    color, 0.0075f, rows: 3, cols: 2);
        }
        // The staff's breast pocket: a welt on the chest, Orlov's with the folded points of a square above it.
        private static void BreastPocket(PolyBuilder b, TorsoShape shape, Outfit o)
        {
            const float x0 = 0.086f, x1 = 0.142f, y0 = 0.352f, y1 = 0.366f;
            Panel(b, shape, y0, y1, y => (x0, x1), o.Top, 0.006f, shade: 0.8f, rows: 1, cols: 3);
            if (o.Square == VoxelPalette.Empty) return;
            foreach (var (l, peak, r) in new[] { (x0 + 0.006f, x0 + 0.018f, x0 + 0.03f), (x0 + 0.022f, x0 + 0.036f, x1 - 0.008f) })
            {
                float top = y1 + (peak < x0 + 0.02f ? 0.022f : 0.017f);
                var points = new List<V3> { new V3(l, y1 - 0.002f, shape.FrontZ(l, y1) - 0.0075f), new V3(r, y1 - 0.002f, shape.FrontZ(r, y1) - 0.0075f),
                    new V3(peak, top, shape.FrontZ(peak, top) - 0.0075f) };
                b.Polygon(points, new V3(0, 0, -1), TrimPart, o.Square);
            }
        }
        // Lena's neck scarf: a small bow at the throat with two short ends.
        private static void Scarf(PolyBuilder b, TorsoShape shape, byte color)
        {
            foreach (int sx in new[] { -1, 1 })
                Panel(b, shape, 0.55f, 0.586f, y => { float t = (y - 0.55f) / 0.036f; return sx < 0 ? (-0.034f + 0.012f * t, -0.004f) : (0.004f, 0.034f - 0.012f * t); },
                    color, 0.006f, rows: 2, cols: 1);
            Panel(b, shape, 0.49f, 0.552f, y => { float t = (y - 0.49f) / 0.062f; return (-0.012f - 0.004f * (1 - t), 0.006f); }, color, 0.008f, shade: 0.9f, rows: 3, cols: 1);
        }
        private static void Bow(PolyBuilder b, TorsoShape shape, byte color)
        {
            foreach (int sx in new[] { -1, 1 })
                Panel(b, shape, 0.555f, 0.585f, y => { float t = Math.Abs(y - 0.57f) / 0.015f, w = 0.006f + 0.022f * (0.4f + 0.6f * t); return sx < 0 ? (-w, 0) : (0, w); },
                    color, 0.007f, rows: 2, cols: 1);
        }
        // The coat's scarf round the neck.
        private static void Wrap(PolyBuilder b, TorsoShape shape, byte color)
        {
            var path = new List<V3>();
            var r = shape.At(0.565f);
            for (int k = 0; k <= 12; k++)
            {
                double a = 2 * Math.PI * k / 12;
                path.Add(new V3((r.A * 0.62f) * (float)Math.Cos(a), 0.575f + 0.006f * (float)Math.Sin(a), r.Z + (r.B * 0.95f) * (float)Math.Sin(a)));
            }
            PolyShapes.Sweep(b, path, 0.03f, 0.03f, 6, new V3(0, 1, 0), TrimPart, color);
        }
        private static void Camera(PolyBuilder b, TorsoShape shape)
        {
            float z = shape.FrontZ(0, 0.33f);
            b.Box(new V3(-0.058f, 0.3f, z - 0.05f), new V3(0.058f, 0.37f, z - 0.004f), 0.008f, TrimPart, VoxelPalette.Ink);
            PolyShapes.Limb(b, new V3(0.012f, 0.334f, z - 0.048f), new V3(0.012f, 0.334f, z - 0.075f), 0.02f, 0.018f, 8, MetalPart, VoxelPalette.Chrome);
        }
        private static void Badge(PolyBuilder b, TorsoShape shape)
        {
            const float x = -0.1f, y = 0.42f;
            var points = new List<V3>();
            foreach (var (dx, dy) in new[] { (-0.018f, 0.016f), (0.018f, 0.016f), (0.018f, -0.004f), (0, -0.022f), (-0.018f, -0.004f) })
                points.Add(new V3(x + dx, y + dy, shape.FrontZ(x + dx, y + dy) - 0.007f));
            b.Polygon(points, new V3(0, 0, -1), MetalPart, VoxelPalette.Brass);
        }

        // ---------- Arms ----------
        // From the shoulder joint down: the shoulder, the sleeve to SleeveEnd with its cuff or hem, bare forearm below a short
        // sleeve, a mitten hand with the thumb forward.
        private static readonly V3[] ArmPath = { new V3(0, 0.02f, 0), new V3(0, -0.14f, 0.004f), new V3(0, -0.29f, 0.008f), new V3(0, -0.42f, -0.004f), new V3(0, -0.545f, -0.02f) };
        private const float WristY = -0.545f;
        private static PolyMesh ArmMesh(Outfit o, float shoulderX)
        {
            var b = NewBuilder();
            float k = o.Narrow ? 0.92f : 1;
            float R(float y) => k * (0.058f + (0.044f - 0.058f) * (0.02f - y) / (0.02f - WristY));
            float end = -Math.Min(o.SleeveEnd, -WristY);
            bool bare = end > WristY + 0.01f;
            int sides = o.LimbSides;
            Tube(b, ArmPath, -0.03f, end, R, 0, sides, o.Sleeve);
            PolyShapes.Blob(b, new V3(0, -0.035f, 0.001f), 0.057f * k, 0.056f, 0.06f * k, sides, o.Round(6), TrimPart, o.Sleeve);
            if (bare)
            {
                Tube(b, ArmPath, end + 0.03f, WristY - 0.01f, R, -0.012f, sides - 1, o.Skin);
                Cuff(b, ArmPath, end - 0.014f, end + 0.004f, R, 0.005f, o.Cuff, sides + 1);
            }
            else Cuff(b, ArmPath, WristY - 0.006f, WristY + 0.024f, R, 0.003f, o.Cuff, sides + 1);
            Hand(b, o, k);
            return Done(b);
        }
        // The hand hanging at the end of the arm, its lowest point ArmLength below the shoulder joint: a mitten with the
        // thumb forward; the staff's with four fingers side by side from the front to the back — one hand for both arms,
        // so it is the same either side, the palm to the thigh — and the thumb in front.
        private static void Hand(PolyBuilder b, Outfit o, float k)
        {
            if (!o.Fine)
            {
                PolyShapes.Blob(b, new V3(0, -ArmLength + 0.066f, -0.022f), 0.03f * k, 0.066f, 0.048f * k, 10, 6, TrimPart, o.Skin);
                PolyShapes.Blob(b, new V3(0, -ArmLength + 0.088f, -0.06f), 0.016f * k, 0.032f, 0.018f, 8, 4, TrimPart, o.Skin);
                return;
            }
            const float z = -0.021f;
            PolyShapes.Blob(b, new V3(0, WristY - 0.038f, z), 0.021f * k, 0.044f, 0.043f * k, 12, 8, TrimPart, o.Skin);
            // Index, middle, ring and little finger: how far each tip stays above the lowest, and half its length.
            foreach (var (dz, rise, half) in new[] { (-0.0285f, 0.006f, 0.027f), (-0.0095f, 0f, 0.03f), (0.0095f, 0.004f, 0.028f), (0.0285f, 0.013f, 0.024f) })
                PolyShapes.Blob(b, new V3(0, -ArmLength + rise + half, z + dz * k), 0.0088f * k, half, 0.0086f * k, 6, 6, TrimPart, o.Skin);
            PolyShapes.Blob(b, new V3(0, WristY - 0.043f, z - 0.041f * k), 0.0105f * k, 0.027f, 0.0115f * k, 8, 6, TrimPart, o.Skin);
        }
        // The seated guest's arm on the table: down and a little forward to the elbow on the table's edge, the forearm level
        // with its underside TableDrop below the shoulder joint, a loose fist with the thumb on top. The sleeve, a bare
        // forearm and the cuffs as on the hanging arm, measured along the arm.
        private const float ElbowY = 0.046f - TableDrop;
        private static readonly V3[] TableArmPath =
        {
            new V3(0, 0.02f, 0), new V3(0, 0.02f + (ElbowY - 0.02f) * 0.55f, -0.05f), new V3(0, 0.02f + (ElbowY - 0.02f) * 0.9f, -0.12f),
            new V3(0, ElbowY, -0.175f), new V3(0, 0.045f - TableDrop, -0.29f), new V3(0, 0.044f - TableDrop, -0.405f),
        };
        private static PolyMesh TableArmMesh(Outfit o)
        {
            var b = NewBuilder();
            float k = o.Narrow ? 0.92f : 1, wrist = ArcLength(TableArmPath);
            float R(float s) => k * (0.058f + (0.044f - 0.058f) * s / wrist);
            float end = Math.Min(o.SleeveEnd + 0.02f, wrist);
            bool bare = end < wrist - 0.01f;
            int sides = o.LimbSides;
            PolyShapes.Sweep(b, SubPath(TableArmPath, 0.05f, end), R(0.05f), R(end), sides, new V3(1, 0, 0), TrimPart, o.Sleeve);
            PolyShapes.Blob(b, new V3(0, -0.035f, 0.001f), 0.057f * k, 0.056f, 0.06f * k, sides, o.Round(6), TrimPart, o.Sleeve);
            if (bare)
            {
                PolyShapes.Sweep(b, SubPath(TableArmPath, end + 0.03f, wrist + 0.01f), R(end + 0.03f) - 0.012f, R(wrist) - 0.012f, sides - 1, new V3(1, 0, 0), TrimPart, o.Skin);
                PolyShapes.Limb(b, AtArc(TableArmPath, end - 0.018f), AtArc(TableArmPath, end + 0.004f), R(end) + 0.005f, R(end) + 0.005f, sides + 1, TrimPart, o.Cuff);
            }
            else PolyShapes.Limb(b, AtArc(TableArmPath, wrist - 0.03f), AtArc(TableArmPath, wrist + 0.006f), R(wrist) + 0.003f, R(wrist) + 0.003f, sides + 1, TrimPart, o.Cuff);
            var hand = TableArmPath[TableArmPath.Length - 1];
            PolyShapes.Blob(b, new V3(0, 0.036f - TableDrop, hand.Z - 0.05f), 0.03f * k, 0.036f, 0.05f, o.Round(10), o.Round(6), TrimPart, o.Skin);
            PolyShapes.Blob(b, new V3(0, 0.07f - TableDrop, hand.Z - 0.062f), 0.013f * k, 0.015f, 0.02f, o.Round(8), o.Round(4), TrimPart, o.Skin);
            return Done(b);
        }
        private static float ArcLength(V3[] path)
        {
            float s = 0;
            for (int i = 1; i < path.Length; i++) s += (path[i] - path[i - 1]).Length;
            return s;
        }
        // The point `s` metres along a path (beyond its ends: on, along the end segments).
        private static V3 AtArc(V3[] path, float s)
        {
            for (int i = 1; i < path.Length; i++)
            {
                float l = (path[i] - path[i - 1]).Length;
                if (s <= l || i == path.Length - 1) return V3.Lerp(path[i - 1], path[i], s / l);
                s -= l;
            }
            return path[path.Length - 1];
        }
        // The stretch of a path from s0 to s1 metres along it.
        private static List<V3> SubPath(V3[] path, float s0, float s1)
        {
            var points = new List<V3> { AtArc(path, s0) }; float s = 0;
            for (int i = 1; i < path.Length; i++)
            {
                s += (path[i] - path[i - 1]).Length;
                if (s > s0 + 0.01f && s < s1 - 0.01f) points.Add(path[i]);
            }
            points.Add(AtArc(path, s1));
            return points;
        }
        // The point of a path at height y (paths run down), linearly between its points.
        private static V3 Along(V3[] path, float y)
        {
            if (y >= path[0].Y) return new V3(path[0].X, y, path[0].Z);
            for (int i = 1; i < path.Length; i++)
                if (y >= path[i].Y) return V3.Lerp(path[i - 1], path[i], (path[i - 1].Y - y) / (path[i - 1].Y - path[i].Y));
            var last = path[path.Length - 1];
            return new V3(last.X, y, last.Z);
        }
        // A tube along a downward path from y0 to y1, radius R(y) + grow, `sides` round; open ends (they hide in the
        // shoulder, a cuff, a shoe or the torso).
        private static void Tube(PolyBuilder b, V3[] path, float y0, float y1, Func<float, float> radius, float grow, int sides, byte color)
        {
            var points = new List<V3> { Along(path, y0) };
            foreach (var p in path) if (p.Y < y0 - 0.01f && p.Y > y1 + 0.01f) points.Add(p);
            points.Add(Along(path, y1));
            // Sweep tapers linearly along the path: from the radius at the top to the radius at the bottom.
            PolyShapes.Sweep(b, points, radius(y0) + grow, radius(y1) + grow, sides, new V3(1, 0, 0), TrimPart, color);
        }
        // A short ring (cuff, hem) round the path from y0 to y1, `proud` out, with its rims.
        private static void Cuff(PolyBuilder b, V3[] path, float y0, float y1, Func<float, float> radius, float proud, byte color, int sides)
        {
            V3 top = Along(path, y1), bottom = Along(path, y0);
            PolyShapes.Limb(b, bottom, top, radius(y0) + proud, radius(y1) + proud, sides, TrimPart, color);
        }

        // ---------- Legs ----------
        // From the hip joint down: trousers (shorts over bare shins and socks; a coat over the thighs) to a turned-up hem, the
        // shoe on its sole at −HipY.
        private static readonly V3[] LegPath = { new V3(0, 0.04f, 0), new V3(0, -0.2f, -0.004f), new V3(0, -0.43f, -0.008f), new V3(0, -0.63f, 0), new V3(0, -0.8f, 0.008f) };
        private const float AnkleY = -0.8f;
        private static float LegRadius(float y) => 0.083f + (0.052f - 0.083f) * (0.04f - y) / (0.04f - AnkleY);
        private static PolyMesh LegMesh(Outfit o)
        {
            var b = NewBuilder();
            float trousers = o.TrouserEnd > 0 ? -(HipY - o.TrouserEnd) : AnkleY, top = 0.04f;
            bool bareLegs = o.SkirtTo > 0;
            int sides = o.LimbSides;
            if (bareLegs)
                Tube(b, LegPath, top, AnkleY - 0.04f, y => LegRadius(y) * 0.78f, 0, sides, o.Trousers);
            else
            {
                if (o.Coat != VoxelPalette.Empty)
                {
                    // Under the coat's skirt; it shows when the leg swings forward.
                    Tube(b, LegPath, top, -o.CoatTo, LegRadius, 0.004f, sides, o.Coat);
                    top = -o.CoatTo + 0.01f;
                }
                Tube(b, LegPath, top, trousers > AnkleY ? trousers : AnkleY - 0.01f, LegRadius, 0, sides, o.Trousers);
                Cuff(b, LegPath, Math.Max(trousers, AnkleY) - 0.004f, Math.Max(trousers, AnkleY) + 0.026f, LegRadius, 0.005f, o.Trousers, sides);
                if (trousers > AnkleY)
                {
                    Tube(b, LegPath, trousers + 0.03f, AnkleY - 0.04f, y => LegRadius(y) * 0.72f, 0, sides - 1, o.Skin);
                    if (o.Socks) Cuff(b, LegPath, AnkleY - 0.04f, AnkleY + 0.03f, y => LegRadius(y) * 0.72f, 0.004f, VoxelPalette.CarWhite, sides);
                }
            }
            Shoe(b, o, 0, -HipY, -0.03f, 0.118f);
            return Done(b);
        }
        // Sole and a domed upper; `half` is half its length, centred at z.
        private static void Shoe(PolyBuilder b, Outfit o, float x, float floor, float z, float half)
        {
            byte sole = o.Shoes == VoxelPalette.CarWhite ? VoxelPalette.StoneLight : VoxelPalette.Ink;
            b.Box(new V3(x - 0.049f, floor, z - half - 0.004f), new V3(x + 0.049f, floor + 0.018f, z + half), 0.007f, TrimPart, sole);
            PolyShapes.Lump(b, new V3(x, floor + 0.018f, z), 0.05f, 0.068f, half, o.Round(10), o.Round(6), 0, 0, TrimPart, o.Shoes, 1, true);
        }

        // ---------- Sitting ----------
        // Thighs forward on the seat from the hips' centre, knees, shins down to the shoes on the floor at −SeatedHipY; the
        // seat (up to 0.4 m) fits under the thighs.
        private static PolyMesh SeatedMesh(Outfit o)
        {
            var b = NewBuilder();
            bool shorts = o.TrouserEnd > 0; int sides = o.LimbSides;
            byte thigh = o.SkirtTo > 0 ? o.Top : o.Trousers, shin = shorts || o.SkirtTo > 0 ? o.Skin : o.Trousers;
            if (o.SkirtTo > 0) shin = o.Trousers;
            foreach (int sx in new[] { -1, 1 })
            {
                float x = sx * HipX;
                var thighPath = new List<V3> { new V3(x, 0.03f, 0.04f), new V3(x, 0.006f, -0.1f), new V3(x, -0.004f, -0.22f), new V3(x, -0.012f, -0.33f) };
                if (o.Coat != VoxelPalette.Empty)
                {
                    PolyShapes.Sweep(b, thighPath.GetRange(0, 3), 0.087f, 0.072f, sides, new V3(0, 1, 0), TrimPart, o.Coat);
                    PolyShapes.Sweep(b, thighPath.GetRange(2, 2), 0.068f, 0.06f, sides, new V3(0, 1, 0), TrimPart, thigh);
                }
                else PolyShapes.Sweep(b, thighPath, 0.083f, 0.06f, sides, new V3(0, 1, 0), TrimPart, thigh);
                PolyShapes.Blob(b, new V3(x, -0.016f, -0.33f), 0.06f, 0.058f, 0.06f, sides, o.Round(6), TrimPart, shorts ? o.Skin : thigh);
                var shinPath = new List<V3> { new V3(x, -0.02f, -0.33f), new V3(x, -0.2f, -0.334f), new V3(x, -0.4f, -0.338f) };
                PolyShapes.Sweep(b, shinPath, shorts ? 0.042f : 0.056f, shorts ? 0.038f : 0.05f, sides, new V3(1, 0, 0), TrimPart, shin);
                if (o.Socks) PolyShapes.Limb(b, new V3(x, -0.41f, -0.338f), new V3(x, -0.37f, -0.338f), 0.043f, 0.043f, sides, TrimPart, VoxelPalette.CarWhite);
                else if (!shorts && o.SkirtTo <= 0) PolyShapes.Limb(b, new V3(x, -0.405f, -0.338f), new V3(x, -0.375f, -0.338f), 0.057f, 0.057f, sides, TrimPart, o.Trousers);
                Shoe(b, o, x, -SeatedHipY, -0.314f, 0.088f);
            }
            return Done(b);
        }
    }
}
