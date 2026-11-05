using System;
using System.Collections.ObjectModel;

namespace Broker.Core
{
    // Visual highway traffic: 12 cars in the four lanes of CityRoads' highway, right-hand, the inner lanes faster than the
    // outer ones, looping inside the tunnels like the boulevard cars. No signals and no lane changes: a Gipps-style safe
    // speed behind the leader with a time gap that grows with the speed, and a hard limit so a car never overlaps it. The
    // player's car is one more leader for every lane it reaches into: the cars brake for it and never drive into it.
    public sealed class HighwayTraffic
    {
        public const int LaneCount = 4, CarsPerLane = 3;
        public const float RoadHalf = CityRoads.HighwayLoopX, RingLength = 2 * RoadHalf, MinGap = 3f, BumperGap = 0.5f, Headway = 0.8f;
        public const float Accel = 2.5f, ComfortBrake = 3.5f, MaxBrake = 7, StandingSpeed = 0.05f, BrakeLampDecel = 0.8f;
        // Half-width of the band of a lane: a car body reaching into it holds that lane's cars.
        public const float LaneBand = 1.4f;
        private const double Step = 0.05;
        // Lane centres from the south: the eastbound outer and inner lanes, then the westbound inner and outer ones.
        private static readonly float[] laneZ =
        {
            CityRoads.HighwaySouthZ + 0.5f + CityRoads.LaneWidth / 2, CityRoads.HighwayZ - CityRoads.MedianHalf - 0.5f - CityRoads.LaneWidth / 2,
            CityRoads.HighwayZ + CityRoads.MedianHalf + 0.5f + CityRoads.LaneWidth / 2, CityRoads.HighwayNorthZ - 0.5f - CityRoads.LaneWidth / 2,
        };
        // Cruising speeds, m/s: 58–68 km/h in the outer lanes, 72–86 km/h in the inner ones.
        public const float OuterMinCruise = 16, OuterMaxCruise = 19, InnerMinCruise = 20, InnerMaxCruise = 24;
        private static readonly CarStyle[] mix =
        {
            CarStyle.Van, CarStyle.Sedan, CarStyle.Pickup, CarStyle.Hatchback, CarStyle.Sedan, CarStyle.Taxi,
            CarStyle.Sedan, CarStyle.Taxi, CarStyle.Hatchback, CarStyle.Pickup, CarStyle.Sedan, CarStyle.Van,
        };
        private readonly CityCar[] cars;
        public ReadOnlyCollection<CityCar> Cars { get; }
        public static float LaneZ(int lane)
        {
            if (lane < 0 || lane >= LaneCount) throw new ArgumentOutOfRangeException(nameof(lane));
            return laneZ[lane];
        }
        public static bool Eastbound(int lane) => lane < 2;
        public static bool Inner(int lane) => lane == 1 || lane == 2;
        public HighwayTraffic()
        {
            cars = new CityCar[LaneCount * CarsPerLane];
            for (int lane = 0; lane < LaneCount; lane++)
                for (int k = 0; k < CarsPerLane; k++)
                {
                    int i = lane * CarsPerLane + k;
                    float min = Inner(lane) ? InnerMinCruise : OuterMinCruise, max = Inner(lane) ? InnerMaxCruise : OuterMaxCruise;
                    float cruise = min + (max - min) * VoxelNoise.Value(i, 5, 2, 61);
                    float s = (11 + lane * 17 + k * 60 + 8 * VoxelNoise.Value(i, 7, 1, 67)) % RingLength;
                    cars[i] = new CityCar(lane, mix[i], i * 3 + 101, cruise, s, LaneZ(lane), RoadHalf, Eastbound(lane)) { Speed = cruise };
                }
            Cars = Array.AsReadOnly(cars);
        }
        public void Advance(double seconds) => Advance(seconds, null, 0);
        // The same with the player's car on the highway: its footprint and its velocity along +X.
        public void Advance(double seconds, OrientedRect? obstacle, float obstacleVelocityX)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (float.IsNaN(obstacleVelocityX) || float.IsInfinity(obstacleVelocityX)) obstacleVelocityX = 0;
            while (seconds > 1e-9)
            {
                double dt = Math.Min(Step, seconds); seconds -= dt;
                StepAll((float)dt, obstacle, obstacleVelocityX);
            }
        }
        // A footprint that reaches into the band of the lane.
        public static bool InLane(OrientedRect footprint, int lane)
        {
            var bounds = footprint.Bounds; float z = LaneZ(lane);
            return bounds.MaxZ > z - LaneBand && bounds.MinZ < z + LaneBand && Math.Abs(bounds.CenterX) < RoadHalf + 2;
        }
        // The speed that still stops `gap` metres behind a leader driving at `leaderSpeed`, keeping the time gap.
        private static float SafeSpeed(float gap, float leaderSpeed, float speed)
        {
            float room = gap - MinGap - Headway * speed;
            return (float)Math.Sqrt(Math.Max(0, 2 * ComfortBrake * room + leaderSpeed * leaderSpeed));
        }
        private void StepAll(float dt, OrientedRect? obstacle, float obstacleVelocityX)
        {
            var oldS = new float[cars.Length]; var oldV = new float[cars.Length];
            for (int i = 0; i < cars.Length; i++) { oldS[i] = cars[i].S; oldV[i] = cars[i].Speed; }
            for (int i = 0; i < cars.Length; i++)
            {
                var car = cars[i];
                float target = car.Cruise, room = float.MaxValue;
                int leader = Leader(i, oldS);
                if (leader >= 0)
                {
                    float gap = Mod(oldS[leader] - oldS[i]) - (car.Length + cars[leader].Length) / 2;
                    room = gap - BumperGap;
                    target = Math.Min(target, SafeSpeed(gap, oldV[leader], oldV[i]));
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
                        target = Math.Min(target, SafeSpeed(gap, speed, oldV[i]));
                    }
                }
                float v = oldV[i];
                v = target > v ? Math.Min(target, v + Accel * dt) : Math.Max(target, v - MaxBrake * dt);
                v = Math.Max(0, v);
                float move = v * dt;
                if (move > room) move = Math.Max(0, room);
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
        // A distance along the loop folded into −RoadHalf…RoadHalf.
        private static float Wrapped(float d) { d = Mod(d); return d >= RoadHalf ? d - RingLength : d; }
    }
}
