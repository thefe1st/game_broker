using System;
using System.Collections.Generic;

namespace Broker.Core
{
    // Round and free-form pieces for the low-poly builders: lathed profiles (column bases, shafts, bowls, flames), discs,
    // ellipsoid blobs and tapered limbs (the statues) and six-sided blocks from their corners (ship prows, roof slabs).
    // Every face gets its outward hint from the shape, so PolyBuilder turns it the right way round.
    internal static class PolyShapes
    {
        private static V3 Around(float cx, float cz, float y, float r, double a) => new V3(cx + r * (float)Math.Cos(a), y, cz + r * (float)Math.Sin(a));

        // Surface of revolution round the vertical axis through (cx, cz). The profile runs bottom to top as (y, radius)
        // with the solid on the axis side; caps close a non-zero radius at either end. Neighbouring faces smooth into
        // each other when they meet at less than the builder's smoothing angle.
        public static void Lathe(PolyBuilder b, float cx, float cz, IList<(float y, float r)> profile, int sides, int part, byte color,
            float shade = 1, bool capBottom = false, bool capTop = false, float phase = 0)
        {
            for (int j = 0; j + 1 < profile.Count; j++)
            {
                var (y0, r0) = profile[j]; var (y1, r1) = profile[j + 1];
                float dy = y1 - y0, dr = r1 - r0;
                for (int k = 0; k < sides; k++)
                {
                    double a0 = phase + 2 * Math.PI * k / sides, a1 = phase + 2 * Math.PI * (k + 1) / sides, am = (a0 + a1) / 2;
                    var outward = new V3((float)Math.Cos(am) * dy, -dr, (float)Math.Sin(am) * dy);
                    b.Quad(Around(cx, cz, y0, r0, a0), Around(cx, cz, y0, r0, a1), Around(cx, cz, y1, r1, a1), Around(cx, cz, y1, r1, a0), outward, part, color, shade);
                }
            }
            if (capBottom && profile[0].r > 0) Disc(b, cx, profile[0].y, cz, profile[0].r, sides, false, part, color, shade * 0.85f, phase);
            var last = profile[profile.Count - 1];
            if (capTop && last.r > 0) Disc(b, cx, last.y, cz, last.r, sides, true, part, color, shade, phase);
        }
        // A flat round face at height y, facing up or down.
        public static void Disc(PolyBuilder b, float cx, float y, float cz, float r, int sides, bool up, int part, byte color, float shade = 1, float phase = 0)
        {
            var centre = new V3(cx, y, cz); var n = new V3(0, up ? 1 : -1, 0);
            for (int k = 0; k < sides; k++)
                b.Triangle(centre, Around(cx, cz, y, r, phase + 2 * Math.PI * k / sides), Around(cx, cz, y, r, phase + 2 * Math.PI * (k + 1) / sides), n, part, color, shade);
        }
        // Ellipsoid round c with radii (rx, ry, rz): `rings` bands from the bottom pole to the top, `sides` round the
        // vertical axis, in a smoothing group of its own.
        public static void Blob(PolyBuilder b, V3 c, float rx, float ry, float rz, int sides, int rings, int part, byte color, float shade = 1)
        {
            int group = b.Group; b.Group = b.NextGroup();
            V3 P(int i, int k)
            {
                double polar = Math.PI * i / rings, a = 2 * Math.PI * k / sides;
                float s = (float)Math.Sin(polar);
                return new V3(c.X + rx * s * (float)Math.Cos(a), c.Y - ry * (float)Math.Cos(polar), c.Z + rz * s * (float)Math.Sin(a));
            }
            for (int i = 0; i < rings; i++)
                for (int k = 0; k < sides; k++)
                {
                    V3 p00 = P(i, k), p01 = P(i, k + 1), p11 = P(i + 1, k + 1), p10 = P(i + 1, k);
                    b.Quad(p00, p01, p11, p10, (p00 + p01 + p11 + p10) * 0.25f - c, part, color, shade);
                }
            b.Group = group;
        }
        // Tapered tube from p0 (radius r0) to p1 (radius r1), `sides` round, capped at both ends unless told otherwise;
        // its own smoothing group.
        public static void Limb(PolyBuilder b, V3 p0, V3 p1, float r0, float r1, int sides, int part, byte color, float shade = 1, bool caps = true)
        {
            var axis = (p1 - p0).Normalized;
            var helper = Math.Abs(axis.Y) < 0.9f ? new V3(0, 1, 0) : new V3(1, 0, 0);
            var u = V3.Cross(axis, helper).Normalized; var w = V3.Cross(axis, u);
            int group = b.Group; b.Group = b.NextGroup();
            V3 At(V3 o, float r, int k) { double a = 2 * Math.PI * k / sides; return o + u * (r * (float)Math.Cos(a)) + w * (r * (float)Math.Sin(a)); }
            var start = new List<V3>(); var end = new List<V3>();
            for (int k = 0; k < sides; k++)
            {
                V3 a0 = At(p0, r0, k), a1 = At(p0, r0, k + 1), b1 = At(p1, r1, k + 1), b0 = At(p1, r1, k);
                var mid = (a0 + a1 + b1 + b0) * 0.25f;
                var onAxis = p0 + axis * V3.Dot(mid - p0, axis);
                b.Quad(a0, a1, b1, b0, mid - onAxis, part, color, shade);
                start.Add(a0); end.Add(b0);
            }
            if (caps && r0 > 0) b.Polygon(start, axis * -1, part, color, shade * 0.9f);
            if (caps && r1 > 0) b.Polygon(end, axis, part, color, shade);
            b.Group = group;
        }
        // Six-sided block from its four lower and four upper corners, both in the same turning order; convex, so each face
        // takes its hint from the block's centre. A smoothing group of its own keeps the faces flat.
        public static void Block(PolyBuilder b, V3[] lower, V3[] upper, int part, byte color, float shade = 1)
        {
            var centre = new V3(0, 0, 0);
            for (int k = 0; k < 4; k++) centre = centre + lower[k] + upper[k];
            centre = centre * (1f / 8);
            int group = b.Group; b.Group = b.NextGroup();
            void Side(V3 p0, V3 p1, V3 p2, V3 p3) => b.Quad(p0, p1, p2, p3, (p0 + p1 + p2 + p3) * 0.25f - centre, part, color, shade);
            Side(lower[0], lower[1], lower[2], lower[3]);
            Side(upper[0], upper[1], upper[2], upper[3]);
            for (int k = 0; k < 4; k++) Side(lower[k], lower[(k + 1) % 4], upper[(k + 1) % 4], upper[k]);
            b.Group = group;
        }
    }
}
