using System;
using System.Collections.Generic;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class CityTrafficTests
    {
        private static void Run(CrosswalkSignal signal, double seconds, bool car = false, bool people = false)
        { for (double t = 0; t < seconds - 1e-9; t += 0.01) signal.Advance(Math.Min(0.01, seconds - t), car, people); }

        [Test] public void SignalRunsTheFullCycleInThirtyFourSecondsWithoutHolds()
        {
            var s = new CrosswalkSignal(); Assert.That(CrosswalkSignal.CycleSeconds, Is.EqualTo(34));
            var phases = new List<(CrosswalkPhase phase, double start)>(); double t = 0; var last = s.Phase; phases.Add((last, 0));
            for (int i = 0; i < 3410; i++)
            {
                s.Advance(0.01, false, false); t += 0.01;
                if (s.Phase != last) { last = s.Phase; phases.Add((last, t)); }
            }
            Assert.That(phases.Count, Is.EqualTo(7));
            var expected = new[] { (CrosswalkPhase.CarsGo, 0.0), (CrosswalkPhase.CarsAmber, 14.0), (CrosswalkPhase.ClearToWalk, 17.0), (CrosswalkPhase.Walk, 18.0),
                (CrosswalkPhase.WalkEnding, 28.0), (CrosswalkPhase.ClearToCars, 32.0), (CrosswalkPhase.CarsGo, 34.0) };
            for (int i = 0; i < expected.Length; i++)
            { Assert.That(phases[i].phase, Is.EqualTo(expected[i].Item1)); Assert.That(phases[i].start, Is.EqualTo(expected[i].Item2).Within(0.02)); }
        }
        [Test] public void LightsMatchThePhases()
        {
            var s = new CrosswalkSignal();
            Assert.That(s.CarsGreen, Is.True); Assert.That(s.PedestrianGreen, Is.False); Assert.That(s.SecondsUntilWalk, Is.EqualTo(18).Within(1e-9));
            Run(s, 15); Assert.That(s.CarsAmber, Is.True); Assert.That(s.CarsGreen, Is.False); Assert.That(s.PedestrianGreen, Is.False);
            Run(s, 3.5); Assert.That(s.Phase, Is.EqualTo(CrosswalkPhase.Walk)); Assert.That(s.PedestrianGreen, Is.True); Assert.That(s.PedestrianFlashing, Is.False);
            Assert.That(s.WalkSecondsLeft, Is.EqualTo(13.5).Within(0.02)); Assert.That(s.SecondsUntilWalk, Is.Zero);
            Run(s, 10); Assert.That(s.PedestrianFlashing, Is.True); Assert.That(s.CarsAmber || s.CarsGreen, Is.False);
            Run(s, 4); Assert.That(s.Phase, Is.EqualTo(CrosswalkPhase.ClearToCars)); Assert.That(s.PedestrianGreen, Is.False); Assert.That(s.WalkSecondsLeft, Is.Zero);
            Assert.That(s.SecondsUntilWalk, Is.EqualTo(1.5 + 14 + 3 + 1).Within(0.02));
        }
        [Test] public void ButtonShortensTheCarsGreenOnceAndIsSpentAtTheAmber()
        {
            var s = new CrosswalkSignal(); Run(s, 2);
            Assert.That(s.RequestWalk(), Is.True); Assert.That(s.RequestWalk(), Is.False, "a second press changes nothing");
            Assert.That(s.WalkRequested, Is.True); Assert.That(s.SecondsUntilWalk, Is.EqualTo(8).Within(0.02));
            Run(s, 3.9); Assert.That(s.Phase, Is.EqualTo(CrosswalkPhase.CarsGo)); Run(s, 0.2); Assert.That(s.Phase, Is.EqualTo(CrosswalkPhase.CarsAmber));
            Assert.That(s.WalkRequested, Is.False); Assert.That(s.RequestWalk(), Is.False, "no press during the amber");
            Run(s, 4); Assert.That(s.Phase, Is.EqualTo(CrosswalkPhase.Walk)); Assert.That(s.RequestWalk(), Is.False);
            Run(s, 15); Assert.That(s.Phase, Is.EqualTo(CrosswalkPhase.ClearToCars));
            Assert.That(s.RequestWalk(), Is.True, "a press in the all-red shortens the next green");
            Run(s, 0.9 + 5.9); Assert.That(s.Phase, Is.EqualTo(CrosswalkPhase.CarsGo)); Run(s, 0.2); Assert.That(s.Phase, Is.EqualTo(CrosswalkPhase.CarsAmber));
        }
        [Test] public void LatePressEndsTheGreenWithAFullAmber()
        {
            var s = new CrosswalkSignal(); Run(s, 10); Assert.That(s.RequestWalk(), Is.True);
            Assert.That(s.SecondsUntilWalk, Is.EqualTo(4).Within(1e-9));
            s.Advance(0.02, false, false); Assert.That(s.Phase, Is.EqualTo(CrosswalkPhase.CarsAmber)); Assert.That(s.PhaseElapsed, Is.LessThanOrEqualTo(0.02 + 1e-9));
            Run(s, 2.9); Assert.That(s.Phase, Is.EqualTo(CrosswalkPhase.CarsAmber), "the amber is never skipped");
            Run(s, 1.2); Assert.That(s.Phase, Is.EqualTo(CrosswalkPhase.Walk));
        }
        [Test] public void WalkWaitsForCarsOnTheZebraAndCarsWaitForPeople()
        {
            var s = new CrosswalkSignal(); Run(s, 17.5);
            Assert.That(s.Phase, Is.EqualTo(CrosswalkPhase.ClearToWalk));
            Run(s, 5, car: true); Assert.That(s.Phase, Is.EqualTo(CrosswalkPhase.ClearToWalk)); Assert.That(s.PhaseElapsed, Is.EqualTo(CrosswalkSignal.ClearToWalkSeconds).Within(1e-9));
            Run(s, 0.05); Assert.That(s.Phase, Is.EqualTo(CrosswalkPhase.Walk));
            Run(s, 15); Assert.That(s.Phase, Is.EqualTo(CrosswalkPhase.ClearToCars));
            Run(s, 20, people: true); Assert.That(s.Phase, Is.EqualTo(CrosswalkPhase.ClearToCars), "nobody is left on the zebra under moving cars");
            Run(s, 0.05); Assert.That(s.Phase, Is.EqualTo(CrosswalkPhase.CarsGo));
        }
        [Test] public void InvalidSignalTimeIsRejected()
        {
            var s = new CrosswalkSignal();
            Assert.Throws<ArgumentOutOfRangeException>(() => s.Advance(double.NaN, false, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.Advance(-0.1, false, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.Advance(double.PositiveInfinity, false, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityTraffic().Advance(-1, s));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityLife().Advance(double.NaN, false, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CityTraffic.LaneZ(4));
        }
        [Test] public void HintsAndGatesFollowTheSignal()
        {
            var s = new CrosswalkSignal();
            Assert.That(CityCrosswalk.Hint(s), Does.Contain("Кнопка на столбе — E")); Assert.That(CityCrosswalk.GatesOpen(s, false), Is.False);
            Assert.That(CityCrosswalk.GatesOpen(s, true), Is.True, "nobody is locked on the zebra");
            s.RequestWalk(); Assert.That(CityCrosswalk.Hint(s), Does.Contain("Зелёный ≈ через 10 с"));
            Run(s, 6 + 3 + 1 + 0.5); Assert.That(CityCrosswalk.Hint(s), Does.StartWith("Пешеходам зелёный · 14 с")); Assert.That(CityCrosswalk.GatesOpen(s, false), Is.True);
            Run(s, 10); Assert.That(CityCrosswalk.Hint(s), Does.Contain("Не начинайте переход"));
            Run(s, 4); Assert.That(CityCrosswalk.Hint(s), Does.Contain("когда зебра свободна")); Assert.That(CityCrosswalk.GatesOpen(s, false), Is.False);
            Assert.That(CityCrosswalk.Hint(null), Is.Empty);
            Assert.That(CityCrosswalk.PlayerInZone(0, 35), Is.True); Assert.That(CityCrosswalk.PlayerInZone(0, 28.2f), Is.True);
            Assert.That(CityCrosswalk.PlayerInZone(0, 27.5f), Is.False); Assert.That(CityCrosswalk.PlayerInZone(3, 35), Is.False);
            Assert.That(CityCrosswalk.Near(0, 26.5f), Is.True); Assert.That(CityCrosswalk.Near(-31, 16), Is.False);
            Assert.That(CityCrosswalk.HalfWidth, Is.LessThan(CityCrosswalk.GateHalfWidth)); Assert.That(CityCrosswalk.GateHalfWidth, Is.LessThan(CityCrosswalk.CorridorX));
        }
        [Test] public void TwelveCarsDriveRightHandInFourLanes()
        {
            var traffic = new CityTraffic(); Assert.That(traffic.Cars.Count, Is.EqualTo(12));
            var styles = new HashSet<CarStyle>();
            foreach (var car in traffic.Cars)
            {
                Assert.That(car.Z, Is.InRange(CityDistrictPlan.SouthKerbZ + 1.5f, CityDistrictPlan.NorthKerbZ - 1.5f)); styles.Add(car.Style);
                Assert.That(car.Yaw, Is.EqualTo(CityTraffic.Eastbound(car.Lane) ? 0 : 180));
                Assert.That(CityTraffic.Eastbound(car.Lane), Is.EqualTo(car.Z < 35), "eastbound lanes are the near ones");
                Assert.That(car.Cruise, Is.InRange(CityTraffic.MinCruise, CityTraffic.MaxCruise));
            }
            Assert.That(styles.Count, Is.EqualTo(5), "every body drives the boulevard");
            var paints = new HashSet<int>();
            foreach (var car in traffic.Cars)
            {
                paints.Add(car.Paint); Assert.That(car.Paint == CarModels.TaxiPaint, Is.EqualTo(car.Style == CarStyle.Taxi));
                Assert.That(car.Braking, Is.False, "nobody brakes before the first step");
            }
            Assert.That(paints.Count, Is.GreaterThanOrEqualTo(4), "a street of different colours");
            foreach (var car in traffic.Cars)
            {
                var shape = CarModels.Shape(car.Style);
                Assert.That(car.Length, Is.EqualTo(shape.Length)); Assert.That(car.Width, Is.EqualTo(shape.Width));
                var footprint = car.Footprint;
                Assert.That(footprint.HalfLength, Is.EqualTo(shape.Length / 2)); Assert.That(footprint.HalfWidth, Is.EqualTo(shape.Width / 2));
                Assert.That(footprint.X, Is.EqualTo(car.X)); Assert.That(footprint.Z, Is.EqualTo(car.Z));
                Assert.That(footprint.Heading, Is.EqualTo(car.Yaw * (float)Math.PI / 180).Within(1e-5f), "the footprint faces the way the car drives");
            }
            Assert.That(CityTraffic.RoadHalf, Is.InRange(CityScenery.TunnelPortalX + 4, CityScenery.TunnelEndX - 2), "cars wrap inside the tunnels");
        }

        private static double Overlap(CityCar car) => Math.Min(car.X + car.Length / 2, CityCrosswalk.HalfWidth) - Math.Max(car.X - car.Length / 2, -CityCrosswalk.HalfWidth);
        [Test] public void TrafficKeepsGapsAndNeverDrivesOverTheZebraOnTheWalk()
        {
            var life = new CityLife(); var travelled = new double[12]; var last = new float[12];
            for (int i = 0; i < 12; i++) last[i] = life.Traffic.Cars[i].S;
            int walkFrames = 0, queued = 0, crossings = 0; bool wasGreen = false;
            for (int frame = 0; frame < 30000; frame++)
            {
                if (frame % 2500 == 100) life.Signal.RequestWalk();
                life.Advance(0.02, false, 0, 0);
                var cars = life.Traffic.Cars;
                for (int i = 0; i < 12; i++)
                {
                    var car = cars[i];
                    float moved = car.S - last[i]; if (moved < -CityTraffic.RoadHalf) moved += CityTraffic.RingLength;
                    Assert.That(moved, Is.InRange(-1e-4, CityTraffic.MaxCruise * 0.02 + 1e-3), "cars never reverse or jump");
                    travelled[i] += moved; last[i] = car.S;
                    Assert.That(car.Speed, Is.InRange(0, CityTraffic.MaxCruise + 1e-3));
                    for (int j = 0; j < 12; j++)
                    {
                        if (j == i || cars[j].Lane != car.Lane) continue;
                        float gap = cars[j].S - car.S; if (gap < 0) gap += CityTraffic.RingLength;
                        if (gap < CityTraffic.RingLength / 2) Assert.That(gap, Is.GreaterThanOrEqualTo((car.Length + cars[j].Length) / 2 + CityTraffic.BumperGap - 1e-3), "no overlap in lane " + car.Lane);
                    }
                    if (life.Signal.PedestrianGreen) Assert.That(Overlap(car), Is.LessThanOrEqualTo(0), "car " + i + " on the zebra during the walk");
                    if (life.Signal.Phase == CrosswalkPhase.Walk && car.Speed < 0.01f && Math.Abs(CityTraffic.Eastbound(car.Lane) ? car.X + 4.2f + car.Length / 2 : car.X - 4.2f - car.Length / 2) < 0.05f) queued++;
                }
                if (life.Signal.PedestrianGreen) walkFrames++;
                if (life.Signal.PedestrianGreen && !wasGreen) crossings++;
                wasGreen = life.Signal.PedestrianGreen;
            }
            Assert.That(walkFrames, Is.GreaterThan(30000 / 34 * 14 / 2), "the walk comes every cycle");
            Assert.That(crossings, Is.GreaterThanOrEqualTo(15));
            Assert.That(queued, Is.GreaterThan(1000), "cars wait at the stop lines on red");
            foreach (double distance in travelled) Assert.That(distance, Is.GreaterThan(1500), "no car is stuck");
        }
        [Test] public void BrakeLampsLightWhileCarsSlowDownOrStand()
        {
            var life = new CityLife();
            life.Advance(0.5, false, 0, 0);
            int cruising = 0;
            foreach (var car in life.Traffic.Cars)
                if (Math.Abs(car.Speed - car.Cruise) < 1e-4f) { cruising++; Assert.That(car.Braking, Is.False, "a cruising car shows no brake lamps"); }
            Assert.That(cruising, Is.GreaterThan(4));
            Assert.That(life.Signal.RequestWalk(), Is.True);
            int slowing = 0;
            for (int i = 0; i < 600 && life.Signal.Phase != CrosswalkPhase.Walk; i++)
            {
                var before = new float[12]; for (int c = 0; c < 12; c++) before[c] = life.Traffic.Cars[c].Speed;
                life.Advance(0.05, false, 0, 0);
                for (int c = 0; c < 12; c++)
                {
                    var car = life.Traffic.Cars[c];
                    if (car.Speed < before[c] - 0.1f) { slowing++; Assert.That(car.Braking, Is.True, "car " + c + " slows down"); }
                }
            }
            Assert.That(slowing, Is.GreaterThan(0), "cars brake for the red light");
            life.Advance(6, false, 0, 0);
            int standing = 0;
            foreach (var car in life.Traffic.Cars)
                if (car.Speed < CityTraffic.StandingSpeed) { standing++; Assert.That(car.Braking, Is.True, "a car waiting at the line holds the brake"); }
            Assert.That(standing, Is.GreaterThanOrEqualTo(2));
        }
        [Test] public void TrafficAndSignalAreDeterministic()
        {
            var a = new CityLife(); var b = new CityLife();
            a.Advance(97.3, false, 0, 0);
            for (int i = 0; i < 973; i++) b.Advance(0.1, false, 0, 0);
            Assert.That(b.Signal.Phase, Is.EqualTo(a.Signal.Phase)); Assert.That(b.Elapsed, Is.EqualTo(a.Elapsed).Within(1e-6));
            for (int i = 0; i < 12; i++) Assert.That(b.Traffic.Cars[i].S, Is.EqualTo(a.Traffic.Cars[i].S).Within(0.05f));
            for (int i = 0; i < a.Crowd.Walkers.Count; i++)
            { Assert.That(b.Crowd.Walkers[i].X, Is.EqualTo(a.Crowd.Walkers[i].X).Within(0.05f)); Assert.That(b.Crowd.Walkers[i].Z, Is.EqualTo(a.Crowd.Walkers[i].Z).Within(0.05f)); }
        }
        [Test] public void PlayerOnTheZebraHoldsTheCarsAndKeepsTheGatesOpen()
        {
            var life = new CityLife(); Assert.That(life.Signal.RequestWalk(), Is.True);
            for (int i = 0; i < 600 && life.Signal.Phase != CrosswalkPhase.Walk; i++) life.Advance(0.1, false, 0, 0);
            Assert.That(life.Signal.Phase, Is.EqualTo(CrosswalkPhase.Walk)); Assert.That(life.Elapsed, Is.LessThan(15)); Assert.That(life.GatesOpen, Is.True);
            life.Advance(60, true, 0, 35);
            Assert.That(life.PlayerOnCrossing, Is.True); Assert.That(life.Signal.Phase, Is.EqualTo(CrosswalkPhase.ClearToCars)); Assert.That(life.GatesOpen, Is.True);
            foreach (var car in life.Traffic.Cars) Assert.That(Overlap(car), Is.LessThanOrEqualTo(0));
            life.Advance(0.5, true, 0, 44); Assert.That(life.PlayerOnCrossing, Is.False);
            life.Advance(10, true, 0, 44); Assert.That(life.Signal.Phase, Is.Not.EqualTo(CrosswalkPhase.ClearToCars)); Assert.That(life.GatesOpen, Is.False);
            Assert.That(life.Hint, Is.Not.Empty);
        }
    }
}
