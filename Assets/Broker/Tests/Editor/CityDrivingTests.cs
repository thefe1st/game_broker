using System;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class CityDrivingTests
    {
        private static float WorstOverlap(CityLife life)
        {
            float worst = 0; var footprint = life.Car.Footprint;
            foreach (var car in life.Traffic.Cars) worst = Math.Max(worst, footprint.Penetration(car.Footprint, out _, out _));
            return worst;
        }

        [Test] public void KeysWaitForTheDriverAndTheCarStaysInTheLayByUntilThen()
        {
            var life = new CityLife(); life.Input = new DriveInput(1, 1, false);
            life.Advance(5, true, 0, 20);
            Assert.That(life.Driving, Is.False); Assert.That(life.Car.X, Is.EqualTo(CityRoads.ParkX)); Assert.That(life.Car.Parked, Is.True);
            Assert.That(life.EnterCar(), Is.True); Assert.That(life.EnterCar(), Is.False); Assert.That(life.Input.IsIdle, Is.True);
            life.Input = new DriveInput(1, 0, false);
            life.Advance(2, true, 0, 35);
            Assert.That(life.Car.X, Is.GreaterThan(CityRoads.ParkX + 1)); Assert.That(life.PlayerOnCrossing, Is.False);
        }
        [Test] public void TheDriverGetsOutOnlyWithTheCarStandingInTheLayBy()
        {
            var life = new CityLife();
            Assert.That(life.LeaveCar(), Is.False);
            Assert.That(life.EnterCar(), Is.True); Assert.That(life.LeaveCar(), Is.True); Assert.That(life.Driving, Is.False);
            life.EnterCar(); life.Car.Place(10, CityTraffic.LaneZ(0), 0);
            Assert.That(life.LeaveCar(), Is.False); Assert.That(life.Driving, Is.True);
            life.Car.Place(CityRoads.ParkX, CityRoads.ParkZ, 0, 3);
            Assert.That(life.LeaveCar(), Is.False);
            life.Car.Place(CityRoads.ParkX + 1, CityRoads.ParkZ + 0.4f, 0.1f);
            life.Input = new DriveInput(0, 1, true);
            Assert.That(life.LeaveCar(), Is.True); Assert.That(life.Car.Speed, Is.Zero); Assert.That(life.Input.IsIdle, Is.True);
            life.EnterCar(); life.Input = new DriveInput(1, 0, false); life.Advance(3, true, 0, 20);
            life.ParkCar();
            Assert.That(life.Driving, Is.False); Assert.That(life.Input.IsIdle, Is.True); Assert.That(life.Car.Parked, Is.True);
            Assert.That(life.Car.X, Is.EqualTo(CityRoads.ParkX)); Assert.That(life.Car.Z, Is.EqualTo(CityRoads.ParkZ));
        }
        [Test] public void AParkedCarLeavesTheBoulevardAsItWas()
        {
            var life = new CityLife(); var signal = new CrosswalkSignal(); var traffic = new CityTraffic(); var crowd = new CityCrowd();
            for (int i = 0; i < 2400; i++)
            {
                life.Advance(0.05, true, 0, 20);
                signal.Advance(0.05, traffic.CarInCrosswalk, crowd.AnyOnCrossing);
                traffic.Advance(0.05, signal); crowd.Advance(0.05, signal, true, 0, 20);
            }
            Assert.That(life.Signal.Phase, Is.EqualTo(signal.Phase)); Assert.That(life.Signal.PhaseElapsed, Is.EqualTo(signal.PhaseElapsed));
            for (int i = 0; i < traffic.Cars.Count; i++) { Assert.That(life.Traffic.Cars[i].S, Is.EqualTo(traffic.Cars[i].S)); Assert.That(life.Traffic.Cars[i].Speed, Is.EqualTo(traffic.Cars[i].Speed)); }
            for (int i = 0; i < crowd.Walkers.Count; i++) { Assert.That(life.Crowd.Walkers[i].X, Is.EqualTo(crowd.Walkers[i].X)); Assert.That(life.Crowd.Walkers[i].Z, Is.EqualTo(crowd.Walkers[i].Z)); }
            Assert.That(life.Car.Parked, Is.True); Assert.That(life.Car.X, Is.EqualTo(CityRoads.ParkX)); Assert.That(life.CarOnZebra, Is.False);
        }
        [Test] public void BoulevardCarsQueueBehindTheStandingCarAndNeverTouchIt()
        {
            var life = new CityLife(); life.EnterCar();
            const float x = 30; life.Car.Place(x, CityTraffic.LaneZ(0), 0);
            Assert.That(WorstOverlap(life), Is.LessThanOrEqualTo(0));
            bool queued = false; float otherLane = 0;
            for (int i = 0; i < 1200; i++)
            {
                life.Advance(0.05, true, 0, 20);
                Assert.That(WorstOverlap(life), Is.LessThanOrEqualTo(0)); Assert.That(life.Car.X, Is.EqualTo(x));
                foreach (var car in life.Traffic.Cars)
                {
                    if (car.Lane == 0 && car.Speed < CityTraffic.StandingSpeed && car.X < x && x - car.X < 9) queued = true;
                    if (car.Lane == 1) otherLane = Math.Max(otherLane, car.Speed);
                }
            }
            Assert.That(queued, Is.True); Assert.That(otherLane, Is.GreaterThan(CityTraffic.MinCruise - 0.5f));
            // Back in the lay-by, it frees the lane.
            life.Car.Park();
            life.Advance(10, true, 0, 20);
            foreach (var car in life.Traffic.Cars) if (car.Lane == 0 && car.X < x && x - car.X < 9) Assert.That(car.Speed, Is.GreaterThan(1));
        }
        [Test] public void ThePlayersCarOnTheZebraHoldsTheWalkUntilItLeaves()
        {
            var life = new CityLife(); life.EnterCar(); life.Car.Place(0, CityTraffic.LaneZ(0), 0);
            Assert.That(life.CarOnZebra, Is.True);
            for (int i = 0; i < 1200; i++) { life.Advance(0.05, true, 0, 20); Assert.That(life.Signal.Phase, Is.Not.EqualTo(CrosswalkPhase.Walk)); }
            Assert.That(life.Signal.Phase, Is.EqualTo(CrosswalkPhase.ClearToWalk));
            life.Car.Place(30, CityTraffic.LaneZ(0), 0); Assert.That(life.CarOnZebra, Is.False);
            bool walk = false;
            for (int i = 0; i < 200 && !walk; i++) { life.Advance(0.05, true, 0, 20); walk = life.Signal.Phase == CrosswalkPhase.Walk; }
            Assert.That(walk, Is.True);
        }
        [Test] public void WalkersAndTheCarNeverOverlapOnTheZebra()
        {
            // The life's order of steps without the boulevard cars: the car rolls back and forth over the zebra.
            var signal = new CrosswalkSignal(); var crowd = new CityCrowd(); var car = new PlayerCar();
            float lane = CityTraffic.LaneZ(0); car.Place(-12, lane, 0);
            bool forward = true; int assisted = 0;
            for (int i = 0; i < 4800; i++)
            {
                if (forward ? car.X > 12 : car.X < -12) forward = !forward;
                float offset = (car.Z - lane) * 0.8f, steer = car.Forward >= 0 ? offset + car.Heading * 2 : offset - car.Heading * 2;
                var keys = new DriveInput(Math.Abs(car.Forward) * 3.6f < 12 ? (forward ? 0.5f : -0.5f) : 0, steer, false);
                if (i % 40 == 0) signal.RequestWalk();
                var footprint = car.Footprint;
                signal.Advance(0.05, footprint.Overlaps(CityLife.ZebraZone), crowd.AnyOnCrossing);
                crowd.Advance(0.05, signal, false, 0, 0, footprint);
                car.Advance(0.05, keys, null, crowd);
                if (keys.Throttle * car.Forward > 0 && car.Braking) assisted++;
                footprint = car.Footprint;
                foreach (var walker in crowd.Walkers)
                    Assert.That(footprint.Distance(walker.X, walker.Z), Is.GreaterThan(CityCrowd.BodyRadius - 0.03f), $"walker {walker.Index} at step {i}");
                Assert.That(Math.Abs(car.Z - lane), Is.LessThan(1.2f));
            }
            Assert.That(assisted, Is.GreaterThan(0));
        }
        [Test] public void WalkersStopBeforeACarLeftStandingOnTheZebra()
        {
            var signal = new CrosswalkSignal(); var crowd = new CityCrowd(); var car = new PlayerCar();
            signal.RequestWalk();
            for (int i = 0; i < 2000 && signal.Phase != CrosswalkPhase.Walk; i++) { signal.Advance(0.05, false, crowd.AnyOnCrossing); crowd.Advance(0.05, signal, false, 0, 0); }
            Assert.That(signal.Phase, Is.EqualTo(CrosswalkPhase.Walk));
            car.Place(0, CityTraffic.LaneZ(1), 0);
            int held = 0;
            for (int i = 0; i < 400; i++)
            {
                var footprint = car.Footprint;
                signal.Advance(0.05, footprint.Overlaps(CityLife.ZebraZone), crowd.AnyOnCrossing);
                crowd.Advance(0.05, signal, false, 0, 0, footprint);
                car.Advance(0.05, DriveInput.None, null, crowd);
                foreach (var walker in crowd.Walkers)
                {
                    float gap = footprint.Distance(walker.X, walker.Z);
                    Assert.That(gap, Is.GreaterThan(CityCrowd.BodyRadius));
                    if (walker.InCrosswalk && !walker.Moving && gap < 2) held++;
                }
            }
            Assert.That(held, Is.GreaterThan(0)); Assert.That(car.X, Is.Zero); Assert.That(signal.Phase, Is.Not.EqualTo(CrosswalkPhase.CarsGo));
        }
        [Test] public void BoulevardCarsFollowTheSlowPlayerCarWithoutTouchingIt()
        {
            var life = new CityLife(); life.EnterCar();
            float lane = CityTraffic.LaneZ(0); life.Car.Place(-60, lane, 0, 15 / 3.6f);
            float closest = float.MaxValue;
            for (int i = 0; i < 1200 && life.Car.X < 60; i++)
            {
                var car = life.Car;
                float steer = (car.Z - lane) * 0.8f + car.Heading * 2;
                life.Input = new DriveInput((15 / 3.6f - car.Forward) * 1.5f, steer, false);
                life.Advance(0.05, true, 0, 20);
                Assert.That(WorstOverlap(life), Is.LessThan(0.03f));
                foreach (var other in life.Traffic.Cars)
                    if (other.Lane == 0 && other.X < car.X) closest = Math.Min(closest, car.X - PlayerCar.HalfLength - (other.X + other.Length / 2));
            }
            Assert.That(life.Car.X, Is.GreaterThan(30));
            Assert.That(closest, Is.InRange(CityTraffic.BumperGap - 0.05f, 6));
        }
    }
}
