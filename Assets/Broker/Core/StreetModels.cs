using System;

namespace Broker.Core
{
    // Street greenery and furniture as smooth low-poly meshes in the manner of Schedule I, in place of the voxel models and
    // on the building parts and materials (BuildingModels): the clipped hedge here; trees, conifers, shrubs, planters and
    // lawns in StreetModels.Green.cs; lamps, benches and bins in StreetModels.Furniture.cs.
    public static partial class StreetModels
    {
        public static readonly WallLook HedgeLook = new WallLook("leaves", 0xFFFFFF);
        // The domed ends take HedgeEnd of the length at each end.
        public const float HedgeHeight = 1.25f, HedgeDepth = 0.5f, HedgeRing = 0.33f, HedgeEnd = 0.11f;
        // Cross-section (z across, y up) from the front foot over the rounded top to the back foot: the foot tucks in a
        // little, the sides bulge slightly, the top corners round off.
        private static readonly (float z, float y)[] HedgeProfile =
        {
            (-0.22f, 0), (-0.25f, 0.18f), (-0.255f, 0.6f), (-0.25f, 1.08f), (-0.225f, 1.19f), (-0.15f, 1.245f), (-0.05f, 1.25f),
            (0.05f, 1.25f), (0.15f, 1.245f), (0.225f, 1.19f), (0.25f, 1.08f), (0.255f, 0.6f), (0.25f, 0.18f), (0.22f, 0),
        };
        // Clipped hedge along X, 1.25 m high and 0.5 m deep, `length` long from tip to tip: rings of the profile about
        // every third of a metre with soft bumps from the seed (smaller at the foot) and domed ends. On the "leaves" wall
        // material; pivot at the middle of the bottom, the foot in contact shade.
        public static PolyMesh Hedge(float length, int seed)
        {
            if (!(length >= 0.25f) || length > 100) throw new ArgumentOutOfRangeException(nameof(length));
            const int wall = (int)BuildingPart.Wall;
            var b = new PolyBuilder(BuildingModels.PartCount, 50, wall, VoxelSurfaces.MetresPerTile) { Ambient = BuildingModels.Contact };
            float body = length - 2 * HedgeEnd;
            int n = Math.Max(2, (int)Math.Round(body / HedgeRing)), points = HedgeProfile.Length;
            const float middle = 0.6f;
            var rings = new V3[n + 1, points];
            for (int i = 0; i <= n; i++)
            {
                float x = -body / 2 + body * i / n;
                bool end = i == 0 || i == n;
                for (int j = 0; j < points; j++)
                {
                    var (z, y) = HedgeProfile[j];
                    // Bumps outward in the cross-section: away from its middle at mid-height, up over the top.
                    float ny = Math.Max(0, y - middle), len = (float)Math.Sqrt(z * z + ny * ny);
                    float bump = 0.035f * (2 * VoxelNoise.Value(i, j, 3, seed) - 1) * Math.Min(1, y / 0.35f);
                    if (len > 1e-4f) { z += z / len * bump; y += ny / len * bump; }
                    if (end) { z *= 0.82f; y *= 0.97f; }
                    rings[i, j] = new V3(x, y, z);
                }
            }
            var centre = new V3(0, middle, 0);
            for (int i = 0; i < n; i++)
                for (int j = 0; j + 1 < points; j++)
                {
                    V3 a = rings[i, j], c = rings[i + 1, j], e = rings[i + 1, j + 1], g = rings[i, j + 1];
                    var mid = (a + c + e + g) * 0.25f;
                    b.Quad(a, c, e, g, new V3(0, mid.Y - centre.Y, mid.Z), wall, 0);
                }
            // Domed ends: a smaller ring a little further out, then a point.
            foreach (int i in new[] { 0, n })
            {
                float sign = i == 0 ? -1 : 1, xe = rings[i, 0].X;
                var inner = new V3[points];
                for (int j = 0; j < points; j++) { var p = rings[i, j]; inner[j] = new V3(xe + sign * 0.07f, 0.15f + p.Y * 0.75f, p.Z * 0.6f); }
                var tip = new V3(xe + sign * HedgeEnd, middle, 0);
                for (int j = 0; j + 1 < points; j++)
                {
                    var mid = (rings[i, j] + rings[i, j + 1] + inner[j + 1] + inner[j]) * 0.25f;
                    b.Quad(rings[i, j], rings[i, j + 1], inner[j + 1], inner[j], new V3(sign * 0.5f, mid.Y - centre.Y, mid.Z), wall, 0);
                    b.Triangle(tip, inner[j], inner[j + 1], new V3(sign, 0, 0), wall, 0);
                }
                b.Triangle(tip, inner[points - 1], inner[0], new V3(sign, -0.5f, 0), wall, 0);
            }
            return b.Build(BuildingModels.PaletteUv);
        }
    }
}
