using System;
using System.Collections.Generic;

namespace Broker.Core
{
    // Street lamps, benches and bins in palette trim: dark painted metal, wood slats, a lamp glass that glows. Pivots and
    // bounds as the voxel models they replace (VoxelProps.StreetLamp, Bench, Bin); fronts face −Z.
    public static partial class StreetModels
    {
        // The lamp's glass faces down at LampGlassY, its middle LampGlassZ out along the arm (the night light hangs under it).
        public const float LampHeight = 5.25f, LampReach = 1.5f, LampGlassY = 4.775f, LampGlassZ = 1.15f;
        public const float SeatHeight = 0.455f, BinHeight = 0.875f, BinRadius = 0.25f;
        // These models have no wall part; PlaceBuilding still takes a look.
        public static readonly WallLook FurnitureLook = new WallLook("stucco", 0xFFFFFF);

        // Street lamp 5.25 m: an octagonal foot with a collar, a slim pole bending over into an arm towards +Z and a flat
        // head with its glass underneath. Pivot at the pole.
        public static PolyMesh StreetLamp()
        {
            var b = NewBuilder();
            byte paint = VoxelPalette.MetalDark;
            PolyShapes.Lathe(b, 0, 0, new[] { (0f, 0.2f), (0.05f, 0.2f), (0.09f, 0.165f), (0.42f, 0.135f), (0.47f, 0.095f) }, 8, TrimPart, paint);
            PolyShapes.Lathe(b, 0, 0, new[] { (0.45f, 0.1f), (0.58f, 0.1f) }, 8, TrimPart, VoxelPalette.Metal, capTop: true);
            PolyShapes.Lathe(b, 0, 0, new[] { (0.58f, 0.075f), (4.6f, 0.058f) }, 8, TrimPart, paint);
            var arm = new List<V3>();
            const float bend = 0.4f, top = 4.6f;
            for (int i = 0; i <= 6; i++)
            {
                double t = Math.PI / 2 * i / 6;
                arm.Add(new V3(0, top + bend * (float)Math.Sin(t), bend - bend * (float)Math.Cos(t)));
            }
            arm.Add(new V3(0, top + bend, 1.05f));
            PolyShapes.Sweep(b, arm, 0.058f, 0.042f, 8, new V3(1, 0, 0), TrimPart, paint);
            float y0 = LampGlassY + 0.005f, y1 = 5.08f;
            PolyShapes.Block(b,
                new[] { new V3(-0.16f, y0, 0.82f), new V3(0.16f, y0, 0.82f), new V3(0.17f, y0, 1.48f), new V3(-0.17f, y0, 1.48f) },
                new[] { new V3(-0.12f, y1, 0.86f), new V3(0.12f, y1, 0.86f), new V3(0.13f, y1, 1.38f), new V3(-0.13f, y1, 1.38f) }, TrimPart, paint);
            b.Quad(new V3(-0.12f, LampGlassY, 0.88f), new V3(0.12f, LampGlassY, 0.88f), new V3(0.13f, LampGlassY, 1.42f), new V3(-0.13f, LampGlassY, 1.42f),
                new V3(0, -1, 0), GlowPart, VoxelPalette.LampWarm);
            return b.Build(BuildingModels.PaletteUv);
        }

        // Bench `length` long on cast-iron side frames: four seat slats; with a back, three tilted back slats and armrests,
        // 0.7 m deep and 0.9 m high, otherwise 0.6 m deep. The sitter faces −Z.
        public static PolyMesh Bench(float length, bool back)
        {
            if (!(length >= 0.8f) || length > 6) throw new ArgumentOutOfRangeException(nameof(length));
            var b = NewBuilder();
            float half = length / 2, front = back ? -0.35f : -0.3f, rear = back ? 0.15f : 0.2f;
            var frames = new List<float> { -(half - (back ? 0.1f : 0.25f)), half - (back ? 0.1f : 0.25f) };
            if (length > 2.6f) frames.Add(0);
            foreach (float x in frames) SideFrame(b, x, front, rear, back);
            const int slats = 4; const float gap = 0.02f, thick = 0.045f;
            float width = (rear - front - 0.02f - gap * (slats - 1)) / slats;
            for (int i = 0; i < slats; i++)
            {
                float z0 = front + 0.01f + i * (width + gap);
                b.Box(new V3(-half, SeatHeight - thick, z0), new V3(half, SeatHeight, z0 + width), 0.012f, TrimPart, VoxelPalette.Wood, i % 2 == 0 ? 1 : 0.93f);
            }
            if (back)
                for (int i = 0; i < 3; i++)
                {
                    // Along the back posts (see SideFrame), just in front of them.
                    float y = 0.53f + 0.13f * i, z = BackZ(y) - 0.045f;
                    Slat(b, half, y, z, 0.1f, 0.035f, VoxelPalette.Wood, i % 2 == 0 ? 0.96f : 1);
                }
            return b.Build(BuildingModels.PaletteUv);
        }
        // The back posts lean from (0.41, 0.14) to (0.88, 0.32) in (y, z).
        private static float BackZ(float y) => 0.14f + (y - 0.41f) * (0.18f / 0.47f);
        private static void SideFrame(PolyBuilder b, float x, float front, float rear, bool back)
        {
            const float t = 0.025f;
            byte iron = VoxelPalette.MetalDark;
            Bar(b, x, (0, front + 0.05f), (back ? 0.62f : SeatHeight - 0.045f, front + 0.07f), 0.05f, t, iron);
            Bar(b, x, (0, rear + (back ? 0.08f : -0.05f)), (0.41f, rear - (back ? 0.01f : 0.07f)), 0.05f, t, iron);
            Bar(b, x, (0.39f, front + 0.04f), (0.39f, rear), 0.045f, t, iron);
            if (back)
            {
                Bar(b, x, (0.41f, BackZ(0.41f)), (0.88f, BackZ(0.88f)), 0.05f, t, iron);
                Bar(b, x, (0.635f, front + 0.0f), (0.635f, BackZ(0.635f)), 0.045f, 0.035f, iron);
            }
            else Bar(b, x, (0.12f, front + 0.06f), (0.12f, rear - 0.06f), 0.035f, t, iron);
        }
        // Flat bar of an iron frame at x in the (y, z) plane from p0 to p1, `width` across, `half` either side of x. Its ends
        // are cut level when it stands (legs on the ground, posts) and plumb when it lies (rails, armrests).
        private static void Bar(PolyBuilder b, float x, (float y, float z) p0, (float y, float z) p1, float width, float half, byte color)
        {
            bool standing = Math.Abs(p1.y - p0.y) >= Math.Abs(p1.z - p0.z);
            float ny = standing ? 0 : width / 2, nz = standing ? width / 2 : 0;
            V3[] End((float y, float z) p) => new[]
            {
                new V3(x - half, p.y - ny, p.z - nz), new V3(x + half, p.y - ny, p.z - nz), new V3(x + half, p.y + ny, p.z + nz), new V3(x - half, p.y + ny, p.z + nz),
            };
            PolyShapes.Block(b, End(p0), End(p1), TrimPart, color);
        }
        // Slat along X across the whole length, centred at (y, z) in the plane of the back, `width` up it and `thick` through.
        private static void Slat(PolyBuilder b, float half, float y, float z, float width, float thick, byte color, float shade)
        {
            float uy = 0.47f, uz = 0.18f, len = (float)Math.Sqrt(uy * uy + uz * uz);
            uy /= len; uz /= len;
            float ny = -uz, nz = uy;
            V3[] End(float x) => new[]
            {
                new V3(x, y - uy * width / 2 - ny * thick / 2, z - uz * width / 2 - nz * thick / 2),
                new V3(x, y + uy * width / 2 - ny * thick / 2, z + uz * width / 2 - nz * thick / 2),
                new V3(x, y + uy * width / 2 + ny * thick / 2, z + uz * width / 2 + nz * thick / 2),
                new V3(x, y - uy * width / 2 + ny * thick / 2, z - uz * width / 2 + nz * thick / 2),
            };
            PolyShapes.Block(b, End(-half), End(half), TrimPart, color, shade);
        }

        // Litter bin 0.5 m round, 0.875 m high: a dark drum widening a little upward with a pale band and rim, the rim
        // turning in over a dark opening.
        public static PolyMesh Bin()
        {
            var b = NewBuilder();
            const int sides = 12;
            PolyShapes.Lathe(b, 0, 0, new[] { (0f, 0.195f), (0.04f, 0.21f), (0.4f, 0.222f) }, sides, TrimPart, VoxelPalette.MetalDark);
            PolyShapes.Lathe(b, 0, 0, new[] { (0.4f, 0.222f), (0.4f, 0.232f), (0.47f, 0.232f), (0.47f, 0.224f) }, sides, TrimPart, VoxelPalette.Metal);
            PolyShapes.Lathe(b, 0, 0, new[] { (0.47f, 0.224f), (0.8f, 0.235f) }, sides, TrimPart, VoxelPalette.MetalDark);
            PolyShapes.Lathe(b, 0, 0, new[] { (0.8f, 0.235f), (0.8f, 0.245f), (0.86f, 0.248f), (BinHeight, 0.232f), (BinHeight, 0.17f), (0.84f, 0.17f) }, sides,
                TrimPart, VoxelPalette.Metal);
            PolyShapes.Disc(b, 0, 0.84f, 0, 0.17f, sides, true, TrimPart, VoxelPalette.Ink);
            return b.Build(BuildingModels.PaletteUv);
        }
    }
}
