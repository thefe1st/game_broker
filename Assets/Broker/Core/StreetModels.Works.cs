using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Broker.Core
{
    // Bollards, traffic cones, the service works' barrier and the refuse container in palette trim, in place of the voxel
    // models (VoxelProps.Bollard, Cone, ConstructionBarrier, Dumpster) with their pivots and bounds; fronts face −Z.
    public static partial class StreetModels
    {
        public const float BollardHeight = 0.9f, ConeHeight = 0.7f, ConeHollow = 0.05f, BarrierLampY = 1.0f, BarrierLampRadius = 0.05f, DumpsterLidY = 1.0f;
        // The barrier's two boards (bottom, top), their front and back faces and the width of its stripes along x + y.
        public static ReadOnlyCollection<(float bottom, float top)> BarrierBoards { get; } = Array.AsReadOnly(new[] { (0.4f, 0.6f), (0.7f, 0.9f) });
        public const float BoardFront = -0.07f, BoardBack = -0.03f, StripeWidth = 0.2f;

        // Bollard 0.9 m: a round dark post on a flared foot, a yellow band at 0.6–0.7 m and a pale domed cap.
        public static PolyMesh Bollard()
        {
            var b = NewBuilder();
            const int sides = 10;
            PolyShapes.Lathe(b, 0, 0, new[] { (0f, 0.125f), (0.03f, 0.125f), (0.07f, 0.103f), (0.6f, 0.1f) }, sides, TrimPart, VoxelPalette.MetalDark);
            PolyShapes.Lathe(b, 0, 0, new[] { (0.6f, 0.1f), (0.7f, 0.1f) }, sides, TrimPart, VoxelPalette.CarYellow);
            PolyShapes.Lathe(b, 0, 0, new[] { (0.7f, 0.1f), (0.78f, 0.1f) }, sides, TrimPart, VoxelPalette.MetalDark);
            PolyShapes.Lathe(b, 0, 0, new[] { (0.78f, 0.1f), (0.79f, 0.108f), (0.82f, 0.106f), (0.86f, 0.078f), (0.89f, 0.038f), (BollardHeight, 0) }, sides,
                TrimPart, VoxelPalette.Metal);
            return b.Build(BuildingModels.PaletteUv);
        }

        // Traffic cone 0.7 m: an orange cone with two white bands on a square black rubber foot, open at the tip over a dark
        // hollow.
        public static PolyMesh Cone()
        {
            var b = NewBuilder();
            const float foot = 0.045f, r0 = 0.165f, r1 = 0.035f;
            b.Box(new V3(-0.24f, 0, -0.24f), new V3(0.24f, foot, 0.24f), 0.012f, TrimPart, VoxelPalette.Tire);
            float R(float y) => r0 + (r1 - r0) * (y - foot) / (ConeHeight - foot);
            var bands = new[] { (foot, VoxelPalette.Amber), (0.22f, VoxelPalette.CarWhite), (0.32f, VoxelPalette.Amber), (0.44f, VoxelPalette.CarWhite),
                (0.52f, VoxelPalette.Amber), (ConeHeight, (byte)0) };
            for (int i = 0; i + 1 < bands.Length; i++)
            {
                float y0 = bands[i].Item1, y1 = bands[i + 1].Item1;
                PolyShapes.Lathe(b, 0, 0, new[] { (y0, R(y0)), (y1, R(y1)) }, 12, TrimPart, bands[i].Item2);
            }
            PolyShapes.Lathe(b, 0, 0, new[] { (ConeHeight, r1), (ConeHeight, r1 - 0.012f), (ConeHeight - ConeHollow, r1 - 0.02f) }, 12, TrimPart, VoxelPalette.Amber, 0.75f);
            PolyShapes.Disc(b, 0, ConeHeight - ConeHollow, 0, r1 - 0.02f, 12, true, TrimPart, VoxelPalette.Ink);
            return b.Build(BuildingModels.PaletteUv);
        }

        // Works barrier 2.2 m along X: two boards with diagonal red and white stripes in front of square posts on tapered
        // feet, an amber warning lamp (glowing) on the left post, closed underneath by a dark cap.
        public static PolyMesh WorksBarrier()
        {
            var b = NewBuilder();
            foreach (float x in new[] { -0.9f, 0.9f })
            {
                PolyShapes.Block(b, Footprint(x, 0, 0, 0.2f, 0.2f), Footprint(x, 0, 0.1f, 0.13f, 0.11f), TrimPart, VoxelPalette.MetalDark);
                b.Box(new V3(x - 0.03f, 0.1f, -0.03f), new V3(x + 0.03f, BarrierLampY, 0.03f), 0.008f, TrimPart, VoxelPalette.Metal);
            }
            foreach (var (bottom, top) in BarrierBoards) Board(b, -1.1f, 1.1f, bottom, top);
            b.Box(new V3(-0.945f, BarrierLampY, -0.045f), new V3(-0.855f, BarrierLampY + 0.025f, 0.045f), 0.006f, TrimPart, VoxelPalette.Ink);
            const float lampFoot = BarrierLampY + 0.025f, r = BarrierLampRadius;
            PolyShapes.Lathe(b, -0.9f, 0, new[] { (lampFoot, r), (1.085f, r), (1.1f, 0.03f), (1.1f, 0) }, 10, GlowPart, VoxelPalette.SignalAmber);
            PolyShapes.Disc(b, -0.9f, lampFoot, 0, r, 10, false, TrimPart, VoxelPalette.Ink);
            b.Box(new V3(0.865f, BarrierLampY, -0.035f), new V3(0.935f, BarrierLampY + 0.02f, 0.035f), 0.006f, TrimPart, VoxelPalette.MetalDark);
            return b.Build(BuildingModels.PaletteUv);
        }
        // Board between x0 and x1, bottom to top: both faces striped red and white along x + y, white edges.
        private static void Board(PolyBuilder b, float x0, float x1, float bottom, float top)
        {
            int group = b.Group; b.Group = b.NextGroup();
            int first = (int)Math.Floor((x0 + bottom) / StripeWidth), last = (int)Math.Ceiling((x1 + top) / StripeWidth);
            for (int k = first; k < last; k++)
            {
                var band = new List<(float x, float y)> { (x0, bottom), (x1, bottom), (x1, top), (x0, top) };
                band = Clip(band, 1, 1, k * StripeWidth); band = Clip(band, -1, -1, -(k + 1) * StripeWidth);
                if (band.Count < 3) continue;
                byte color = (k % 2 + 2) % 2 == 0 ? VoxelPalette.CarRed : VoxelPalette.CarWhite;
                foreach (float z in new[] { BoardFront, BoardBack })
                {
                    var face = new List<V3>();
                    foreach (var (x, y) in band) face.Add(new V3(x, y, z));
                    b.Polygon(face, new V3(0, 0, z < BoardBack ? -1 : 1), TrimPart, color);
                }
            }
            byte edge = VoxelPalette.CarWhite;
            b.Quad(new V3(x0, top, BoardFront), new V3(x1, top, BoardFront), new V3(x1, top, BoardBack), new V3(x0, top, BoardBack), new V3(0, 1, 0), TrimPart, edge);
            b.Quad(new V3(x0, bottom, BoardFront), new V3(x1, bottom, BoardFront), new V3(x1, bottom, BoardBack), new V3(x0, bottom, BoardBack), new V3(0, -1, 0), TrimPart, edge, 0.85f);
            foreach (float x in new[] { x0, x1 })
                b.Quad(new V3(x, bottom, BoardFront), new V3(x, top, BoardFront), new V3(x, top, BoardBack), new V3(x, bottom, BoardBack), new V3(x, 0, 0), TrimPart, edge);
            b.Group = group;
        }
        // The part of a convex polygon where nx·x + ny·y ≥ c (Sutherland–Hodgman).
        private static List<(float x, float y)> Clip(List<(float x, float y)> polygon, float nx, float ny, float c)
        {
            var kept = new List<(float x, float y)>();
            for (int i = 0; i < polygon.Count; i++)
            {
                var p = polygon[i]; var q = polygon[(i + 1) % polygon.Count];
                float dp = nx * p.x + ny * p.y - c, dq = nx * q.x + ny * q.y - c;
                if (dp >= 0) kept.Add(p);
                if (dp >= 0 != dq >= 0) { float t = dp / (dp - dq); kept.Add((p.x + (q.x - p.x) * t, p.y + (q.y - p.y) * t)); }
            }
            // Drop repeated corners where a cut runs through one.
            var clean = new List<(float x, float y)>();
            foreach (var p in kept)
                if (clean.Count == 0 || Math.Abs(clean[clean.Count - 1].x - p.x) + Math.Abs(clean[clean.Count - 1].y - p.y) > 1e-5f) clean.Add(p);
            if (clean.Count > 1 && Math.Abs(clean[0].x - clean[clean.Count - 1].x) + Math.Abs(clean[0].y - clean[clean.Count - 1].y) <= 1e-5f) clean.RemoveAt(clean.Count - 1);
            return clean;
        }

        // Refuse container 1.2 × 1.6 m on four castors: a green bin a little narrower at the foot with ribs down the long
        // sides, a rim, two dark lids meeting over the middle and a handle at the front.
        public static PolyMesh Dumpster()
        {
            var b = NewBuilder();
            foreach (float x in new[] { -0.4f, 0.4f })
                foreach (float z in new[] { -0.6f, 0.6f })
                {
                    PolyShapes.Limb(b, new V3(x - 0.035f, 0.085f, z), new V3(x + 0.035f, 0.085f, z), 0.085f, 0.085f, 8, TrimPart, VoxelPalette.Tire);
                    b.Box(new V3(x - 0.05f, 0.14f, z - 0.05f), new V3(x + 0.05f, 0.21f, z + 0.05f), 0.008f, TrimPart, VoxelPalette.MetalDark);
                }
            const float y0 = 0.2f, y1 = 0.96f, x0 = 0.53f, x1 = 0.585f, z0 = 0.68f, z1 = 0.745f;
            PolyShapes.Block(b, Footprint(0, 0, y0, x0, z0), Footprint(0, 0, y1, x1, z1), TrimPart, VoxelPalette.CarGreen);
            float SideX(float y) => x0 + (x1 - x0) * (y - y0) / (y1 - y0);
            foreach (float s in new[] { -1f, 1f })
                foreach (float z in new[] { -0.45f, -0.15f, 0.15f, 0.45f })
                {
                    float ya = 0.27f, yb = 0.9f, xa = SideX(ya), xb = SideX(yb);
                    PolyShapes.Block(b,
                        new[] { new V3(s * (xa - 0.01f), ya, z - 0.025f), new V3(s * (xa + 0.018f), ya, z - 0.025f), new V3(s * (xa + 0.018f), ya, z + 0.025f), new V3(s * (xa - 0.01f), ya, z + 0.025f) },
                        new[] { new V3(s * (xb - 0.01f), yb, z - 0.025f), new V3(s * (xb + 0.018f), yb, z - 0.025f), new V3(s * (xb + 0.018f), yb, z + 0.025f), new V3(s * (xb - 0.01f), yb, z + 0.025f) },
                        TrimPart, VoxelPalette.CarGreen, 0.86f);
                }
            b.Box(new V3(-0.6f, 0.94f, -0.77f), new V3(0.6f, DumpsterLidY, 0.77f), 0.012f, TrimPart, VoxelPalette.CarGreen, 0.94f);
            foreach (float s in new[] { -1f, 1f })
                PolyShapes.Block(b,
                    new[] { new V3(-0.6f, DumpsterLidY, s * 0.006f), new V3(0.6f, DumpsterLidY, s * 0.006f), new V3(0.6f, DumpsterLidY, s * 0.795f), new V3(-0.6f, DumpsterLidY, s * 0.795f) },
                    new[] { new V3(-0.55f, 1.1f, s * 0.05f), new V3(0.55f, 1.1f, s * 0.05f), new V3(0.55f, 1.1f, s * 0.7f), new V3(-0.55f, 1.1f, s * 0.7f) },
                    TrimPart, VoxelPalette.MetalDark);
            // The handle: a bar on two stand-offs over the front face.
            float front = -(z0 + (z1 - z0) * (0.8f - y0) / (y1 - y0));
            PolyShapes.Limb(b, new V3(-0.32f, 0.8f, -0.775f), new V3(0.32f, 0.8f, -0.775f), 0.02f, 0.02f, 6, TrimPart, VoxelPalette.Metal);
            foreach (float x in new[] { -0.28f, 0.28f })
                b.Box(new V3(x - 0.015f, 0.785f, -0.775f), new V3(x + 0.015f, 0.815f, front + 0.01f), 0, TrimPart, VoxelPalette.Metal);
            return b.Build(BuildingModels.PaletteUv);
        }

        // Rectangle round (cx, cz) at height y, half-sizes hx and hz, its corners turning the same way for every call.
        private static V3[] Footprint(float cx, float cz, float y, float hx, float hz)
            => new[] { new V3(cx - hx, y, cz - hz), new V3(cx + hx, y, cz - hz), new V3(cx + hx, y, cz + hz), new V3(cx - hx, y, cz + hz) };
    }
}
