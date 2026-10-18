using System;
using System.Collections.ObjectModel;

namespace Broker.Core
{
    public sealed class CityBuilding
    {
        public float X { get; } public float Z { get; }
        public float Width { get; } public float Depth { get; } public float Height { get; }
        public int Style { get; }
        public CityBuilding(float x, float z, float width, float depth, float height, int style)
        { X = x; Z = z; Width = width; Depth = depth; Height = height; Style = style; }
    }

    // Local coordinates relative to the procedural office. A small explorable plaza, not an open world.
    public sealed class CityBlockPlan
    {
        public const float GroundY = -8f;
        public const float SpawnX = 0f, SpawnY = GroundY + 0.05f, SpawnZ = 16f;
        public const float PlazaMinX = -15f, PlazaMaxX = 15f, PlazaMinZ = 8f, PlazaMaxZ = 28f;
        public ReadOnlyCollection<CityBuilding> Buildings { get; }
        public CityBlockPlan()
        {
            var buildings = new CityBuilding[18];
            int index = 0;
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 6; column++)
                {
                    float height = 12 + ((column * 7 + row * 11) % 9) * 3;
                    buildings[index++] = new CityBuilding(-37.5f + column * 15, 49 + row * 24,
                        8 + (column % 3), 11, height, (column + row) % 3);
                }
            Buildings = Array.AsReadOnly(buildings);
        }
        public static float TrafficX(double elapsedSeconds, int lane)
        {
            if (lane < 0 || lane > 3) throw new ArgumentOutOfRangeException(nameof(lane));
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            double distance = (elapsedSeconds * (lane % 2 == 0 ? 5.0 : 6.0) + lane * 23) % 100.0;
            return lane % 2 == 0 ? (float)(distance - 50) : (float)(50 - distance);
        }
    }
}
