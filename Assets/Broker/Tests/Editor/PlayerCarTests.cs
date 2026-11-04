using System;
using System.Collections.Generic;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    // A test driver: pure pursuit of a point a lookahead ahead along a polyline, slowing before the slow points.
    internal sealed class CarPilot
    {
        private readonly float[] x, z, kmh; private readonly float lookahead; private readonly bool closed;
        private int segment;
        public int Laps { get; private set; }
        public CarPilot(float[] path, float[] kmh, float lookahead, bool closed)
        {
            int n = path.Length / 2; x = new float[n]; z = new float[n];
            for (int i = 0; i < n; i++) { x[i] = path[2 * i]; z[i] = path[2 * i + 1]; }
            this.kmh = kmh; this.lookahead = lookahead; this.closed = closed;
        }
        private int N => x.Length;
        private int Next(int i) => closed ? (i + 1) % N : Math.Min(i + 1, N - 1);
        public DriveInput Drive(PlayerCar car)
        {
            float t;
            for (int guard = 0; ; guard++)
            {
                int j = Next(segment); float sx = x[j] - x[segment], sz = z[j] - z[segment], length2 = Math.Max(1e-6f, sx * sx + sz * sz);
                t = ((car.X - x[segment]) * sx + (car.Z - z[segment]) * sz) / length2;
                if (t < 1 || guard > N || (!closed && j == N - 1)) break;
                segment = j; if (segment == 0) Laps++;
            }
            t = Math.Max(0, Math.Min(1, t));
            float remaining = lookahead, aimX = 0, aimZ = 0, ahead = 0, target = float.MaxValue;
            float cx = x[segment] + (x[Next(segment)] - x[segment]) * t, cz = z[segment] + (z[Next(segment)] - z[segment]) * t;
            int k = segment; bool found = false;
            for (int guard = 0; guard < N + 1; guard++)
            {
                int j = Next(k); float ex = x[j] - cx, ez = z[j] - cz, length = (float)Math.Sqrt(ex * ex + ez * ez);
                if (!found && (length >= remaining || j == k)) { float f = length > 0 ? remaining / length : 0; aimX = cx + ex * f; aimZ = cz + ez * f; found = true; }
                if (!found) remaining -= length;
                ahead += length;
                float v = kmh[j] / 3.6f;
                target = Math.Min(target, (float)Math.Sqrt(v * v + 2 * 2.5f * Math.Max(0, ahead - 2)));
                if ((ahead > 40 && found) || j == k) break;
                cx = x[j]; cz = z[j]; k = j;
            }
            target = Math.Min(target, Math.Max(kmh[segment], kmh[Next(segment)]) / 3.6f);
            car.Footprint.Local(aimX, aimZ, out float along, out float side);
            float distance2 = Math.Max(1, along * along + side * side);
            float wanted = (float)Math.Atan(2 * side / distance2 * (PlayerCar.ToFrontAxle + PlayerCar.ToRearAxle));
            float lockAngle = PlayerCar.MaxSteer / (1 + car.Forward * car.Forward / (PlayerCar.SteerFadeSpeed * PlayerCar.SteerFadeSpeed));
            return new DriveInput((target - car.Forward) * 0.8f, -wanted / lockAngle, false);
        }
        // A clockwise lap of the ring from the boulevard's first lane: east, down the east street, west along the
        // south street, up the west street and back; 18 km/h round the corners, 45 on the straights.
        public static readonly float[] Lap =
        {
            -30, 30.5f, 30, 30.5f, 40, 31.5f, 45, 31.5f, 47.3f, 31.0f, 49.2f, 29.7f, 50.5f, 28.0f, 51, 25.5f, 50.5f, 20, 48, 14, 48, -4,
            50.5f, -9, 51, -13, 50.5f, -15.3f, 49.2f, -17.2f, 47.3f, -18.5f, 45, -19, 40, -18.5f, 0, -18, -30, -18,
            -38, -20.5f, -45, -21, -47.3f, -20.5f, -49.2f, -19.2f, -50.5f, -17.3f, -51, -15, -50.5f, -9, -48, -3, -48, 15,
            -50.5f, 20, -51, 25.5f, -50.5f, 27.8f, -49.2f, 29.7f, -47.3f, 31.0f, -45, 31.5f, -38, 31.0f,
        };
        public static float[] LapKmh()
        {
            var kmh = new float[Lap.Length / 2];
            for (int i = 0; i < kmh.Length; i++) kmh[i] = Math.Abs(Lap[2 * i]) > 44 && (Lap[2 * i + 1] > 22 || Lap[2 * i + 1] < -12) ? 18 : 45;
            return kmh;
        }
        public static float[] Even(float[] path, float kmh) { var s = new float[path.Length / 2]; for (int i = 0; i < s.Length; i++) s[i] = kmh; return s; }
    }

    public sealed class PlayerCarTests
    {
        private static readonly DriveArea Open = new DriveArea(new CityRect(-3000, -3000, 3000, 3000));
        private static PlayerCar OnOpenGround(float kmh = 0, float heading = 0) { var car = new PlayerCar(Open); car.Place(0, 0, heading, kmh / 3.6f); return car; }
        private static float Hold(PlayerCar car, float kmh) => (kmh / 3.6f - car.Forward) * 1.5f;
        private static float Turned(float from, float to)
        {
            double d = to - from; while (d > Math.PI) d -= 2 * Math.PI; while (d < -Math.PI) d += 2 * Math.PI; return (float)d;
        }
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        // Slip of the rear axle: near zero while the tail follows the front, large once it swings out. (The body's own slip
        // angle is large in any tight slow turn, from the geometry alone.)
        private static float RearSlip(PlayerCar car)
            => Math.Abs((float)Math.Atan2(car.Lateral - PlayerCar.ToRearAxle * car.YawRate, Math.Max(1, Math.Abs(car.Forward))));
        private static void AssertSane(PlayerCar car)
        {
            foreach (float v in new[] { car.X, car.Z, car.Heading, car.Forward, car.Lateral, car.YawRate, car.Steering, car.Rpm, car.Pitch, car.Roll, car.Rolled })
                Assert.That(Finite(v), Is.True);
            Assert.That(car.Heading, Is.InRange(-Math.PI - 1e-4, Math.PI + 1e-4));
            Assert.That(car.Rpm, Is.InRange(PlayerCar.IdleRpm - 1, PlayerCar.RedlineRpm + 101)); Assert.That(car.Gear == -1 || (car.Gear >= 1 && car.Gear <= PlayerCar.GearCount), Is.True);
            Assert.That(Math.Abs(car.Pitch), Is.LessThanOrEqualTo(3)); Assert.That(Math.Abs(car.Roll), Is.LessThanOrEqualTo(3.5f));
        }
        private static float Deepest(PlayerCar car)
        {
            float deepest = 0; var footprint = car.Footprint;
            for (int i = 0; i < 4; i++) { footprint.Corner(i, out float x, out float z); deepest = Math.Max(deepest, car.Area.Outside(x, z, out _, out _)); }
            return deepest;
        }
        private static bool CornerInside(PlayerCar car)
        {
            var footprint = car.Footprint;
            foreach (var p in car.Area.InnerCorners)
            {
                footprint.Local(p.X, p.Z, out float along, out float side);
                if (Math.Abs(along) < PlayerCar.HalfLength - 0.02f && Math.Abs(side) < PlayerCar.HalfWidth - 0.02f) return true;
            }
            return false;
        }

        [Test] public void StandsParkedInTheLayByWithWheelsStraight()
        {
            var car = new PlayerCar();
            Assert.That(car.Area, Is.SameAs(CityRoads.Ring));
            Assert.That(car.X, Is.EqualTo(CityRoads.ParkX)); Assert.That(car.Z, Is.EqualTo(CityRoads.ParkZ)); Assert.That(car.Heading, Is.EqualTo(CityRoads.ParkHeading));
            Assert.That(car.Speed, Is.Zero); Assert.That(car.Steering, Is.Zero); Assert.That(car.Gear, Is.EqualTo(1)); Assert.That(car.GearLabel, Is.EqualTo("1"));
            Assert.That(car.Rpm, Is.EqualTo(PlayerCar.IdleRpm)); Assert.That(car.AtRest, Is.True); Assert.That(car.Parked, Is.True); Assert.That(car.Contacts, Is.Zero);
            Assert.Throws<ArgumentNullException>(() => new PlayerCar(null));
            Assert.Throws<ArgumentException>(() => car.Place(float.NaN, 0, 0));
            Assert.Throws<ArgumentException>(() => car.Place(0, 0, 0, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => car.Advance(-1, DriveInput.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => car.Advance(double.NaN, DriveInput.None));
            car.Place(0, 35, 0, 50 / 3.6f); Assert.That(car.Parked, Is.False); Assert.That(car.Gear, Is.EqualTo(2));
            car.Place(0, 35, 0, -2); Assert.That(car.Gear, Is.EqualTo(-1));
            car.Place(-24, 27.35f, 0, 3); Assert.That(car.Parked, Is.False);
            car.Park(); Assert.That(car.Parked, Is.True); Assert.That(car.X, Is.EqualTo(CityRoads.ParkX));
        }
        [Test] public void KeysAreClampedAndNoneIsIdle()
        {
            var keys = new DriveInput(3, -7, true);
            Assert.That(keys.Throttle, Is.EqualTo(1)); Assert.That(keys.Steer, Is.EqualTo(-1)); Assert.That(keys.Handbrake, Is.True); Assert.That(keys.IsIdle, Is.False);
            Assert.That(new DriveInput(float.NaN, float.PositiveInfinity, false).IsIdle, Is.True); Assert.That(DriveInput.None.IsIdle, Is.True);
        }
        [Test] public void GearsAndTorqueCurve()
        {
            for (int gear = 2; gear <= PlayerCar.GearCount; gear++) Assert.That(PlayerCar.Ratio(gear), Is.LessThan(PlayerCar.Ratio(gear - 1)));
            Assert.That(PlayerCar.Ratio(-1), Is.EqualTo(PlayerCar.ReverseRatio));
            Assert.Throws<ArgumentOutOfRangeException>(() => PlayerCar.Ratio(0)); Assert.Throws<ArgumentOutOfRangeException>(() => PlayerCar.Ratio(PlayerCar.GearCount + 1));
            Assert.That(PlayerCar.Torque(PlayerCar.PeakRpm), Is.EqualTo(PlayerCar.PeakTorque));
            Assert.That(PlayerCar.Torque(PlayerCar.IdleRpm), Is.LessThan(PlayerCar.PeakTorque)); Assert.That(PlayerCar.Torque(PlayerCar.RedlineRpm), Is.LessThan(PlayerCar.PeakTorque));
            Assert.That(PlayerCar.Torque(20000), Is.EqualTo(0.3f * PlayerCar.PeakTorque).Within(1e-3f));
        }
        [Test] public void PullsAwayThroughFourGearsToTheLimiter()
        {
            var car = OnOpenGround(); double t = 0, to50 = -1; var gears = new List<int> { 1 }; float maxRpm = 0, pitch = 0;
            while (t < 60)
            {
                car.Advance(0.02, new DriveInput(1, 0, false)); t += 0.02;
                if (car.Gear != gears[gears.Count - 1]) gears.Add(car.Gear);
                if (to50 < 0 && car.Kmh >= 50) to50 = t;
                maxRpm = Math.Max(maxRpm, car.Rpm); pitch = Math.Max(pitch, car.Pitch); AssertSane(car);
            }
            Assert.That(gears, Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(to50, Is.InRange(3.5, 6.0)); Assert.That(car.Kmh, Is.InRange(95f, 115f));
            Assert.That(maxRpm, Is.LessThanOrEqualTo(PlayerCar.RedlineRpm + 100)); Assert.That(pitch, Is.GreaterThan(0.5f));
            Assert.That(Math.Abs(car.Z), Is.LessThan(0.01f)); Assert.That(Math.Abs(car.Heading), Is.LessThan(1e-3f));
            Assert.That(car.Rolled, Is.EqualTo(car.X).Within(0.5f));
        }
        [Test] public void BrakesStraightFromFiftyThenReversesOnTheSameKey()
        {
            var car = OnOpenGround(50); float pitch = 0;
            for (int guard = 0; car.Forward > 0.01f && guard < 4000; guard++)
            {
                car.Advance(0.005, new DriveInput(-1, 0, false)); pitch = Math.Min(pitch, car.Pitch);
                if (car.Forward > 0.5f) { Assert.That(car.Gear, Is.GreaterThan(0)); Assert.That(car.Braking, Is.True); }
            }
            Assert.That(car.X, Is.InRange(9f, 15f)); Assert.That(Math.Abs(car.Z), Is.LessThan(0.01f)); Assert.That(pitch, Is.LessThan(-1));
            for (int i = 0; i < 1500; i++) { car.Advance(0.02, new DriveInput(-1, 0, false)); Assert.That(car.Kmh, Is.LessThanOrEqualTo(PlayerCar.MaxReverseSpeed * 3.6f + 0.5f)); }
            Assert.That(car.Gear, Is.EqualTo(-1)); Assert.That(car.GearLabel, Is.EqualTo("R")); Assert.That(car.Forward, Is.LessThan(0));
            Assert.That(car.Kmh, Is.GreaterThan(PlayerCar.MaxReverseSpeed * 3.6f - 2));
            // W brakes the reverse first, then pulls forward.
            bool braked = false;
            for (int guard = 0; car.Forward < -0.01f && guard < 4000; guard++) { car.Advance(0.005, new DriveInput(1, 0, false)); braked |= car.Braking && car.Gear < 0; }
            Assert.That(braked, Is.True);
            for (int i = 0; i < 100; i++) car.Advance(0.02, new DriveInput(1, 0, false));
            Assert.That(car.Gear, Is.EqualTo(1)); Assert.That(car.Forward, Is.GreaterThan(1));
        }
        [Test] public void CoastsToAStopAndStaysPut()
        {
            var car = OnOpenGround(50); double t = 0;
            while (!car.AtRest && t < 60) { car.Advance(0.05, DriveInput.None); t += 0.05; }
            Assert.That(t, Is.InRange(8.0, 40.0));
            float x = car.X;
            for (int i = 0; i < 200; i++) car.Advance(0.05, DriveInput.None);
            Assert.That(car.X, Is.EqualTo(x)); Assert.That(car.Speed, Is.Zero); Assert.That(car.Rpm, Is.EqualTo(PlayerCar.IdleRpm));
        }
        [Test] public void AStandingCarTurnsOnlyItsWheels()
        {
            var car = OnOpenGround();
            car.Advance(1, new DriveInput(0, 1, false));
            Assert.That(car.X, Is.Zero); Assert.That(car.Z, Is.Zero); Assert.That(car.Heading, Is.Zero);
            Assert.That(car.Steering, Is.EqualTo(-PlayerCar.MaxSteer).Within(1e-4f));
            car.Advance(1, DriveInput.None); Assert.That(car.Steering, Is.Zero);
            car.Advance(0.1, new DriveInput(0, 0, true)); Assert.That(car.HandbrakeOn, Is.True); Assert.That(car.Braking, Is.True); Assert.That(car.X, Is.Zero);
            car.Advance(0.1, DriveInput.None); Assert.That(car.HandbrakeOn, Is.False);
        }
        [TestCase(10f, 0f, 5f)] [TestCase(50f, 12f, 40f)]
        public void TurnsTightWhenSlowAndWideWhenFast(float kmh, float minRadius, float maxRadius)
        {
            var car = OnOpenGround(kmh);
            for (int i = 0; i < 600; i++) { car.Advance(0.01, new DriveInput(Hold(car, kmh), 1, false)); AssertSane(car); }
            float radius = car.Speed / Math.Abs(car.YawRate);
            Assert.That(car.Kmh, Is.EqualTo(kmh).Within(2)); Assert.That(radius, Is.InRange(minRadius, maxRadius));
            Assert.That(car.YawRate, Is.LessThan(0)); Assert.That(car.Steering, Is.LessThan(0));
            Assert.That(car.Speed * Math.Abs(car.YawRate), Is.LessThan(PlayerCar.Gravity)); Assert.That(RearSlip(car), Is.LessThan(8 / 57.3f));
            if (kmh > 30) Assert.That(car.Roll, Is.GreaterThan(0.5f));
        }
        [Test] public void FullLockUnderPowerNeverSpins()
        {
            var car = OnOpenGround(); float slip = 0;
            for (int i = 0; i < 800; i++) { car.Advance(0.01, new DriveInput(1, -1, false)); slip = Math.Max(slip, RearSlip(car)); }
            Assert.That(slip, Is.LessThan(8 / 57.3f)); Assert.That(car.YawRate, Is.GreaterThan(0));
            var fast = OnOpenGround(60); slip = 0;
            for (int i = 0; i < 400; i++) { fast.Advance(0.01, new DriveInput(1, 1, false)); slip = Math.Max(slip, RearSlip(fast)); AssertSane(fast); }
            Assert.That(slip, Is.LessThan(8 / 57.3f));
        }
        [Test] public void BrakingInAFastTurnStaysStable()
        {
            var car = OnOpenGround(80); float slip = 0;
            for (int i = 0; i < 300; i++) { car.Advance(0.01, new DriveInput(i < 50 ? 1 : -1, 1, false)); slip = Math.Max(slip, RearSlip(car)); AssertSane(car); }
            Assert.That(slip, Is.LessThan(8 / 57.3f)); Assert.That(car.Kmh, Is.LessThan(40));
        }
        [Test] public void HandbrakeSwingsTheTailRoundForATightTurn()
        {
            float Turn(bool handbrake, out float slip)
            {
                var car = OnOpenGround(50); float turned = 0, last = car.Heading; slip = 0;
                for (int i = 0; i < 150; i++)
                {
                    car.Advance(0.01, new DriveInput(0, i < 80 ? 1 : 0, handbrake && i < 80));
                    slip = Math.Max(slip, RearSlip(car)); turned += Turned(last, car.Heading); last = car.Heading;
                    if (handbrake && i < 80) Assert.That(car.HandbrakeOn, Is.True);
                }
                return Math.Abs(turned);
            }
            float plain = Turn(false, out float plainSlip), drift = Turn(true, out float driftSlip);
            Assert.That(plainSlip, Is.LessThan(8 / 57.3f)); Assert.That(driftSlip, Is.GreaterThan(20 / 57.3f));
            Assert.That(drift, Is.GreaterThan(plain + 30 / 57.3f));
        }
        [Test] public void RandomDrivingOnTheRingIsRepeatableAndStaysOnTheAsphalt()
        {
            PlayerCar Drive(out int contacts)
            {
                var car = new PlayerCar(); var random = new Random(7); var keys = DriveInput.None; contacts = 0;
                for (int i = 0; i < 3000; i++)
                {
                    if (i % 25 == 0) keys = new DriveInput((float)random.NextDouble() * 2 - 0.6f, (float)random.NextDouble() * 2 - 1, random.Next(8) == 0);
                    car.Advance(0.02 + i % 3 * 0.015, keys); contacts += car.Contacts;
                    AssertSane(car); Assert.That(Deepest(car), Is.LessThan(0.05f)); Assert.That(CornerInside(car), Is.False);
                }
                return car;
            }
            var a = Drive(out int contactsA); var b = Drive(out int contactsB);
            Assert.That(b.X, Is.EqualTo(a.X)); Assert.That(b.Z, Is.EqualTo(a.Z)); Assert.That(b.Heading, Is.EqualTo(a.Heading)); Assert.That(contactsB, Is.EqualTo(contactsA));
            Assert.That(contactsA, Is.GreaterThan(0));
        }
        [Test] public void LongFramesGiveTheSameDriveAsShortOnes()
        {
            var a = OnOpenGround(); var b = OnOpenGround(); var keys = new DriveInput(0.7f, 0.4f, false);
            for (int i = 0; i < 50; i++) a.Advance(0.1, keys);
            for (int i = 0; i < 1000; i++) b.Advance(0.005, keys);
            Assert.That(b.X, Is.EqualTo(a.X).Within(0.05f)); Assert.That(b.Z, Is.EqualTo(a.Z).Within(0.05f)); Assert.That(b.Heading, Is.EqualTo(a.Heading).Within(0.01f));
        }
        [TestCase(20f)] [TestCase(50f)] [TestCase(90f)]
        public void AKerbStopsTheCarWithoutLettingItThrough(float kmh)
        {
            var kerb = new DriveArea(new CityRect(-3000, -3000, 3000, 0));
            foreach (float angle in new[] { 10f, 30f, 60f, 90f })
            {
                var car = new PlayerCar(kerb); float heading = angle / 57.3f; car.Place(0, -3, heading, kmh / 3.6f);
                int contacts = 0; float deepest = 0;
                for (int i = 0; i < 300; i++)
                {
                    car.Advance(0.01, new DriveInput(i < 100 ? 0.5f : 0, 0, false)); contacts += car.Contacts; deepest = Math.Max(deepest, Deepest(car)); AssertSane(car);
                }
                Assert.That(contacts, Is.GreaterThan(0)); Assert.That(deepest, Is.LessThan(0.02f)); Assert.That(car.Kmh, Is.LessThan(kmh));
                if (angle == 90) { Assert.That(Math.Abs(Turned(heading, car.Heading)), Is.LessThan(1 / 57.3f)); Assert.That(car.Speed, Is.LessThan(1)); }
            }
        }
        [TestCase(-14f)] [TestCase(-11f)] [TestCase(-9f)] [TestCase(-7f)] [TestCase(-5f)]
        public void AnInnerKerbCornerNeverCutsIntoTheBody(float turnAt)
        {
            var area = new DriveArea(new CityRect(-100, 0, 100, 8), new CityRect(-4, -100, 4, 0));
            var car = new PlayerCar(area); car.Place(-30, 2, 0, 15 / 3.6f);
            for (int i = 0; i < 800; i++)
            {
                float steer = 0;
                if (car.X > turnAt || car.Heading < -0.2f)
                {
                    float error = -(float)Math.PI / 2 - car.Heading;
                    steer = car.Z < -3 ? -error * 2 + car.X * 0.3f : -error * 3;
                }
                car.Advance(0.01, new DriveInput(car.Kmh < 15 ? 0.4f : 0, steer, false));
                Assert.That(CornerInside(car), Is.False); Assert.That(Deepest(car), Is.LessThan(0.05f));
            }
        }
        [Test] public void AutopilotLapsTheRingWithoutTouchingAKerb()
        {
            var pilot = new CarPilot(CarPilot.Lap, CarPilot.LapKmh(), 4.5f, true);
            var car = new PlayerCar(); car.Place(-30, 30.5f, 0);
            int contacts = 0; double t = 0;
            for (int step = 0; step < 10000 && pilot.Laps < 1; step++)
            {
                car.Advance(0.02, pilot.Drive(car)); t += 0.02; contacts += car.Contacts; AssertSane(car);
            }
            Assert.That(pilot.Laps, Is.EqualTo(1)); Assert.That(contacts, Is.Zero); Assert.That(t, Is.InRange(25.0, 60.0));
        }
        [Test] public void PullsOutOfTheLayByAndParksBackIn()
        {
            float[] Drive(PlayerCar car, float[] path, float kmh, float stopX)
            {
                var pilot = new CarPilot(path, CarPilot.Even(path, kmh), 3, false); int contacts = 0;
                for (int step = 0; step < 3000; step++)
                {
                    var keys = pilot.Drive(car);
                    if (car.X > stopX) keys = new DriveInput(car.Forward > 0.05f ? -1 : 0, 0, false);
                    car.Advance(0.02, keys); contacts += car.Contacts;
                    if (car.X > stopX && car.AtRest) break;
                }
                return new float[] { contacts };
            }
            var outCar = new PlayerCar();
            Assert.That(Drive(outCar, new[] { -24.75f, 27.35f, -21.5f, 28.4f, -18, 30.2f, -12, 30.5f, 20, 30.5f, 21, 30.5f }, 10, 0)[0], Is.Zero);
            Assert.That(outCar.Z, Is.EqualTo(CityTraffic.LaneZ(0)).Within(0.4f)); Assert.That(CityTraffic.InLane(outCar.Footprint, 0), Is.True); Assert.That(outCar.Parked, Is.False);
            var inCar = new PlayerCar(); inCar.Place(-50, 30.5f, 0, 10 / 3.6f);
            Assert.That(Drive(inCar, new[] { -50, 30.5f, -34, 30.5f, -30, 29.8f, -27, 28.2f, -24, 27.45f, -21, 27.35f, 0, 27.35f, 1, 27.35f }, 8, -24.5f)[0], Is.Zero);
            Assert.That(inCar.Parked, Is.True); Assert.That(inCar.AtRest, Is.True);
        }
    }
}
