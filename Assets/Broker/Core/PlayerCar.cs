using System;
using System.Collections.ObjectModel;

namespace Broker.Core
{
    // The driver's keys: Throttle +1 for W (gas), −1 for S (brake while rolling forward, then reverse); Steer +1 for D
    // (right), −1 for A; Handbrake for Space.
    public readonly struct DriveInput
    {
        public float Throttle { get; } public float Steer { get; } public bool Handbrake { get; }
        public DriveInput(float throttle, float steer, bool handbrake) { Throttle = Clamp(throttle); Steer = Clamp(steer); Handbrake = handbrake; }
        public static DriveInput None => default;
        public bool IsIdle => Throttle == 0 && Steer == 0 && !Handbrake;
        private static float Clamp(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Math.Max(-1, Math.Min(1, value));
    }

    // The player's test sedan: an arcade car on real-ish physics. A single-track model with load transfer, tyres that grip
    // up to the friction circle and then slide, a four-speed automatic with a rev limiter, front-biased brakes and a
    // handbrake that locks the rear wheels for a slide. Below walking pace the tyres act like rails, so the car turns on its
    // steering geometry and comes to a clean stop. Kerbs, the other cars and the walkers are solid: the car stops against
    // them with a soft bounce, nothing is damaged, and an assist brakes before people in its path. Local coordinates of
    // CityDistrictPlan; heading in radians from +X toward +Z (0 = east).
    public sealed class PlayerCar
    {
        // Body: CarModels.Sedan, the centre of mass over the middle of the footprint.
        public const float Mass = 1150, YawInertia = 1800, ToFrontAxle = 1.24f, ToRearAxle = 1.20f, CentreHeight = 0.55f;
        public const float HalfLength = 2.07f, HalfWidth = 0.832f, WheelRadius = 0.30f, Gravity = 9.81f;
        // Tyres: cornering stiffness and peak grip per axle and the grip of locked wheels; the curve keeps ~90 % past its
        // peak. The rear holds a little more than the front, so at the limit the car runs wide instead of turning round.
        public const float FrontStiffness = 80000, RearStiffness = 95000, FrontGrip = 1.0f, RearGrip = 1.1f, SlideGrip = 0.8f, TyreShape = 1.3f;
        // Engine and gearbox: overall ratios include the final drive.
        public const float IdleRpm = 900, PeakRpm = 3800, PeakTorque = 95, RedlineRpm = 6300, UpshiftRpm = 5800, DownshiftRpm = 2300;
        public const float ShiftSeconds = 0.3f, Efficiency = 0.85f, ReverseRatio = 19, MaxReverseSpeed = 6;
        public const int GearCount = 4;
        // Brakes, air drag (N per (m/s)²) and rolling resistance.
        public const float BrakeForce = 9000, FrontBrakeShare = 0.65f, Drag = 0.45f, Rolling = 170;
        // Steering: full lock at a standstill, less as the speed grows, and how fast the wheels turn.
        public const float MaxSteer = 0.6f, SteerFadeSpeed = 9, SteerRate = 2.6f, SteerReturnRate = 4;
        // Stability assist: the correcting yaw moment per rad/s of over-rotation, its limit and the rotation it tolerates.
        public const float StabilityGain = 8000, StabilityMoment = 4000, StabilityDeadband = 0.05f;
        // The drivetrain holds a car rolling against the selected gear (pushed back off a kerb).
        public const float HoldForce = 3000;
        // Collisions: bounce and friction against kerbs, other cars and people; impacts turn the body as if it were twice as
        // heavy to turn, so a knock rights the car instead of spinning it; the assist's braking reach.
        public const float WallBounce = 0.15f, CarBounce = 0.15f, WallFriction = 0.3f, ImpactInertia = 2 * YawInertia, AssistDecel = 7;
        public const double Step = 1.0 / 200;
        // Paint of the test sedan in CarModels.Paints: red.
        public const int Paint = 1;
        private static readonly float[] ratios = { 20.9f, 13.05f, 9.0f, 6.67f };
        private const float RestSpeed = 0.05f, SlipFloor = 2f, ReverseSwitchSpeed = 0.4f;

        public float X { get; private set; } public float Z { get; private set; } public float Heading { get; private set; }
        // Velocity in the car's frame (forward, to the left) and the yaw rate (+ turns left).
        public float Forward { get; private set; } public float Lateral { get; private set; } public float YawRate { get; private set; }
        // Front wheel angle, radians, + to the left.
        public float Steering { get; private set; }
        // −1 reverse, 1…4 forward.
        public int Gear { get; private set; } = 1;
        public float Rpm { get; private set; } = IdleRpm;
        // Metres rolled by the wheels, for their turn; brake lamps; the handbrake as held.
        public float Rolled { get; private set; }
        public bool Braking { get; private set; }
        public bool HandbrakeOn { get; private set; }
        // Body lean for the scene, degrees: nose up under power and down under braking, the roof out of the turn.
        public float Pitch { get; private set; } public float Roll { get; private set; }
        // Collisions resolved during the last Advance.
        public int Contacts { get; private set; }
        public float Speed => (float)Math.Sqrt(Forward * Forward + Lateral * Lateral);
        public float Kmh => Speed * 3.6f;
        public float VelocityX => Forward * (float)Math.Cos(Heading) - Lateral * (float)Math.Sin(Heading);
        public float VelocityZ => Forward * (float)Math.Sin(Heading) + Lateral * (float)Math.Cos(Heading);
        // Angle between the heading and the direction of travel: a slide.
        public float SlipAngle => Speed < 1 ? 0 : (float)Math.Atan2(Lateral, Math.Abs(Forward));
        public string GearLabel => Gear < 0 ? "R" : Gear.ToString();
        public OrientedRect Footprint => new OrientedRect(X, Z, Heading, HalfLength, HalfWidth);
        public bool AtRest => Speed < RestSpeed && Math.Abs(YawRate) < 0.01f;
        // In the lay-by and standing: the driver may get out.
        public bool Parked
        {
            get
            {
                if (Speed > 0.5f) return false;
                var footprint = Footprint;
                for (int i = 0; i < 4; i++) { footprint.Corner(i, out float x, out float z); if (!CityRoads.ParkingZone.Contains(x, z)) return false; }
                return true;
            }
        }
        public static float Ratio(int gear)
        {
            if (gear < 0) return ReverseRatio;
            if (gear < 1 || gear > GearCount) throw new ArgumentOutOfRangeException(nameof(gear));
            return ratios[gear - 1];
        }
        // Engine torque at the crank, N·m: a broad curve round the peak.
        public static float Torque(float rpm)
        {
            float k = (rpm - PeakRpm) / 2900;
            return PeakTorque * Math.Max(0.3f, 1 - 0.35f * k * k);
        }

        private float shiftTimer, accelX, accelY;
        // The asphalt the car drives on: the ring of roads, or any other area (a test track).
        public DriveArea Area { get; }
        public PlayerCar() : this(CityRoads.Ring) { }
        public PlayerCar(DriveArea area) { Area = area ?? throw new ArgumentNullException(nameof(area)); Park(); }
        // Back in the lay-by, standing, wheels straight.
        public void Park() => Place(CityRoads.ParkX, CityRoads.ParkZ, CityRoads.ParkHeading);
        // Puts the car somewhere, rolling straight ahead at the given speed in a fitting gear.
        public void Place(float x, float z, float heading, float speed = 0)
        {
            if (!Finite(x) || !Finite(z) || !Finite(heading) || !Finite(speed)) throw new ArgumentException("Invalid car pose.");
            X = x; Z = z; Heading = Wrap(heading); Forward = speed; Lateral = 0; YawRate = 0; Steering = 0;
            Gear = speed < -0.1f ? -1 : 1;
            while (Gear > 0 && Gear < GearCount && WheelRpm(speed) * Ratio(Gear) > UpshiftRpm) Gear++;
            Rpm = Math.Max(IdleRpm, WheelRpm(speed) * Ratio(Gear));
            shiftTimer = 0; accelX = accelY = 0; Pitch = Roll = 0; Braking = HandbrakeOn = false; Contacts = 0;
        }
        // Stops the car where it stands (the driver got out).
        public void Stop() { Forward = Lateral = YawRate = 0; Braking = HandbrakeOn = false; accelX = accelY = 0; Pitch = Roll = 0; Rpm = IdleRpm; }

        public void Advance(double seconds, DriveInput input, CityTraffic traffic = null, CityCrowd crowd = null, HighwayTraffic highway = null)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            Contacts = 0;
            bool walkerAhead = WalkerAhead(crowd);
            while (seconds > 1e-9)
            {
                float dt = (float)Math.Min(Step, seconds); seconds -= dt;
                if (AtRest && input.Throttle == 0)
                {
                    // Standing: only the wheels turn to the keys; nothing pushes a car that stands.
                    Forward = Lateral = YawRate = 0; SteerToward(input.Steer, dt);
                    Braking = HandbrakeOn = input.Handbrake; Rpm = IdleRpm; Settle(0, 0, dt);
                    continue;
                }
                Integrate(dt, input, walkerAhead);
                Collide(traffic?.Cars, highway?.Cars, crowd);
                // Deep in a highway tunnel: on through the other one.
                float x = X;
                if (Area.Wrap(ref x, Z)) X = x;
            }
        }

        private static float WheelRpm(float speed) => Math.Abs(speed) / WheelRadius * 60 / (2 * (float)Math.PI);
        private void SteerToward(float steer, float dt)
        {
            float speed = Math.Abs(Forward), lockAngle = MaxSteer / (1 + speed * speed / (SteerFadeSpeed * SteerFadeSpeed));
            float wanted = -steer * lockAngle;
            float rate = Math.Abs(wanted) < Math.Abs(Steering) || wanted * Steering < 0 ? SteerReturnRate : SteerRate;
            Steering = MoveTowards(Steering, wanted, rate * dt);
        }
        private void Integrate(float dt, DriveInput input, bool walkerAhead)
        {
            float u = Forward, v = Lateral, r = YawRate;
            SteerToward(input.Steer, dt);
            // Pedals: the throttle drives the selected way; the opposite key brakes until the car stands, then selects it.
            float throttle = input.Throttle, drive = 0, brake = 0;
            if (Gear < 0)
            {
                if (throttle > 0) { if (u < -ReverseSwitchSpeed) brake = throttle; else { Gear = 1; drive = throttle; } }
                else if (throttle < 0) drive = throttle;
            }
            else
            {
                if (throttle < 0) { if (u > ReverseSwitchSpeed) brake = -throttle; else { Gear = -1; drive = throttle; } }
                else if (throttle > 0) drive = throttle;
            }
            if (walkerAhead) { drive = 0; brake = 1; }
            bool hand = input.Handbrake;
            if (hand) drive = 0;
            HandbrakeOn = hand; Braking = brake > 0.05f || hand;
            // Automatic gearbox: up near the limiter under power, down as the revs fall; no drive while it shifts.
            float wheelRpm = WheelRpm(u);
            if (shiftTimer > 0) shiftTimer -= dt;
            if (Gear > 0 && shiftTimer <= 0)
            {
                if (Gear < GearCount && drive > 0 && wheelRpm * Ratio(Gear) > UpshiftRpm) { Gear++; shiftTimer = ShiftSeconds; }
                else if (Gear > 1 && wheelRpm * Ratio(Gear) < DownshiftRpm) Gear--;
            }
            float engine = wheelRpm * Ratio(Gear);
            // The clutch slips while pulling away, so the engine revs above the wheels.
            float launch = drive != 0 && (Gear == 1 || Gear < 0) ? IdleRpm + Math.Abs(drive) * (2600 - IdleRpm) : IdleRpm;
            Rpm = Math.Min(RedlineRpm + 100, Math.Max(engine, launch));
            float force = 0;
            bool limited = engine >= RedlineRpm || (Gear < 0 && -u >= MaxReverseSpeed);
            if (drive != 0 && shiftTimer <= 0 && !limited)
                force = drive * Torque(Math.Max(engine, 2000)) * Ratio(Gear) * Efficiency / WheelRadius;
            float engineBrake = 0;
            if (drive == 0 && shiftTimer <= 0)
            {
                if (Gear > 0 ? u < -0.05f : u > 0.05f) engineBrake = HoldForce;
                else if (Math.Abs(u) > 1) engineBrake = (6 + 0.003f * engine) * Ratio(Gear) / WheelRadius;
            }
            // Axle loads: weight moves back under power and forward under braking.
            const float wheelbase = ToFrontAxle + ToRearAxle, weight = Mass * Gravity;
            float transfer = Mass * accelX * CentreHeight / wheelbase;
            float frontLoad = Math.Max(0.25f * weight * ToRearAxle / wheelbase, weight * ToRearAxle / wheelbase - transfer);
            float rearLoad = Math.Max(0.25f * weight * ToFrontAxle / wheelbase, weight * ToFrontAxle / wheelbase + transfer);
            // Front axle: contact velocity in the frame of the steered wheels; side force from the slip, brakes against the roll.
            float cosD = (float)Math.Cos(Steering), sinD = (float)Math.Sin(Steering);
            float fvx = u, fvy = v + ToFrontAxle * r;
            float fwx = fvx * cosD + fvy * sinD, fwy = -fvx * sinD + fvy * cosD;
            float ffy = SideForce(fwx, fwy, frontLoad, FrontStiffness, FrontGrip);
            float ffx = -brake * BrakeForce * FrontBrakeShare * Soft(fwx);
            Saturate(ref ffx, ref ffy, FrontGrip * frontLoad);
            // Rear axle: locked by the handbrake it slides against its own velocity. Otherwise the side force comes first:
            // the drive and the brakes only use the grip it leaves (traction control and ABS), so neither power nor braking
            // in a turn swings the tail out.
            float rvx = u, rvy = v - ToRearAxle * r, rfx, rfy;
            if (hand)
            {
                float slide = (float)Math.Sqrt(rvx * rvx + rvy * rvy);
                float k = SlideGrip * rearLoad * Math.Min(1, slide / 0.3f) / Math.Max(slide, 1e-4f);
                rfx = -rvx * k; rfy = -rvy * k;
            }
            else
            {
                rfy = SideForce(rvx, rvy, rearLoad, RearStiffness, RearGrip);
                float room = 0.95f * RearGrip * rearLoad, free = (float)Math.Sqrt(Math.Max(0, room * room - rfy * rfy));
                float hold = Math.Min(free, brake * BrakeForce * (1 - FrontBrakeShare) + engineBrake);
                rfx = Math.Max(-free, Math.Min(free, force)) - hold * Soft(rvx);
                Saturate(ref rfx, ref rfy, RearGrip * rearLoad);
            }
            // Body forces and the yaw moment; air drag and rolling resistance along the body.
            float frontSide = ffx * sinD + ffy * cosD;
            float fx = ffx * cosD - ffy * sinD + rfx - Drag * u * Math.Abs(u) - Rolling * Soft(u);
            float fy = frontSide + rfy;
            float mz = ToFrontAxle * frontSide - ToRearAxle * rfy;
            // Stability assist, like ESC braking one wheel: a car that rotates faster than the steering and the grip allow
            // gets a correcting moment, so full lock under power never spins it. The handbrake switches it off for a slide.
            if (!hand && Math.Abs(u) > 1)
            {
                float speed = (float)Math.Sqrt(u * u + v * v), limit = FrontGrip * Gravity / speed;
                float wanted = Math.Max(-limit, Math.Min(limit, u * (float)Math.Tan(Steering) / wheelbase)), excess = r - wanted;
                if (excess * r > 0 && Math.Abs(excess) > StabilityDeadband)
                    mz -= Math.Sign(excess) * Math.Min(StabilityMoment, (Math.Abs(excess) - StabilityDeadband) * StabilityGain);
            }
            u += (fx / Mass + v * r) * dt; v += (fy / Mass - u * r) * dt; r += mz / YawInertia * dt;
            // A car left without drive settles instead of creeping.
            if (drive == 0 && Math.Abs(u) < RestSpeed && Math.Abs(v) < RestSpeed && Math.Abs(r) < 0.02f) u = v = r = 0;
            Forward = u; Lateral = v; YawRate = r;
            Heading = Wrap(Heading + r * dt);
            float c = (float)Math.Cos(Heading), s = (float)Math.Sin(Heading);
            X += (u * c - v * s) * dt; Z += (u * s + v * c) * dt;
            Rolled += u * dt;
            Settle(fx / Mass, fy / Mass, dt);
        }
        // Smoothed accelerations for the load transfer and the body lean.
        private void Settle(float ax, float ay, float dt)
        {
            float k = Math.Min(1, dt / 0.12f);
            accelX += (ax - accelX) * k; accelY += (ay - accelY) * k;
            Pitch = Math.Max(-3, Math.Min(3, accelX * 0.35f));
            Roll = Math.Max(-3.5f, Math.Min(3.5f, -accelY * 0.4f));
        }
        // Side force of a tyre from its contact velocity: linear at small slip, a broad peak at the axle's grip, ~90 % past it.
        private static float SideForce(float vx, float vy, float load, float stiffness, float grip)
        {
            float alpha = (float)Math.Atan(vy / Math.Max(Math.Abs(vx), SlipFloor));
            float peak = grip * load, b = stiffness / (TyreShape * peak);
            return -peak * (float)Math.Sin(TyreShape * Math.Atan(b * alpha));
        }
        private static void Saturate(ref float fx, ref float fy, float limit)
        {
            float total = (float)Math.Sqrt(fx * fx + fy * fy);
            if (total > limit && total > 0) { float k = limit / total; fx *= k; fy *= k; }
        }
        // Sign of a velocity that fades to zero near a standstill, so brakes and resistance stop the car instead of rocking it.
        private static float Soft(float velocity) => Math.Max(-1, Math.Min(1, velocity / 0.3f));

        // People inside the car's path within its braking distance.
        private bool WalkerAhead(CityCrowd crowd)
        {
            if (crowd == null || Math.Abs(Forward) < 0.3f) return false;
            var footprint = Footprint; float reach = Forward * Forward / (2 * AssistDecel) + 1.2f, direction = Math.Sign(Forward);
            foreach (var walker in crowd.Walkers)
            {
                footprint.Local(walker.X, walker.Z, out float along, out float side);
                along *= direction;
                if (along > HalfLength - 0.3f && along < HalfLength + reach && Math.Abs(side) < HalfWidth + CityCrowd.BodyRadius + 0.3f) return true;
            }
            return false;
        }
        // Kerbs, the inner kerb corners, the boulevard and highway cars and the walkers, deepest first, a few passes.
        private void Collide(ReadOnlyCollection<CityCar> traffic, ReadOnlyCollection<CityCar> highway, CityCrowd crowd)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                bool touched = false; var footprint = Footprint;
                float deepest = 0, nx = 0, nz = 0;
                for (int i = 0; i < 4; i++)
                {
                    footprint.Corner(i, out float cx, out float cz);
                    float depth = Area.Outside(cx, cz, out float tx, out float tz);
                    if (depth > deepest) { deepest = depth; nx = tx; nz = tz; }
                }
                if (deepest > 0)
                {
                    // Corners pressed against the same kerb face share the contact, so a square hit does not twist the car.
                    float sx = 0, sz = 0, weight = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        footprint.Corner(i, out float cx, out float cz);
                        float depth = Area.Outside(cx, cz, out float tx, out float tz);
                        if (depth <= 0 || tx * nx + tz * nz < 0.9f) continue;
                        float w = depth + 0.02f; sx += cx * w; sz += cz * w; weight += w;
                    }
                    Resolve(sx / weight, sz / weight, nx, nz, deepest, WallBounce, WallFriction, ImpactInertia);
                    touched = true; footprint = Footprint;
                }
                foreach (var corner in Area.InnerCorners)
                    if (PushOffPoint(footprint, corner.X, corner.Z, 0, WallBounce, WallFriction)) { touched = true; footprint = Footprint; }
                if (CollideCars(traffic, ref footprint)) touched = true;
                if (CollideCars(highway, ref footprint)) touched = true;
                if (crowd != null)
                    foreach (var walker in crowd.Walkers)
                        if (PushOffPoint(footprint, walker.X, walker.Z, CityCrowd.BodyRadius, 0, 0.5f)) { touched = true; footprint = Footprint; }
                if (!touched) break;
            }
        }
        // The cars of one road that the footprint reaches into.
        private bool CollideCars(ReadOnlyCollection<CityCar> cars, ref OrientedRect footprint)
        {
            if (cars == null) return false;
            bool touched = false;
            foreach (var car in cars)
            {
                if (Math.Abs(car.X - X) > 6 || Math.Abs(car.Z - Z) > 6) continue;
                var other = car.Footprint;
                float depth = footprint.Penetration(other, out float cnx, out float cnz);
                if (depth <= 0) continue;
                ContactPoint(footprint, other, cnx, cnz, out float cx, out float cz);
                Resolve(cx, cz, cnx, cnz, depth, CarBounce, WallFriction, ImpactInertia); touched = true; footprint = Footprint;
            }
            return touched;
        }
        // A post of the given radius (0 for a kerb corner) against the car's rectangle.
        private bool PushOffPoint(OrientedRect footprint, float x, float z, float radius, float bounce, float friction)
        {
            footprint.Local(x, z, out float along, out float side);
            float ca = Math.Max(-HalfLength, Math.Min(HalfLength, along)), cs = Math.Max(-HalfWidth, Math.Min(HalfWidth, side));
            float da = ca - along, ds = cs - side, gap = (float)Math.Sqrt(da * da + ds * ds);
            float na, ns, depth;
            if (gap > 1e-4f)
            {
                if (gap >= radius) return false;
                na = da / gap; ns = ds / gap; depth = radius - gap;
            }
            else
            {
                // The post is inside the rectangle: leave through the nearest side.
                float toEnd = HalfLength - Math.Abs(along), toSide = HalfWidth - Math.Abs(side);
                if (toSide < toEnd) { na = 0; ns = side > 0 ? -1 : 1; depth = toSide + radius; }
                else { na = along > 0 ? -1 : 1; ns = 0; depth = toEnd + radius; }
            }
            if (depth <= 1e-5f) return false;
            float nx = na * footprint.Cos - ns * footprint.Sin, nz = na * footprint.Sin + ns * footprint.Cos;
            footprint.World(ca, cs, out float px, out float pz);
            Resolve(px, pz, nx, nz, depth, bounce, friction, ImpactInertia);
            return true;
        }
        // The deepest corner of one rectangle inside the other, as the point where two cars touch.
        private static void ContactPoint(OrientedRect self, OrientedRect other, float nx, float nz, out float x, out float z)
        {
            float best = float.MaxValue; x = (self.X + other.X) / 2; z = (self.Z + other.Z) / 2;
            for (int i = 0; i < 4; i++)
            {
                self.Corner(i, out float cx, out float cz);
                float d = cx * nx + cz * nz;
                if (other.Contains(cx, cz, 0.05f) && d < best) { best = d; x = cx; z = cz; }
            }
            if (best < float.MaxValue) return;
            float far = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                other.Corner(i, out float cx, out float cz);
                float d = cx * nx + cz * nz;
                if (self.Contains(cx, cz, 0.05f) && d > far) { far = d; x = cx; z = cz; }
            }
        }
        // Moves the car out along the normal and removes the closing speed at the contact point, with a little bounce and friction.
        private void Resolve(float px, float pz, float nx, float nz, float depth, float bounce, float friction, float inertia)
        {
            X += nx * (depth + 0.001f); Z += nz * (depth + 0.001f); Contacts++;
            float c = (float)Math.Cos(Heading), s = (float)Math.Sin(Heading);
            float ox = px - X, oz = pz - Z, oa = ox * c + oz * s, os = -ox * s + oz * c;
            float na = nx * c + nz * s, ns = -nx * s + nz * c;
            float va = Forward - YawRate * os, vs = Lateral + YawRate * oa;
            float closing = va * na + vs * ns;
            if (closing >= 0) return;
            float rn = oa * ns - os * na;
            float j = -(1 + bounce) * closing / (1 / Mass + rn * rn / inertia);
            float ta = -ns, ts = na, vt = va * ta + vs * ts, rt = oa * ts - os * ta;
            float jt = -vt / (1 / Mass + rt * rt / inertia);
            jt = Math.Max(-friction * j, Math.Min(friction * j, jt));
            Forward += (j * na + jt * ta) / Mass; Lateral += (j * ns + jt * ts) / Mass;
            YawRate += (j * rn + jt * rt) / inertia;
        }
        private static float MoveTowards(float value, float target, float step)
            => Math.Abs(target - value) <= step ? target : value + Math.Sign(target - value) * step;
        private static float Wrap(float angle)
        {
            const float pi = (float)Math.PI;
            angle %= 2 * pi;
            if (angle > pi) angle -= 2 * pi; else if (angle <= -pi) angle += 2 * pi;
            return angle;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
