using System;
using System.Collections.Generic;
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

    // Local coordinates relative to the procedural office. A small explorable district, not an open world.
    // Three rows of towers north of the boulevard; the two middle towers of the first rows give way to the Exchange.
    public sealed class CityBlockPlan
    {
        public const float GroundY = -8f;
        public const float TerrainTopY = GroundY - 0.1f;
        public const float SpawnX = 0f, SpawnY = GroundY + 0.05f, SpawnZ = 16f;
        public const float PlazaMinX = -15f, PlazaMaxX = 15f, PlazaMinZ = 8f, PlazaMaxZ = 28f;
        public ReadOnlyCollection<CityBuilding> Buildings { get; }
        public CityBlockPlan()
        {
            var buildings = new List<CityBuilding>();
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 6; column++)
                {
                    if (row < 2 && (column == 2 || column == 3)) continue;
                    float height = 12 + ((column * 7 + row * 11) % 9) * 3;
                    buildings.Add(new CityBuilding(-37.5f + column * 15, (row == 0 ? 50 : 49) + row * 24,
                        8 + (column % 3), 11, height, (column + row) % 3));
                }
            Buildings = Array.AsReadOnly(buildings.ToArray());
        }
    }
}
