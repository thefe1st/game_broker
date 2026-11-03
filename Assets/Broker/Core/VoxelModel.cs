using System;
using System.Collections.Generic;

namespace Broker.Core
{
    // Deterministic integer hash: the same seed builds the same tree/car/building on every platform.
    public static class VoxelNoise
    {
        public static uint Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u ^ (uint)x * 0x85EBCA77u ^ (uint)y * 0xC2B2AE3Du ^ (uint)z * 0x27D4EB2Fu;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15; return h;
            }
        }
        // [0, 1) with 24-bit steps: exact in float on every platform.
        public static float Value(int x, int y, int z, int seed) => (Hash(x, y, z, seed) & 0xFFFFFF) / 16777216f;
    }

    // A small grid of palette indices. X right, Y up, Z forward; models face −Z unless noted.
    // Vertices are relative to the pivot (default: bottom centre), in metres.
    public sealed class VoxelModel
    {
        public const int MaxSize = 256;
        private readonly byte[] cells;
        public int SizeX { get; } public int SizeY { get; } public int SizeZ { get; }
        public float VoxelSize { get; }
        public float PivotX { get; set; } public float PivotY { get; set; } public float PivotZ { get; set; }
        public VoxelModel(int sizeX, int sizeY, int sizeZ, float voxelSize)
        {
            if (sizeX < 1 || sizeY < 1 || sizeZ < 1 || sizeX > MaxSize || sizeY > MaxSize || sizeZ > MaxSize)
                throw new ArgumentOutOfRangeException(nameof(sizeX));
            if (!(voxelSize > 0) || float.IsInfinity(voxelSize)) throw new ArgumentOutOfRangeException(nameof(voxelSize));
            SizeX = sizeX; SizeY = sizeY; SizeZ = sizeZ; VoxelSize = voxelSize;
            PivotX = sizeX / 2f; PivotZ = sizeZ / 2f; cells = new byte[sizeX * sizeY * sizeZ];
        }
        public float Width => SizeX * VoxelSize;
        public float Height => SizeY * VoxelSize;
        public float Depth => SizeZ * VoxelSize;
        public bool Inside(int x, int y, int z) => x >= 0 && y >= 0 && z >= 0 && x < SizeX && y < SizeY && z < SizeZ;
        public byte Get(int x, int y, int z) => Inside(x, y, z) ? cells[(y * SizeZ + z) * SizeX + x] : VoxelPalette.Empty;
        // Writes outside the grid are clipped: generators may draw shapes that cross the border.
        public void Set(int x, int y, int z, byte color)
        {
            if (color >= VoxelPalette.Count) throw new ArgumentOutOfRangeException(nameof(color));
            if (Inside(x, y, z)) cells[(y * SizeZ + z) * SizeX + x] = color;
        }
        public void Box(int x, int y, int z, int width, int height, int depth, byte color)
        {
            for (int yy = Math.Max(0, y); yy < Math.Min(SizeY, y + height); yy++)
                for (int zz = Math.Max(0, z); zz < Math.Min(SizeZ, z + depth); zz++)
                    for (int xx = Math.Max(0, x); xx < Math.Min(SizeX, x + width); xx++) Set(xx, yy, zz, color);
        }
        // Voxel centres inside the ellipsoid; the callback picks the colour (or Empty to skip).
        public void Ellipsoid(float cx, float cy, float cz, float rx, float ry, float rz, Func<int, int, int, float, byte> paint)
        {
            for (int y = Math.Max(0, (int)Math.Floor(cy - ry)); y <= Math.Min(SizeY - 1, (int)Math.Ceiling(cy + ry)); y++)
                for (int z = Math.Max(0, (int)Math.Floor(cz - rz)); z <= Math.Min(SizeZ - 1, (int)Math.Ceiling(cz + rz)); z++)
                    for (int x = Math.Max(0, (int)Math.Floor(cx - rx)); x <= Math.Min(SizeX - 1, (int)Math.Ceiling(cx + rx)); x++)
                    {
                        float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry, dz = (z + 0.5f - cz) / rz;
                        float d = dx * dx + dy * dy + dz * dz;
                        if (d > 1) continue;
                        byte color = paint(x, y, z, d);
                        if (color != VoxelPalette.Empty) Set(x, y, z, color);
                    }
        }
        public void Ellipsoid(float cx, float cy, float cz, float rx, float ry, float rz, byte color)
            => Ellipsoid(cx, cy, cz, rx, ry, rz, (x, y, z, d) => color);
        // Vertical cylinder on the XZ plane.
        public void Cylinder(float cx, float cz, float radius, int y, int height, byte color)
        {
            for (int yy = y; yy < y + height; yy++)
                for (int z = 0; z < SizeZ; z++)
                    for (int x = 0; x < SizeX; x++)
                    {
                        float dx = x + 0.5f - cx, dz = z + 0.5f - cz;
                        if (dx * dx + dz * dz <= radius * radius) Set(x, yy, z, color);
                    }
        }
        public int Filled
        {
            get { int n = 0; foreach (byte c in cells) if (c != VoxelPalette.Empty) n++; return n; }
        }
    }

    public sealed class VoxelMesh
    {
        // Flat arrays: positions/normals xyz, uvs uv. Solid and glowing triangles are separate submeshes.
        public float[] Positions { get; internal set; }
        public float[] Normals { get; internal set; }
        public float[] Uvs { get; internal set; }
        public int[] Triangles { get; internal set; }
        public int[] GlowTriangles { get; internal set; }
        public int VertexCount => Positions.Length / 3;
        public int QuadCount => VertexCount / 4;
    }

    // Greedy mesher: only faces between a filled voxel and empty space are emitted, then equal-colour
    // neighbours on one plane merge into rectangles. Triangles are clockwise seen from outside (Unity front faces):
    // cross(v1 − v0, v2 − v0) points along the outward normal.
    public static class VoxelMesher
    {
        public static VoxelMesh Build(VoxelModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            var positions = new List<float>(); var normals = new List<float>(); var uvs = new List<float>();
            var solid = new List<int>(); var glow = new List<int>();
            int[] size = { model.SizeX, model.SizeY, model.SizeZ };
            var p = new int[3]; var q = new int[3];
            for (int d = 0; d < 3; d++)
            {
                int u = (d + 1) % 3, v = (d + 2) % 3;
                var mask = new byte[size[u] * size[v]];
                for (int side = -1; side <= 1; side += 2)
                    for (int slice = 0; slice < size[d]; slice++)
                    {
                        for (int j = 0; j < size[v]; j++)
                            for (int i = 0; i < size[u]; i++)
                            {
                                p[d] = slice; p[u] = i; p[v] = j; q[d] = slice + side; q[u] = i; q[v] = j;
                                byte c = model.Get(p[0], p[1], p[2]);
                                mask[j * size[u] + i] = c != VoxelPalette.Empty && model.Get(q[0], q[1], q[2]) == VoxelPalette.Empty ? c : VoxelPalette.Empty;
                            }
                        for (int j = 0; j < size[v]; j++)
                            for (int i = 0; i < size[u];)
                            {
                                byte c = mask[j * size[u] + i];
                                if (c == VoxelPalette.Empty) { i++; continue; }
                                int w = 1; while (i + w < size[u] && mask[j * size[u] + i + w] == c) w++;
                                int h = 1;
                                for (; j + h < size[v]; h++)
                                {
                                    bool row = true;
                                    for (int k = 0; k < w; k++) if (mask[(j + h) * size[u] + i + k] != c) { row = false; break; }
                                    if (!row) break;
                                }
                                for (int jj = 0; jj < h; jj++) for (int k = 0; k < w; k++) mask[(j + jj) * size[u] + i + k] = VoxelPalette.Empty;
                                Quad(model, d, u, v, side, slice + (side > 0 ? 1 : 0), i, j, w, h, c, positions, normals, uvs, VoxelPalette.Glows(c) ? glow : solid);
                                i += w;
                            }
                    }
            }
            return new VoxelMesh { Positions = positions.ToArray(), Normals = normals.ToArray(), Uvs = uvs.ToArray(), Triangles = solid.ToArray(), GlowTriangles = glow.ToArray() };
        }
        private static void Quad(VoxelModel m, int d, int u, int v, int side, int plane, int i, int j, int w, int h, byte color,
            List<float> positions, List<float> normals, List<float> uvs, List<int> triangles)
        {
            int start = positions.Count / 3;
            var corner = new float[3];
            int[,] uv = { { i, j }, { i + w, j }, { i + w, j + h }, { i, j + h } };
            for (int k = 0; k < 4; k++)
            {
                corner[d] = plane; corner[u] = uv[k, 0]; corner[v] = uv[k, 1];
                positions.Add((corner[0] - m.PivotX) * m.VoxelSize); positions.Add((corner[1] - m.PivotY) * m.VoxelSize); positions.Add((corner[2] - m.PivotZ) * m.VoxelSize);
                normals.Add(d == 0 ? side : 0); normals.Add(d == 1 ? side : 0); normals.Add(d == 2 ? side : 0);
                uvs.Add(VoxelPalette.U(color)); uvs.Add(VoxelPalette.V(color));
            }
            // cross(+u, +v) = +d for cyclic axes, so (0, 1, 2) faces +d and the reversed order faces −d.
            if (side > 0) { triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2); triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3); }
            else { triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1); triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2); }
        }
    }
}
