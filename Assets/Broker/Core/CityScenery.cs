using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Broker.Core
{
    public enum SceneryKind { RoundTree, Conifer, StreetLamp, Bin, Cone, Bollard }
    public sealed class SceneryItem
    {
        public SceneryKind Kind { get; } public float X { get; } public float Z { get; } public float Yaw { get; } public int Seed { get; }
        // Only trunks and posts in the walkable district get small colliders; everything else is decoration.
        public bool Solid { get; }
        internal SceneryItem(SceneryKind kind, float x, float z, float yaw, int seed, bool solid)
        { Kind = kind; X = x; Z = z; Yaw = yaw; Seed = seed; Solid = solid; }
    }
    public sealed class SkylineBlock
    {
        public float X { get; } public float Z { get; } public float Width { get; } public float Depth { get; } public float Height { get; }
        public int Shade { get; }
        internal SkylineBlock(float x, float z, float width, float depth, float height, int shade)
        { X = x; Z = z; Width = width; Depth = depth; Height = height; Shade = shade; }
    }

    // Voxel dressing around the walkable district: trees, lamps, small street props and a hazy boxy skyline.
    // Local coordinates match CityBlockPlan and CityDistrictPlan.
    public static class CityScenery
    {
        public const float FarClip = 420f, FogStart = 30f, FogEnd = 230f;
        public const float TerrainCenterZ = 45f, TerrainSize = 470f;
        public static ReadOnlyCollection<SceneryItem> Props { get; } = Array.AsReadOnly(BuildProps());
        public static ReadOnlyCollection<SkylineBlock> Skyline { get; } = Array.AsReadOnly(BuildSkyline());

        private static SceneryItem[] BuildProps()
        {
            var items = new List<SceneryItem>();
            void Add(SceneryKind kind, float x, float z, float yaw, bool solid) => items.Add(new SceneryItem(kind, x, z, yaw, items.Count + 1, solid));
            // Western square: a loose ring of trees around the benches and beds.
            Add(SceneryKind.RoundTree, -42.2f, 10.6f, 0, true); Add(SceneryKind.Conifer, -36f, 10.4f, 0, true);
            Add(SceneryKind.RoundTree, -26.5f, 10.8f, 90, true); Add(SceneryKind.Conifer, -42.4f, 26.4f, 0, true);
            Add(SceneryKind.RoundTree, -31f, 26.6f, 180, true); Add(SceneryKind.Conifer, -21.5f, 26.4f, 0, true);
            // Plaza corners and the business street.
            Add(SceneryKind.RoundTree, -13f, 10.6f, 0, true); Add(SceneryKind.RoundTree, 13f, 10.6f, 90, true);
            Add(SceneryKind.RoundTree, 22f, 26.6f, 0, true); Add(SceneryKind.RoundTree, 32f, 26.6f, 270, true);
            // Beyond the boulevard: trees in the gaps between the first row of towers, lamps on the far kerb.
            foreach (float x in new[] { -30.2f, -15.2f, 0.5f, 14.8f, 29.8f }) Add(SceneryKind.RoundTree, x, 45.6f, x * 7, false);
            for (int i = 0; i < 5; i++) Add(SceneryKind.StreetLamp, -40 + i * 20, 41.8f, 180, false);
            // Small street props.
            Add(SceneryKind.Bin, -31f, 23.2f, 0, false); Add(SceneryKind.Bin, 32f, 23.2f, 0, false);
            Add(SceneryKind.Bin, -6.5f, 17f, 0, false); Add(SceneryKind.Bin, 6.5f, 17f, 0, false);
            Add(SceneryKind.Cone, 40.3f, -9.3f, 0, false); Add(SceneryKind.Cone, 42.7f, -9.3f, 30, false); Add(SceneryKind.Cone, 41.5f, -8.7f, 60, false);
            foreach (float x in new[] { -15.8f, 15.8f }) foreach (float z in new[] { 9.2f, 27.6f }) Add(SceneryKind.Bollard, x, z, 0, false);
            return items.ToArray();
        }

        private static SkylineBlock[] BuildSkyline()
        {
            var blocks = new List<SkylineBlock>();
            Ring(blocks, 92, -72, 172, 10, 30, 18, 1);
            Ring(blocks, 205, -150, 250, 18, 78, 22, 2);
            return blocks.ToArray();
        }
        // Blocks along a rectangle: half-width in X, south and north edges in Z.
        private static void Ring(List<SkylineBlock> blocks, float halfX, float south, float north, float minH, float maxH, float step, int seed)
        {
            int index = 0;
            void Edge(float x0, float z0, float x1, float z1)
            {
                float length = Math.Max(Math.Abs(x1 - x0), Math.Abs(z1 - z0));
                bool alongX = Math.Abs(x1 - x0) > Math.Abs(z1 - z0);
                for (float t = 0; t < length; index++)
                {
                    float r0 = VoxelNoise.Value(index, 0, 1, seed), r1 = VoxelNoise.Value(index, 1, 1, seed), r2 = VoxelNoise.Value(index, 2, 1, seed), r3 = VoxelNoise.Value(index, 3, 1, seed);
                    float width = step * (0.55f + r0 * 0.4f), depth = 10 + r1 * 14, jitter = (r2 - 0.5f) * 16;
                    float k = (t + width / 2) / length;
                    float x = x0 + (x1 - x0) * k + (alongX ? 0 : jitter), z = z0 + (z1 - z0) * k + (alongX ? jitter : 0);
                    float height = minH + (maxH - minH) * r3 * r3 + (r3 > 0.92f ? maxH * 0.35f : 0);
                    blocks.Add(alongX ? new SkylineBlock(x, z, width, depth, height, index % 3) : new SkylineBlock(x, z, depth, width, height, index % 3));
                    t += width + 2 + r1 * step * 0.35f;
                }
            }
            Edge(-halfX, south, halfX, south); Edge(halfX, south, halfX, north);
            Edge(halfX, north, -halfX, north); Edge(-halfX, north, -halfX, south);
        }
    }
}
