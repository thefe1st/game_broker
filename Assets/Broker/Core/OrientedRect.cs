using System;

namespace Broker.Core
{
    // A rectangle on the ground turned by a heading (radians from +X toward +Z): half-length along the heading, half-width
    // to its sides. The footprint of a car, or a slanted piece of road.
    public readonly struct OrientedRect
    {
        public float X { get; } public float Z { get; } public float Heading { get; }
        public float HalfLength { get; } public float HalfWidth { get; }
        public float Cos { get; } public float Sin { get; }
        public OrientedRect(float x, float z, float heading, float halfLength, float halfWidth)
        {
            if (!(halfLength >= 0) || !(halfWidth >= 0)) throw new ArgumentException("Negative rectangle size.");
            X = x; Z = z; Heading = heading; HalfLength = halfLength; HalfWidth = halfWidth;
            Cos = (float)Math.Cos(heading); Sin = (float)Math.Sin(heading);
        }
        public static OrientedRect From(CityRect rect) => new OrientedRect(rect.CenterX, rect.CenterZ, 0, rect.Width / 2, rect.Depth / 2);
        // A rectangle standing on the segment from (x0, z0) to (x1, z1) and reaching the given depth to its left.
        public static OrientedRect OnEdge(float x0, float z0, float x1, float z1, float depth)
        {
            float dx = x1 - x0, dz = z1 - z0, length = (float)Math.Sqrt(dx * dx + dz * dz);
            if (!(length > 0)) throw new ArgumentException("Empty edge.");
            float lx = -dz / length, lz = dx / length;
            return new OrientedRect((x0 + x1) / 2 + lx * depth / 2, (z0 + z1) / 2 + lz * depth / 2, (float)Math.Atan2(dz, dx), length / 2, depth / 2);
        }
        // A point in the rectangle's frame: along the heading and to the left of it.
        public void Local(float x, float z, out float along, out float side)
        {
            float dx = x - X, dz = z - Z;
            along = dx * Cos + dz * Sin; side = -dx * Sin + dz * Cos;
        }
        // A point of the rectangle's frame back in local scene coordinates.
        public void World(float along, float side, out float x, out float z)
        {
            x = X + along * Cos - side * Sin; z = Z + along * Sin + side * Cos;
        }
        public bool Contains(float x, float z, float margin = 0)
        {
            Local(x, z, out float along, out float side);
            return Math.Abs(along) <= HalfLength + margin && Math.Abs(side) <= HalfWidth + margin;
        }
        // Distance from a point to the rectangle; 0 inside.
        public float Distance(float x, float z) => Outside(x, z, out _, out _);
        // The same with the unit direction from the point toward the rectangle. Worked out in the rectangle's own frame, so
        // the direction stays exact at the edges of a large piece of road; 0 and no direction inside.
        public float Outside(float x, float z, out float towardX, out float towardZ)
        {
            Local(x, z, out float along, out float side);
            float da = Math.Max(-HalfLength, Math.Min(HalfLength, along)) - along, ds = Math.Max(-HalfWidth, Math.Min(HalfWidth, side)) - side;
            towardX = towardZ = 0;
            if (da == 0 && ds == 0) return 0;
            float distance = (float)Math.Sqrt(da * da + ds * ds), ua = da / distance, us = ds / distance;
            towardX = ua * Cos - us * Sin; towardZ = ua * Sin + us * Cos;
            return distance;
        }
        // Corners: 0 front left, 1 front right, 2 rear right, 3 rear left.
        public void Corner(int index, out float x, out float z)
        {
            if (index < 0 || index > 3) throw new ArgumentOutOfRangeException(nameof(index));
            World(index < 2 ? HalfLength : -HalfLength, index == 0 || index == 3 ? HalfWidth : -HalfWidth, out x, out z);
        }
        public CityRect Bounds
        {
            get
            {
                float ex = HalfLength * Math.Abs(Cos) + HalfWidth * Math.Abs(Sin), ez = HalfLength * Math.Abs(Sin) + HalfWidth * Math.Abs(Cos);
                return new CityRect(X - ex, Z - ez, X + ex, Z + ez);
            }
        }
        public bool Overlaps(CityRect rect) => Penetration(From(rect), out _, out _) > 0;
        // Separating-axis test of two rectangles: the smallest overlap along their four axes (≤ 0 when apart) and the unit
        // normal along which this rectangle has to move to clear the other one.
        public float Penetration(OrientedRect other, out float normalX, out float normalZ)
        {
            float best = float.MaxValue; normalX = normalZ = 0;
            float dx = other.X - X, dz = other.Z - Z;
            for (int axis = 0; axis < 4; axis++)
            {
                var owner = axis < 2 ? this : other;
                float ax = axis % 2 == 0 ? owner.Cos : -owner.Sin, az = axis % 2 == 0 ? owner.Sin : owner.Cos;
                float reach = Extent(ax, az) + other.Extent(ax, az), distance = dx * ax + dz * az;
                float overlap = reach - Math.Abs(distance);
                if (overlap < best) { best = overlap; float sign = distance > 0 ? -1 : 1; normalX = ax * sign; normalZ = az * sign; }
            }
            return best;
        }
        // Half of the rectangle's shadow on a unit axis.
        private float Extent(float ax, float az)
            => HalfLength * Math.Abs(ax * Cos + az * Sin) + HalfWidth * Math.Abs(-ax * Sin + az * Cos);
    }
}
