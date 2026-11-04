using System;
using System.Collections.Generic;

namespace Broker.Core
{
    // Small float vector for the low-poly builders; Core has no engine types.
    public readonly struct V3
    {
        public readonly float X, Y, Z;
        public V3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static V3 operator *(V3 a, float s) => new V3(a.X * s, a.Y * s, a.Z * s);
        public static float Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static V3 Cross(V3 a, V3 b) => new V3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public static V3 Lerp(V3 a, V3 b, float t) => new V3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        public V3 Normalized { get { float l = Length; return l > 1e-12f ? this * (1 / l) : new V3(0, 1, 0); } }
        public override string ToString() => "(" + X + ", " + Y + ", " + Z + ")";
    }

    // A welded low-poly mesh: one triangle list per submesh. Shades are corner shades like VoxelMesh.Shades
    // (255 = open), so the Broker/Voxel material darkens creases and the underside the same way.
    public sealed class PolyMesh
    {
        public float[] Positions { get; internal set; }
        public float[] Normals { get; internal set; }
        public float[] Uvs { get; internal set; }
        public byte[] Shades { get; internal set; }
        public int[][] Triangles { get; internal set; }
        public int VertexCount => Positions.Length / 3;
        public int TriangleCount { get { int n = 0; foreach (var t in Triangles) n += t.Length / 3; return n; } }
        public V3 Position(int i) => new V3(Positions[i * 3], Positions[i * 3 + 1], Positions[i * 3 + 2]);
        public V3 Normal(int i) => new V3(Normals[i * 3], Normals[i * 3 + 1], Normals[i * 3 + 2]);
    }

    // Collects triangles (submesh, palette colour, corner shade, smoothing group) and welds them. A vertex normal
    // averages, by area, the faces of its group that meet it at less than the smoothing angle, so big panels stay flat
    // and narrow chamfers read as rounded edges. Every face is turned towards the outward hint it was given;
    // triangles come out clockwise seen from outside (Unity front faces): cross(b − a, c − a) points out.
    internal sealed class PolyBuilder
    {
        private struct Tri { public V3 A, B, C, N; public float Area, SA, SB, SC; public int Part, Group; public byte Color; }
        private readonly List<Tri> tris = new List<Tri>();
        private readonly int parts;
        private readonly float cosSmooth;
        // Paint UVs: a box projection in metres over this tile; other parts sample their palette texel.
        private readonly float tile;
        private readonly int uvPart;
        public int Group { get; set; }
        // Corner shade as a function of position (contact darkening near the ground); multiplied by each face's own shade.
        public Func<V3, float> Ambient { get; set; } = p => 1;
        public PolyBuilder(int parts, float smoothAngleDegrees, int boxMappedPart, float tileMetres)
        {
            this.parts = parts; cosSmooth = (float)Math.Cos(smoothAngleDegrees * Math.PI / 180); uvPart = boxMappedPart; tile = tileMetres;
        }
        public int Count => tris.Count;

        public void Triangle(V3 a, V3 b, V3 c, V3 outward, int part, byte color, float shade = 1)
        {
            var n = V3.Cross(b - a, c - a); float area2 = n.Length;
            if (area2 < 1e-9f) return;
            if (V3.Dot(n, outward) < 0) { var t = b; b = c; c = t; n = n * -1; }
            tris.Add(new Tri { A = a, B = b, C = c, N = n * (1 / area2), Area = area2 / 2, Part = part, Color = color, Group = Group,
                SA = shade * Ambient(a), SB = shade * Ambient(b), SC = shade * Ambient(c) });
        }
        public void Quad(V3 a, V3 b, V3 c, V3 d, V3 outward, int part, byte color, float shade = 1)
        {
            Triangle(a, b, c, outward, part, color, shade); Triangle(a, c, d, outward, part, color, shade);
        }
        // Convex polygon as a fan around its centroid.
        public void Polygon(IList<V3> points, V3 outward, int part, byte color, float shade = 1)
        {
            var centre = new V3(0, 0, 0);
            foreach (var p in points) centre = centre + p;
            centre = centre * (1f / points.Count);
            for (int i = 0; i < points.Count; i++) Triangle(centre, points[i], points[(i + 1) % points.Count], outward, part, color, shade);
        }
        // Axis-aligned box with all twelve edges chamfered by bevel (0 = sharp box), in its own smoothing group.
        public void Box(V3 min, V3 max, float bevel, int part, byte color, float shade = 1)
        {
            float[] lo = { min.X, min.Y, min.Z }, hi = { max.X, max.Y, max.Z };
            float b = Math.Max(0, Math.Min(bevel, 0.45f * Math.Min(hi[0] - lo[0], Math.Min(hi[1] - lo[1], hi[2] - lo[2]))));
            float[] innerLo = { lo[0] + b, lo[1] + b, lo[2] + b }, innerHi = { hi[0] - b, hi[1] - b, hi[2] - b };
            var centre = (min + max) * 0.5f;
            int group = Group; Group = NextGroup();
            V3 P(float[] c) => new V3(c[0], c[1], c[2]);
            float Face(int axis, int sign) => sign > 0 ? hi[axis] : lo[axis];
            float Inner(int axis, int sign) => sign > 0 ? innerHi[axis] : innerLo[axis];
            void Out(V3 p0, V3 p1, V3 p2, V3 p3)
            {
                var mid = (p0 + p1 + p2 + p3) * 0.25f; Quad(p0, p1, p2, p3, mid - centre, part, color, shade);
            }
            for (int axis = 0; axis < 3; axis++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int u = (axis + 1) % 3, v = (axis + 2) % 3; var c = new float[3];
                    var q = new V3[4]; int k = 0;
                    foreach (var (su, sv) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                    { c[axis] = Face(axis, sign); c[u] = Inner(u, su); c[v] = Inner(v, sv); q[k++] = P(c); }
                    Out(q[0], q[1], q[2], q[3]);
                }
            if (b > 0)
            {
                // Edge chamfers between faces i and j, along the third axis.
                for (int i = 0; i < 3; i++)
                    for (int j = i + 1; j < 3; j++)
                    {
                        int k = 3 - i - j;
                        for (int si = -1; si <= 1; si += 2)
                            for (int sj = -1; sj <= 1; sj += 2)
                            {
                                var c = new float[3]; var q = new V3[4]; int n = 0;
                                foreach (var (onI, sk) in new[] { (true, -1), (true, 1), (false, 1), (false, -1) })
                                {
                                    c[i] = onI ? Face(i, si) : Inner(i, si); c[j] = onI ? Inner(j, sj) : Face(j, sj); c[k] = Inner(k, sk);
                                    q[n++] = P(c);
                                }
                                Out(q[0], q[1], q[2], q[3]);
                            }
                    }
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sy = -1; sy <= 1; sy += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            var a = new V3(Face(0, sx), Inner(1, sy), Inner(2, sz));
                            var bb = new V3(Inner(0, sx), Face(1, sy), Inner(2, sz));
                            var cc = new V3(Inner(0, sx), Inner(1, sy), Face(2, sz));
                            Triangle(a, bb, cc, (a + bb + cc) * (1f / 3) - centre, part, color, shade);
                        }
            }
            Group = group;
        }
        private int nextGroup = 1000;
        public int NextGroup() => nextGroup++;

        private static long Quant(float v) => (long)Math.Round(v * 20000.0);
        private static long PositionKey(V3 p, int group)
            => unchecked(((Quant(p.X) * 73856093L) ^ (Quant(p.Y) * 19349663L) ^ (Quant(p.Z) * 83492791L)) * 31 + group);

        public PolyMesh Build(Func<int, byte, (float u, float v)> paletteUv)
        {
            // Faces around each welded position (exact coordinates and group; the hash only buckets).
            var around = new Dictionary<long, List<int>>();
            for (int t = 0; t < tris.Count; t++)
                for (int k = 0; k < 3; k++)
                {
                    long key = PositionKey(Corner(tris[t], k), tris[t].Group);
                    if (!around.TryGetValue(key, out var list)) around[key] = list = new List<int>();
                    list.Add(t * 3 + k);
                }
            var positions = new List<float>(); var normals = new List<float>(); var uvs = new List<float>(); var shades = new List<byte>();
            var lists = new List<int>[parts]; for (int p = 0; p < parts; p++) lists[p] = new List<int>();
            var index = new Dictionary<(long, long, long, int, int, int, int, int, int, int), int>();
            for (int t = 0; t < tris.Count; t++)
            {
                var tri = tris[t];
                for (int k = 0; k < 3; k++)
                {
                    var p = Corner(tri, k);
                    var sum = new V3(0, 0, 0);
                    foreach (int other in around[PositionKey(p, tri.Group)])
                    {
                        var o = tris[other / 3];
                        if (o.Group != tri.Group || !Same(Corner(o, other % 3), p)) continue;
                        if (V3.Dot(o.N, tri.N) >= cosSmooth) sum = sum + o.N * o.Area;
                    }
                    var n = sum.Length > 1e-12f ? sum.Normalized : tri.N;
                    float u, v;
                    if (tri.Part == uvPart)
                    {
                        float ax = Math.Abs(tri.N.X), ay = Math.Abs(tri.N.Y), az = Math.Abs(tri.N.Z);
                        if (ax >= ay && ax >= az) { u = p.Z / tile; v = p.Y / tile; }
                        else if (ay >= az) { u = p.X / tile; v = p.Z / tile; }
                        else { u = p.X / tile; v = p.Y / tile; }
                    }
                    else (u, v) = paletteUv(tri.Part, tri.Color);
                    float s = k == 0 ? tri.SA : k == 1 ? tri.SB : tri.SC;
                    byte shade = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(s * 255)));
                    var key = (Quant(p.X), Quant(p.Y), Quant(p.Z), (int)Math.Round(n.X * 1000), (int)Math.Round(n.Y * 1000), (int)Math.Round(n.Z * 1000),
                        (int)Math.Round(u * 100000), (int)Math.Round(v * 100000), (int)shade, tri.Part);
                    if (!index.TryGetValue(key, out int vi))
                    {
                        vi = positions.Count / 3; index[key] = vi;
                        positions.Add(p.X); positions.Add(p.Y); positions.Add(p.Z);
                        normals.Add(n.X); normals.Add(n.Y); normals.Add(n.Z);
                        uvs.Add(u); uvs.Add(v); shades.Add(shade);
                    }
                    lists[tri.Part].Add(vi);
                }
            }
            var triangles = new int[parts][]; for (int p = 0; p < parts; p++) triangles[p] = lists[p].ToArray();
            return new PolyMesh { Positions = positions.ToArray(), Normals = normals.ToArray(), Uvs = uvs.ToArray(), Shades = shades.ToArray(), Triangles = triangles };
        }
        private static V3 Corner(Tri t, int k) => k == 0 ? t.A : k == 1 ? t.B : t.C;
        private static bool Same(V3 a, V3 b) => Quant(a.X) == Quant(b.X) && Quant(a.Y) == Quant(b.Y) && Quant(a.Z) == Quant(b.Z);
    }
}
