using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Broker.Core
{
    public enum WalkerStyle { Clerk, Banker, Casual, Coat, Courier, Student, Elder, Tourist, Analyst, Barista, Guard, Trader }

    public sealed class CityRoute
    {
        public ReadOnlyCollection<float> X { get; } public ReadOnlyCollection<float> Z { get; }
        public int Count => X.Count;
        public float Length { get; }
        private readonly float[] lengths, starts; private readonly bool[] crossing;
        internal CityRoute(params float[] points)
        {
            int n = points.Length / 2; var x = new float[n]; var z = new float[n];
            for (int i = 0; i < n; i++) { x[i] = points[i * 2]; z[i] = points[i * 2 + 1]; }
            X = Array.AsReadOnly(x); Z = Array.AsReadOnly(z);
            lengths = new float[n]; starts = new float[n]; crossing = new bool[n];
            float total = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n; float dx = x[j] - x[i], dz = z[j] - z[i];
                lengths[i] = (float)Math.Sqrt(dx * dx + dz * dz); starts[i] = total; total += lengths[i];
                // A segment from one kerb to the other is the zebra: walkers wait for the green before it.
                crossing[i] = Math.Min(z[i], z[j]) < CityDistrictPlan.SouthKerbZ && Math.Max(z[i], z[j]) > CityDistrictPlan.NorthKerbZ;
            }
            Length = total;
        }
        public float SegmentLength(int segment) => lengths[segment];
        public float SegmentStart(int segment) => starts[segment];
        public bool IsCrossing(int segment) => crossing[segment];
        // Point at a distance along the closed route.
        public void PointAt(float distance, out float x, out float z, out int segment, out float along)
        {
            distance %= Length; if (distance < 0) distance += Length;
            segment = 0;
            while (segment < Count - 1 && distance >= starts[segment] + lengths[segment]) segment++;
            along = distance - starts[segment]; Locate(segment, along, out x, out z);
        }
        internal void Locate(int segment, float along, out float x, out float z)
        {
            int j = (segment + 1) % Count; float t = lengths[segment] > 0 ? along / lengths[segment] : 0;
            x = X[segment] + (X[j] - X[segment]) * t; z = Z[segment] + (Z[j] - Z[segment]) * t;
        }
        internal void Direction(int segment, out float dx, out float dz)
        {
            int j = (segment + 1) % Count; float length = Math.Max(1e-4f, lengths[segment]);
            dx = (X[j] - X[segment]) / length; dz = (Z[j] - Z[segment]) / length;
        }
    }
    public sealed class CityWalker
    {
        public int Index { get; } public int Route { get; } public WalkerStyle Style { get; } public float Speed { get; }
        public float X { get; internal set; } public float Z { get; internal set; }
        // Direction of travel in degrees: 0 = +Z, 90 = +X.
        public float Heading { get; internal set; }
        public bool Moving { get; internal set; }
        // Metres walked, for the leg swing.
        public float Stride { get; internal set; }
        public bool Waiting { get; internal set; }
        public bool InCrosswalk { get; internal set; }
        internal int Segment; internal float Along, Blocked, IgnoreOthers;
        internal CityWalker(int index, int route, WalkerStyle style, float speed) { Index = index; Route = route; Style = style; Speed = speed; }
        internal float RoutePosition => CityCrowd.Routes[Route].SegmentStart(Segment) + Along;
    }
    // People who stay in place: the barista, a café visitor, the Exchange guard and a trader at the board.
    public sealed class CityFigure
    {
        public WalkerStyle Style { get; } public float X { get; } public float Z { get; }
        // Facing direction in degrees: 0 = +Z, 90 = +X.
        public float Facing { get; }
        public bool Seated { get; }
        public CityRect Footprint => CityRect.Around(X, Z, CityCrowd.BodyRadius, CityCrowd.BodyRadius);
        internal CityFigure(WalkerStyle style, float x, float z, float facing, bool seated) { Style = style; X = x; Z = z; Facing = facing; Seated = seated; }
    }

    // Decorative passers-by on closed routes in both quarters and in the Exchange hall. They cross the boulevard only
    // on the green and only if there is time to finish, stop for the player, follow each other on a route and yield to
    // lower-numbered walkers from other routes. Not clients, no dialogue, no effect on the day.
    public sealed class CityCrowd
    {
        public const float BodyRadius = 0.3f, PlayerStop = 1.2f, Lane = 0.55f, FollowGap = 0.85f, CrossSpeed = 1.4f, MinSpeed = 1.05f, MaxSpeed = 1.45f;
        private const double Step = 0.05; private const float PatienceSeconds = 2.5f, PassSeconds = 1.5f;
        public static ReadOnlyCollection<CityRoute> Routes { get; } = Array.AsReadOnly(new[]
        {
            new CityRoute(-11.5f, 12, 11.5f, 12, 11.5f, 21.5f, -11.5f, 21.5f),                                   // plaza ring
            new CityRoute(-42.5f, 13.5f, 42.5f, 13.5f, 42.5f, 24.6f, 15, 24.6f, 13.5f, 23.6f, -13.5f, 23.6f, -15, 24.6f, -42.5f, 24.6f), // promenade
            new CityRoute(-20.5f, 12, -20.5f, 0.8f, -41, 0.8f, -41, 12),                                          // western street
            new CityRoute(20.2f, 0.8f, 42.3f, 0.8f, 42.3f, 11.6f, 20.2f, 11.6f),                                  // business street
            new CityRoute(0.9f, 17.5f, 0.9f, CityCrosswalk.SouthCurbZ, 0.9f, CityCrosswalk.NorthCurbZ, 0.9f, 60.8f, 3.5f, 64, 3.5f, 69,
                -3.5f, 69, -3.5f, 64, -0.9f, 60.8f, -0.9f, CityCrosswalk.NorthCurbZ, -0.9f, CityCrosswalk.SouthCurbZ, -0.9f, 17.5f), // crossing, Exchange hall
            new CityRoute(-42, 42.7f, 42, 42.7f, 42, 43.9f, -42, 43.9f),                                          // north pavement
            new CityRoute(-10.5f, 46.2f, 10.5f, 46.2f, 10.5f, 53.6f, -10.5f, 53.6f),                              // Exchange square
        });
        private static readonly int[] walkersPerRoute = { 2, 3, 1, 1, 3, 2, 2 };
        public static ReadOnlyCollection<CityFigure> Figures { get; } = Array.AsReadOnly(new[]
        {
            new CityFigure(WalkerStyle.Barista, 25.1f, -8.5f, 0, false),
            new CityFigure(WalkerStyle.Analyst, 25.5f, -3.95f, 0, true),
            new CityFigure(WalkerStyle.Guard, -3.2f, 61.3f, 90, false),
            new CityFigure(WalkerStyle.Trader, 5.4f, 69.4f, 0, false),
        });
        private readonly CityWalker[] walkers;
        public ReadOnlyCollection<CityWalker> Walkers { get; }
        public CityCrowd()
        {
            var list = new List<CityWalker>();
            for (int route = 0; route < Routes.Count; route++)
                for (int k = 0; k < walkersPerRoute[route]; k++)
                {
                    int i = list.Count;
                    // One pace per route: a faster walker would only catch up and trail the slower one in single file.
                    var walker = new CityWalker(i, route, (WalkerStyle)(i % 9), MinSpeed + (MaxSpeed - MinSpeed) * VoxelNoise.Value(route, 2, 9, 61));
                    var path = Routes[route];
                    path.PointAt(path.Length * (k + 0.35f * VoxelNoise.Value(i, 4, 9, 67)) / walkersPerRoute[route], out _, out _, out int segment, out float along);
                    // Nobody starts on the zebra: such a walker begins at the far kerb.
                    if (path.IsCrossing(segment)) { segment = (segment + 1) % path.Count; along = 0; }
                    walker.Segment = segment; walker.Along = along;
                    Place(walker);
                    list.Add(walker);
                }
            walkers = list.ToArray();
            Walkers = Array.AsReadOnly(walkers);
        }
        public bool AnyOnCrossing { get { foreach (var w in walkers) if (w.InCrosswalk) return true; return false; } }
        public void Advance(double seconds, CrosswalkSignal signal, bool playerPresent, float playerX, float playerZ)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            while (seconds > 1e-9)
            {
                double dt = Math.Min(Step, seconds); seconds -= dt;
                foreach (var walker in walkers) StepWalker(walker, (float)dt, signal, playerPresent, playerX, playerZ);
            }
        }
        private static bool MayStartCrossing(CityRoute route, int segment, CrosswalkSignal signal)
            => signal == null || (signal.Phase == CrosswalkPhase.Walk && signal.WalkSecondsLeft >= route.SegmentLength(segment) / CrossSpeed - 1);
        private void StepWalker(CityWalker w, float dt, CrosswalkSignal signal, bool playerPresent, float px, float pz)
        {
            var route = Routes[w.Route];
            if (route.IsCrossing(w.Segment) && w.Along <= 0 && !MayStartCrossing(route, w.Segment, signal))
            { w.Waiting = true; w.Moving = false; w.InCrosswalk = false; return; }
            w.Waiting = false;
            route.Direction(w.Segment, out float dx, out float dz);
            bool blocked = playerPresent && Ahead(w, dx, dz, px, pz, PlayerStop);
            bool yielding = false;
            if (!blocked)
                foreach (var other in walkers)
                {
                    if (other == w) continue;
                    if (other.Route == w.Route)
                    {
                        float gap = other.RoutePosition - w.RoutePosition; if (gap < 0) gap += route.Length;
                        if (gap > 0 && gap < FollowGap) { blocked = true; break; }
                    }
                    else if (other.Index < w.Index && w.IgnoreOthers <= 0 && Ahead(w, dx, dz, other.X, other.Z, FollowGap)) { blocked = yielding = true; break; }
                }
            if (w.IgnoreOthers > 0) w.IgnoreOthers -= dt;
            if (blocked)
            {
                w.Moving = false;
                // A walker held by another route for long passes through once, so crossing routes never lock up.
                if (yielding && (w.Blocked += dt) > PatienceSeconds) { w.IgnoreOthers = PassSeconds; w.Blocked = 0; }
                return;
            }
            w.Blocked = 0;
            float speed = route.IsCrossing(w.Segment) ? Math.Max(w.Speed, CrossSpeed) : w.Speed, move = speed * dt;
            w.Along += move; w.Stride += move;
            while (w.Along >= route.SegmentLength(w.Segment))
            {
                w.Along -= route.SegmentLength(w.Segment); w.Segment = (w.Segment + 1) % route.Count;
                if (route.IsCrossing(w.Segment) && !MayStartCrossing(route, w.Segment, signal)) { w.Along = 0; break; }
            }
            w.Moving = true;
            Place(w);
        }
        private static void Place(CityWalker w)
        {
            var route = Routes[w.Route];
            route.Locate(w.Segment, w.Along, out float x, out float z); w.X = x; w.Z = z;
            route.Direction(w.Segment, out float dx, out float dz);
            w.Heading = (float)(Math.Atan2(dx, dz) * 180 / Math.PI);
            w.InCrosswalk = route.IsCrossing(w.Segment) && w.Along > 0;
        }
        private static bool Ahead(CityWalker w, float dx, float dz, float ox, float oz, float range)
        {
            float rx = ox - w.X, rz = oz - w.Z, along = rx * dx + rz * dz;
            return along > 0 && along < range && Math.Abs(rx * dz - rz * dx) < Lane;
        }
    }
}
