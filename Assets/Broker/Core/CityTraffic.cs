using System;
using System.Collections.ObjectModel;

namespace Broker.Core
{
    public sealed class CityCar
    {
        public int Lane { get; } public CarStyle Style { get; } public int Seed { get; }
        // Index into CarModels.Paints.
        public int Paint => CarModels.PaintFor(Style, Seed);
        // The body: its length keeps the gaps in the lane, its footprint is what the player's car runs into.
        public CarShape Shape => CarModels.Shape(Style);
        public float Length => Shape.Length;
        public float Width => Shape.Width;
        public OrientedRect Footprint => new OrientedRect(X, Z, Yaw == 0 ? 0 : (float)Math.PI, Length / 2, Width / 2);
        public float Cruise { get; }
        // Position of the car centre along its lane ring, 0 ≤ S < RingLength; the lane direction is +S.
        public float S { get; internal set; }
        public float Speed { get; internal set; }
        // Brake lamps: on while the car slows down noticeably or stands.
        public bool Braking { get; internal set; }
        public float X => Eastbound ? S - roadHalf : roadHalf - S;
        public float Z { get; }
        public bool Eastbound { get; }
        // Yaw of a car model whose front faces +X.
        public float Yaw => Eastbound ? 0 : 180;
        internal bool Committed;
        private readonly float roadHalf;
        // A boulevard car.
        internal CityCar(int lane, CarStyle style, int seed, float cruise, float s)
            : this(lane, style, seed, cruise, s, CityTraffic.LaneZ(lane), CityTraffic.RoadHalf, CityTraffic.Eastbound(lane)) { }
        // A car on any looped road: the lane's centre line, the half-length of the loop and the direction.
        internal CityCar(int lane, CarStyle style, int seed, float cruise, float s, float laneZ, float roadHalf, bool eastbound)
        { Lane = lane; Style = style; Seed = seed; Cruise = cruise; S = s; Z = laneZ; this.roadHalf = roadHalf; Eastbound = eastbound; }
    }

    // Visual boulevard traffic: 12 cars in four lanes (sedans, hatchbacks, two taxis, two pickups and a van, each keeping
    // gaps by its own length), right-hand, ring roads that wrap inside the end tunnels.
    // Gipps-style safe speed behind the leader and before the stop line, an amber rule for cars too close to stop
    // and hard limits so a car never overlaps its leader or passes the line on red. The player's car is one more leader
    // for every lane it reaches into: the cars brake for it and never drive into it. Not a collision physics model:
    // cars never touch people, the signal keeps them apart.
    public sealed class CityTraffic
    {
        public const int LaneCount = 4, CarsPerLane = 3;
        public const float RoadHalf = 90, RingLength = 2 * RoadHalf, MinGap = 2f, BumperGap = 0.5f;
        // Ring positions of the stop line and the zebra; equal for both directions.
        public const float StopS = RoadHalf + CityCrosswalk.EastboundStopX, CrossStartS = RoadHalf - CityCrosswalk.HalfWidth, CrossEndS = RoadHalf + CityCrosswalk.HalfWidth;
        public const float Accel = 2, ComfortBrake = 3, MaxBrake = 6, MinCruise = 6.4f, MaxCruise = 7.5f, StopMargin = 0.2f;
        // Deceleration that lights the brake lamps, and the speed under which a car counts as standing on the brake.
        public const float BrakeLampDecel = 0.8f, StandingSpeed = 0.05f;
        // Half-width of the band of a lane: a car body reaching into it holds that lane's cars.
        public const float LaneBand = 1.2f;
        private const double Step = 0.05;
        private static readonly float[] laneZ = { 30.5f, 33.5f, 36.5f, 39.5f };
        // Bodies by car index, three a lane from the nearest eastbound lane.
        private static readonly CarStyle[] mix =
        {
            CarStyle.Sedan, CarStyle.Hatchback, CarStyle.Taxi, CarStyle.Sedan, CarStyle.Pickup, CarStyle.Hatchback,
            CarStyle.Van, CarStyle.Sedan, CarStyle.Taxi, CarStyle.Pickup, CarStyle.Sedan, CarStyle.Hatchback,
        };
        private readonly CityCar[] cars;
        public ReadOnlyCollection<CityCar> Cars { get; }
        public static float LaneZ(int lane)
        {
            if (lane < 0 || lane >= LaneCount) throw new ArgumentOutOfRangeException(nameof(lane));
            return laneZ[lane];
        }
        public static bool Eastbound(int lane) => lane < 2;
        public CityTraffic()
        {
            cars = new CityCar[LaneCount * CarsPerLane];
            for (int lane = 0; lane < LaneCount; lane++)
                for (int k = 0; k < CarsPerLane; k++)
                {
                    int i = lane * CarsPerLane + k;
                    var style = mix[i];
                    float cruise = MinCruise + (MaxCruise - MinCruise) * VoxelNoise.Value(i, 5, 2, 43);
                    float s = 5 + lane * 13 + k * 60 + 6 * VoxelNoise.Value(i, 7, 1, 47);
                    cars[i] = new CityCar(lane, style, i * 3 + 1, cruise, s) { Speed = cruise };
                }
            Cars = Array.AsReadOnly(cars);
        }
        // A car between the stop line and the far side of the zebra, or one that took the amber and has not crossed yet.
        public bool CarInCrosswalk
        {
            get
            {
                foreach (var car in cars)
                {
                    float d = LineGap(car);
                    if (d < -0.1f && car.S - car.Length / 2 < CrossEndS + BumperGap) return true;
                    if (car.Committed && d >= -0.1f) return true;
                }
                return false;
            }
        }
        public void Advance(double seconds, CrosswalkSignal signal) => Advance(seconds, signal, null, 0);
        // The same with the player's car on the boulevard: its footprint and its velocity along +X.
        public void Advance(double seconds, CrosswalkSignal signal, OrientedRect? obstacle, float obstacleVelocityX)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (float.IsNaN(obstacleVelocityX) || float.IsInfinity(obstacleVelocityX)) obstacleVelocityX = 0;
            while (seconds > 1e-9)
            {
                double dt = Math.Min(Step, seconds); seconds -= dt;
                StepAll((float)dt, signal, obstacle, obstacleVelocityX);
            }
        }
        // A footprint that reaches into the band of the lane.
        public static bool InLane(OrientedRect footprint, int lane)
        {
            var bounds = footprint.Bounds; float z = LaneZ(lane);
            return bounds.MaxZ > z - LaneBand && bounds.MinZ < z + LaneBand && Math.Abs(bounds.CenterX) < RoadHalf;
        }
        // Distance from the nose to the stopping point before the next stop line; negative once the nose is past it.
        private static float LineGap(CityCar car)
        {
            float d = StopS - StopMargin - (car.S + car.Length / 2);
            if (d < -(CrossEndS - StopS + car.Length + 2)) d += RingLength;
            return d;
        }
        private void StepAll(float dt, CrosswalkSignal signal, OrientedRect? obstacle, float obstacleVelocityX)
        {
            bool green = signal == null || signal.CarsGreen, amber = signal != null && signal.CarsAmber;
            bool walkPhases = signal != null && (signal.Phase >= CrosswalkPhase.Walk);
            var oldS = new float[cars.Length]; var oldV = new float[cars.Length];
            for (int i = 0; i < cars.Length; i++) { oldS[i] = cars[i].S; oldV[i] = cars[i].Speed; }
            for (int i = 0; i < cars.Length; i++)
            {
                var car = cars[i];
                float lineGap = LineGap(car); bool beyond = lineGap < -0.1f;
                if (green || walkPhases || beyond) car.Committed = false;
                // Amber: a car that cannot stop with the hard brake goes on and is waited for.
                if (amber && !beyond && !car.Committed && oldV[i] * oldV[i] / (2 * MaxBrake) > lineGap + StopMargin) car.Committed = true;
                bool mustStop = !green && !beyond && !car.Committed;
                float target = car.Cruise, room = float.MaxValue;
                int leader = Leader(i, oldS);
                if (leader >= 0)
                {
                    float gap = Mod(oldS[leader] - oldS[i]) - (car.Length + cars[leader].Length) / 2;
                    room = gap - BumperGap;
                    target = Math.Min(target, (float)Math.Sqrt(Math.Max(0, 2 * ComfortBrake * (gap - MinGap) + oldV[leader] * oldV[leader])));
                }
                if (obstacle.HasValue && InLane(obstacle.Value, car.Lane))
                {
                    // The player's car as a leader: the gap from this nose to its near end along the lane, 0 while they
                    // overlap along the lane; a car it is behind ignores it.
                    var bounds = obstacle.Value.Bounds;
                    bool east = Eastbound(car.Lane);
                    float near = east ? bounds.MinX + RoadHalf : RoadHalf - bounds.MaxX, length = bounds.Width;
                    float ahead = Wrapped(near - oldS[i]);
                    if (ahead + length > -car.Length / 2)
                    {
                        float gap = Math.Max(0, ahead - car.Length / 2), speed = Math.Max(0, east ? obstacleVelocityX : -obstacleVelocityX);
                        room = Math.Min(room, gap - BumperGap);
                        target = Math.Min(target, (float)Math.Sqrt(Math.Max(0, 2 * ComfortBrake * (gap - MinGap) + speed * speed)));
                    }
                }
                if (mustStop) target = Math.Min(target, (float)Math.Sqrt(2 * ComfortBrake * Math.Max(0, lineGap)));
                float v = oldV[i];
                v = target > v ? Math.Min(target, v + Accel * dt) : Math.Max(target, v - MaxBrake * dt);
                v = Math.Max(0, v);
                float move = v * dt;
                if (move > room) move = Math.Max(0, room);
                if (mustStop && move > lineGap) move = Math.Max(0, lineGap);
                car.Speed = dt > 0 ? Math.Min(v, move / dt) : v;
                car.Braking = car.Speed < StandingSpeed || car.Speed < oldV[i] - BrakeLampDecel * dt;
                car.S = Mod(oldS[i] + move);
            }
        }
        // Nearest car ahead in the same lane, from the positions at the start of the step.
        private int Leader(int index, float[] s)
        {
            int lane = cars[index].Lane, best = -1; float bestGap = float.MaxValue;
            for (int j = lane * CarsPerLane; j < (lane + 1) * CarsPerLane; j++)
            {
                if (j == index) continue;
                float gap = Mod(s[j] - s[index]);
                if (gap < bestGap) { bestGap = gap; best = j; }
            }
            return best;
        }
        private static float Mod(float s) { s %= RingLength; return s < 0 ? s + RingLength : s; }
        // A distance along the ring folded into −RoadHalf…RoadHalf.
        private static float Wrapped(float d) { d = Mod(d); return d >= RoadHalf ? d - RingLength : d; }
    }
}
