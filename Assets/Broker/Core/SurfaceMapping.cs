using System;

namespace Broker.Core
{
    public readonly struct SurfaceUv
    {
        public float U { get; } public float V { get; }
        public SurfaceUv(float u, float v) { U = u; V = v; }
    }

    // Coordinates on a unit cube become metres before tiling. A long wall must
    // repeat its material, not stretch one image across the entire building.
    public static class SurfaceMapping
    {
        public static SurfaceUv Box(float x, float y, float z, int normalAxis,
            float width, float height, float depth, float metresPerTile)
        {
            if (normalAxis < 0 || normalAxis > 2) throw new ArgumentOutOfRangeException(nameof(normalAxis));
            if (!Positive(width) || !Positive(height) || !Positive(depth) || !Positive(metresPerTile))
                throw new ArgumentOutOfRangeException(nameof(metresPerTile));
            if (!Finite(x) || !Finite(y) || !Finite(z)) throw new ArgumentOutOfRangeException(nameof(x));
            if (normalAxis == 0) return new SurfaceUv(z * depth / metresPerTile, y * height / metresPerTile);
            if (normalAxis == 1) return new SurfaceUv(x * width / metresPerTile, z * depth / metresPerTile);
            return new SurfaceUv(x * width / metresPerTile, y * height / metresPerTile);
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static bool Positive(float value) { return Finite(value) && value > 0; }
    }
}
