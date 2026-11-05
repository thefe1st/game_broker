using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Broker.Core
{
    public enum SceneryKind
    {
        RoundTree, Conifer, StreetLamp, Bin, Cone, Bollard,
        Planter, PlazaBench, Bench, Dumpster, FlowerBed, WorksBarrier,
        Hedge, Lawn, RostralColumn, Kiosk, ExchangeColumn,
        CafeCounter, CafeShelf, CafeTable, CafeChair, MenuBoard, PendantLamp,
    }
    public sealed class SceneryItem
    {
        public SceneryKind Kind { get; } public float X { get; } public float Z { get; } public float Yaw { get; } public int Seed { get; }
        // Only props inside the walkable district get colliders; everything else is decoration.
        public bool Solid { get; }
        // Ground footprint in world axes (after the yaw) and the floor height under the prop above CityBlockPlan.GroundY.
        public float HalfX { get; } public float HalfZ { get; } public float BaseY { get; }
        public CityRect Footprint => CityRect.Around(X, Z, HalfX, HalfZ);
        // Wall-mounted or hanging: no footprint on the floor.
        public bool Overhead => Kind == SceneryKind.MenuBoard || Kind == SceneryKind.PendantLamp;
        internal SceneryItem(SceneryKind kind, float x, float z, float yaw, int seed, bool solid, float halfX, float halfZ, float baseY)
        { Kind = kind; X = x; Z = z; Yaw = yaw; Seed = seed; Solid = solid; HalfX = halfX; HalfZ = halfZ; BaseY = baseY; }
    }
    public sealed class SkylineBlock
    {
        public float X { get; } public float Z { get; } public float Width { get; } public float Depth { get; } public float Height { get; }
        public int Shade { get; }
        internal SkylineBlock(float x, float z, float width, float depth, float height, int shade)
        { X = x; Z = z; Width = width; Depth = depth; Height = height; Shade = shade; }
    }

    // Voxel dressing of both quarters: trees, lamps, street furniture, the Exchange square and colonnade, the café
    // interior, the tunnels at the ends of the boulevard and of the highway and a hazy boxy skyline. Local coordinates match CityBlockPlan
    // and CityDistrictPlan; the scene builds every prop and collider from this list.
    public static class CityScenery
    {
        public const float FarClip = 420f, FogStart = 20f, FogEnd = 250f;
        public const float TerrainCenterZ = 45f, TerrainSize = 860f;
        // Boulevard tunnels: portal faces at ±TunnelPortalX, dark mouths over the road, embankments around them.
        public const float TunnelPortalX = 80f, TunnelEndX = 97f, TunnelMinZ = 28f, TunnelMaxZ = 42f, TunnelHeight = 6.5f;
        public const float EmbankmentEndX = 100f, EmbankmentMinZ = 16f, EmbankmentMaxZ = 54f, EmbankmentHeight = 10f;
        // Highway tunnels on the same portal line: one wide mouth over both carriageways and the median, embankments of
        // their own around it.
        public const float HighwayTunnelMinZ = CityRoads.HighwaySouthZ - 0.5f, HighwayTunnelMaxZ = CityRoads.HighwayNorthZ + 0.5f;
        public const float HighwayBankMinZ = HighwayTunnelMinZ - 12, HighwayBankMaxZ = HighwayTunnelMaxZ + 12;
        // Front edge of the Exchange roof over the colonnade; the hall walls start at the podium's hall footprint.
        public const float ExchangeRoofZ = 57.25f;
        private const float Terrain = CityBlockPlan.TerrainTopY - CityBlockPlan.GroundY;
        // Interactive plaza boards and the street elevator are built by the scene; their footprints still block walkers.
        public static readonly CityRect MapStand = CityRect.Around(-3, 19, 1.15f, 0.35f), CompanyStand = CityRect.Around(4, 19, 1.15f, 0.35f);
        public static readonly CityRect Elevator = CityRect.Around(0, 8.2f, 1.25f, 0.2f);
        public static ReadOnlyCollection<SceneryItem> Props { get; } = Array.AsReadOnly(BuildProps());
        // Floor footprints of every standing prop, for the walkers' routes.
        public static ReadOnlyCollection<CityRect> Obstacles { get; } = Array.AsReadOnly(BuildObstacles());
        public static ReadOnlyCollection<SkylineBlock> Skyline { get; } = Array.AsReadOnly(BuildSkyline());

        private static void Half(SceneryKind kind, out float halfX, out float halfZ)
        {
            switch (kind)
            {
                case SceneryKind.Bollard: halfX = halfZ = 0.15f; break;
                case SceneryKind.Planter: halfX = 1.125f; halfZ = 0.625f; break;
                case SceneryKind.PlazaBench: halfX = 1.5f; halfZ = 0.375f; break;
                case SceneryKind.Bench: halfX = 1.2f; halfZ = 0.4f; break;
                case SceneryKind.Dumpster: halfX = 0.6f; halfZ = 0.8f; break;
                case SceneryKind.FlowerBed: halfX = 1.2f; halfZ = 2.5f; break;
                case SceneryKind.WorksBarrier: halfX = 1.1f; halfZ = 0.2f; break;
                case SceneryKind.Lawn: halfX = 3f; halfZ = 2.5f; break;
                case SceneryKind.RostralColumn: halfX = halfZ = 1.3f; break;
                case SceneryKind.Kiosk: halfX = 1.1f; halfZ = 0.8f; break;
                case SceneryKind.ExchangeColumn: halfX = halfZ = 0.6f; break;
                case SceneryKind.CafeCounter: halfX = 1.6f; halfZ = 0.35f; break;
                case SceneryKind.CafeShelf: halfX = 1.5f; halfZ = 0.2f; break;
                case SceneryKind.CafeTable: halfX = halfZ = 0.4f; break;
                case SceneryKind.MenuBoard: halfX = 0.6f; halfZ = 0.05f; break;
                case SceneryKind.PendantLamp: halfX = halfZ = 0.15f; break;
                default: halfX = halfZ = 0.25f; break;   // trunks, lamp bases, bins, cones, chairs
            }
        }
        private static SceneryItem[] BuildProps()
        {
            var items = new List<SceneryItem>();
            void Add(SceneryKind kind, float x, float z, float yaw, bool solid, float length = 0)
            {
                Half(kind, out float hx, out float hz);
                if (length > 0) hx = length / 2;
                if (Math.Abs(Math.Abs(yaw % 180) - 90) < 1) { float t = hx; hx = hz; hz = t; }
                float baseY = CityDistrictPlan.CanWalk(x, z) ? CityDistrictPlan.FloorY(x, z) : Terrain;
                items.Add(new SceneryItem(kind, x, z, yaw, items.Count + 1, solid, hx, hz, baseY));
            }
            // Western square: a loose ring of trees around the benches and planted beds.
            Add(SceneryKind.RoundTree, -42.2f, 10.6f, 0, true); Add(SceneryKind.Conifer, -36f, 10.4f, 0, true);
            Add(SceneryKind.RoundTree, -26.5f, 10.8f, 90, true); Add(SceneryKind.Conifer, -42.4f, 26.4f, 0, true);
            Add(SceneryKind.RoundTree, -35.5f, 26.6f, 180, true); Add(SceneryKind.Conifer, -15.9f, 26.2f, 0, true);
            Add(SceneryKind.FlowerBed, -40f, 19f, 0, true); Add(SceneryKind.FlowerBed, -23f, 19f, 0, true);
            foreach (float x in new[] { -37f, -25f, 27f, 37f }) Add(SceneryKind.Bench, x, 23f, 0, true);
            foreach (float x in new[] { -19f, 41f }) foreach (float z in new[] { 10f, 23f }) Add(SceneryKind.StreetLamp, x, z, -90, true);
            // Plaza: corner trees, benches, and planters with lamps along the kerb that leave the crossing approach open.
            Add(SceneryKind.RoundTree, -13f, 10.6f, 0, true); Add(SceneryKind.RoundTree, 13f, 10.6f, 90, true);
            foreach (float x in new[] { -9f, 9f }) Add(SceneryKind.PlazaBench, x, 17f, 0, true);
            foreach (float x in new[] { -13f, -9f, -5f, 5f, 9f, 13f })
            { Add(SceneryKind.Planter, x, 25.5f, 0, true); Add(SceneryKind.StreetLamp, x, 27.9f, 0, true); }
            // Business street.
            Add(SceneryKind.RoundTree, 22f, 26.6f, 0, true); Add(SceneryKind.RoundTree, 32f, 26.6f, 270, true);
            // Small street props and the service works in the eastern side street.
            Add(SceneryKind.Bin, -31f, 23.2f, 0, true); Add(SceneryKind.Bin, 32f, 23.2f, 0, true);
            Add(SceneryKind.Bin, -6.5f, 17f, 0, true); Add(SceneryKind.Bin, 6.5f, 17f, 0, true);
            foreach (float x in new[] { -15.8f, 15.8f }) foreach (float z in new[] { 9.2f, 27.6f }) Add(SceneryKind.Bollard, x, z, 0, true);
            Add(SceneryKind.Dumpster, -19.2f, -7f, 0, true); Add(SceneryKind.Dumpster, 40.8f, -7f, 0, true);
            Add(SceneryKind.WorksBarrier, 41.5f, -10f, 0, true);
            Add(SceneryKind.Cone, 40.3f, -9.3f, 0, false); Add(SceneryKind.Cone, 42.7f, -9.3f, 30, false); Add(SceneryKind.Cone, 41.5f, -8.7f, 60, false);
            // Café «Бык и медведь»: counter and shelf at the back, two tables, a menu and pendant lamps.
            Add(SceneryKind.CafeCounter, 24.9f, -7.4f, 180, true); Add(SceneryKind.CafeShelf, 24.9f, -9.3f, 180, false);
            Add(SceneryKind.CafeTable, 25.5f, -3.4f, 0, true); Add(SceneryKind.CafeTable, 22.1f, -5.4f, 0, true);
            Add(SceneryKind.CafeChair, 25.5f, -2.85f, 0, true); Add(SceneryKind.CafeChair, 25.5f, -3.95f, 180, true);
            Add(SceneryKind.CafeChair, 22.1f, -4.85f, 0, true); Add(SceneryKind.CafeChair, 22.1f, -5.95f, 180, true);
            Add(SceneryKind.MenuBoard, 22.6f, -9.45f, 180, false);
            foreach (float x in new[] { 23.8f, 25.0f, 26.0f }) Add(SceneryKind.PendantLamp, x, -7.4f, 0, false);
            // North pavement: lamps on the kerb (none at the crossing), hedges in the gaps between the towers, trees behind.
            foreach (float x in new[] { -40f, -20f, 20f, 40f }) Add(SceneryKind.StreetLamp, x, 41.8f, 180, true);
            foreach (var (x, length) in new[] { (-42.75f, 2.5f), (-30.25f, 6.5f), (-17f, 2f), (17f, 2f), (29.75f, 5.5f), (43.25f, 1.5f) })
                Add(SceneryKind.Hedge, x, 44.9f, 0, false, length);
            foreach (float x in new[] { -16.6f, 16.6f }) Add(SceneryKind.Hedge, x, 49.9f, 90, false, 9.2f);
            Add(SceneryKind.RoundTree, -30.2f, 46.1f, -211, false); Add(SceneryKind.RoundTree, 29.8f, 46.1f, 209, false);
            // Exchange square: rostral columns, lawns, benches along the central walk, a kiosk and corner trees.
            foreach (float x in new[] { -12.8f, 12.8f }) Add(SceneryKind.RostralColumn, x, 50f, 0, true);
            foreach (float x in new[] { -6.5f, 6.5f }) Add(SceneryKind.Lawn, x, 50f, 0, true);
            Add(SceneryKind.Bench, -2.7f, 50f, -90, true); Add(SceneryKind.Bench, 2.7f, 50f, 90, true);
            Add(SceneryKind.Kiosk, -13.6f, 45.7f, 0, true);
            foreach (float x in new[] { -14.6f, 14.6f }) Add(SceneryKind.RoundTree, x, 54f, x * 9, true);
            // Colonnade of the Exchange on the podium: six columns in front, four along each side.
            foreach (float x in new[] { -10f, -6.3f, -2.6f, 2.6f, 6.3f, 10f }) Add(SceneryKind.ExchangeColumn, x, 58.2f, 0, true);
            foreach (float x in new[] { -11.6f, 11.6f }) foreach (float z in new[] { 61.6f, 65f, 68.4f, 71.8f }) Add(SceneryKind.ExchangeColumn, x, z, 0, true);
            return items.ToArray();
        }
        private static CityRect[] BuildObstacles()
        {
            var list = new List<CityRect>();
            foreach (var item in Props) if (!item.Overhead) list.Add(item.Footprint);
            list.Add(MapStand); list.Add(CompanyStand); list.Add(Elevator);
            return list.ToArray();
        }

        private static SkylineBlock[] BuildSkyline()
        {
            var blocks = new List<SkylineBlock>();
            Ring(blocks, 92, -134, 172, 10, 30, 18, 1, true);
            Ring(blocks, 205, -190, 250, 18, 78, 22, 2, false);
            return blocks.ToArray();
        }
        // Blocks along a rectangle: half-width in X, south and north edges in Z. The inner ring leaves the
        // embankments of both tunnels free on its east and west edges.
        private static void Ring(List<SkylineBlock> blocks, float halfX, float south, float north, float minH, float maxH, float step, int seed, bool tunnelGap)
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
                    bool gap = tunnelGap && !alongX && (z + width / 2 > EmbankmentMinZ - 2 && z - width / 2 < EmbankmentMaxZ + 2
                        || z + width / 2 > HighwayBankMinZ - 2 && z - width / 2 < HighwayBankMaxZ + 2);
                    if (!gap) blocks.Add(alongX ? new SkylineBlock(x, z, width, depth, height, index % 3) : new SkylineBlock(x, z, depth, width, height, index % 3));
                    t += width + 2 + r1 * step * 0.35f;
                }
            }
            Edge(-halfX, south, halfX, south); Edge(halfX, south, halfX, north);
            Edge(halfX, north, -halfX, north); Edge(-halfX, north, -halfX, south);
        }
    }
}
