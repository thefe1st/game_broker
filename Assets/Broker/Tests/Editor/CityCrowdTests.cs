using System;
using System.Collections.Generic;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class CityCrowdTests
    {
        private static IEnumerable<(int route, int segment, float x, float z, float dx, float dz)> Samples()
        {
            for (int r = 0; r < CityCrowd.Routes.Count; r++)
            {
                var route = CityCrowd.Routes[r];
                for (int s = 0; s < route.Count; s++)
                {
                    int j = (s + 1) % route.Count; float length = route.SegmentLength(s);
                    float dx = (route.X[j] - route.X[s]) / length, dz = (route.Z[j] - route.Z[s]) / length;
                    for (float a = 0; a <= length; a += 0.25f) yield return (r, s, route.X[s] + dx * a, route.Z[s] + dz * a, dx, dz);
                }
            }
        }
        [Test] public void RoutesStayOnWalkableFloorClearOfPropsFiguresAndWalls()
        {
            double closest = double.MaxValue;
            foreach (var p in Samples())
            {
                string at = "route " + p.route + " segment " + p.segment + " at " + p.x + "/" + p.z;
                Assert.That(CityDistrictPlan.CanWalk(p.x, p.z), Is.True, at);
                foreach (float side in new[] { -CityCrowd.BodyRadius, CityCrowd.BodyRadius })
                    Assert.That(CityDistrictPlan.CanWalk(p.x + p.dz * side, p.z - p.dx * side), Is.True, at + " shoulder");
                foreach (var rect in CityScenery.Obstacles) closest = Math.Min(closest, rect.Distance(p.x, p.z));
                foreach (var figure in CityCrowd.Figures) closest = Math.Min(closest, figure.Footprint.Distance(p.x, p.z));
            }
            Assert.That(closest, Is.GreaterThanOrEqualTo(CityCrowd.BodyRadius + 0.2f), "walkers brush past props, never through them");
        }
        [Test] public void OnlyTheExchangeRouteCrossesTheBoulevardAndOnlyOnTheZebra()
        {
            int crossings = 0;
            for (int r = 0; r < CityCrowd.Routes.Count; r++)
                for (int s = 0; s < CityCrowd.Routes[r].Count; s++)
                    if (CityCrowd.Routes[r].IsCrossing(s))
                    {
                        crossings++; Assert.That(r, Is.EqualTo(4));
                        Assert.That(Math.Abs(CityCrowd.Routes[r].X[s]), Is.LessThanOrEqualTo(CityCrosswalk.HalfWidth - CityCrowd.BodyRadius));
                        Assert.That(CityCrowd.Routes[r].SegmentLength(s) / CityCrowd.CrossSpeed, Is.LessThan(CrosswalkSignal.WalkSeconds + CrosswalkSignal.WalkEndingSeconds));
                    }
            Assert.That(crossings, Is.EqualTo(2), "there and back");
            foreach (var p in Samples())
                if (p.z > CityDistrictPlan.SouthKerbZ && p.z < CityDistrictPlan.NorthKerbZ)
                { Assert.That(p.route, Is.EqualTo(4)); Assert.That(CityCrowd.Routes[4].IsCrossing(p.segment), Is.True); }
        }
        [Test] public void FiguresStandOrSitInsideTheCafeAndTheHall()
        {
            Assert.That(CityCrowd.Figures.Count, Is.EqualTo(4)); var styles = new HashSet<WalkerStyle>();
            foreach (var figure in CityCrowd.Figures)
            {
                Assert.That(CityDistrictPlan.CanWalk(figure.X, figure.Z), Is.True, figure.Style.ToString()); styles.Add(figure.Style);
                if (figure.Seated) continue;
                foreach (var rect in CityScenery.Obstacles) Assert.That(rect.Distance(figure.X, figure.Z), Is.GreaterThan(CityCrowd.BodyRadius), figure.Style + " inside a prop");
            }
            Assert.That(styles, Is.EquivalentTo(new[] { WalkerStyle.Barista, WalkerStyle.Analyst, WalkerStyle.Guard, WalkerStyle.Trader }));
            Assert.That(CityDistrictPlan.Openings[0].Contains(CityCrowd.Figures[0].X, CityCrowd.Figures[0].Z), Is.True, "the barista is in the café");
            Assert.That(CityDistrictPlan.Openings[2].Contains(CityCrowd.Figures[2].X, CityCrowd.Figures[2].Z), Is.True, "the guard is in the hall");
        }
        [Test] public void FourteenWalkersStartOffTheZebraAndWalkDeterministically()
        {
            var a = new CityCrowd(); var b = new CityCrowd(); var signal = new CrosswalkSignal();
            Assert.That(a.Walkers.Count, Is.EqualTo(14)); Assert.That(a.AnyOnCrossing, Is.False);
            var styles = new HashSet<WalkerStyle>();
            foreach (var w in a.Walkers) { styles.Add(w.Style); Assert.That(w.Speed, Is.InRange(CityCrowd.MinSpeed, CityCrowd.MaxSpeed)); }
            Assert.That(styles.Count, Is.GreaterThanOrEqualTo(8));
            a.Advance(60, signal, false, 0, 0); b.Advance(60, signal, false, 0, 0);
            for (int i = 0; i < a.Walkers.Count; i++)
            { Assert.That(b.Walkers[i].X, Is.EqualTo(a.Walkers[i].X)); Assert.That(b.Walkers[i].Z, Is.EqualTo(a.Walkers[i].Z)); }
            Assert.Throws<ArgumentOutOfRangeException>(() => a.Advance(double.NaN, signal, false, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => a.Advance(-1, signal, false, 0, 0));
        }
        private static double Overlap(CityCar car) => Math.Min(car.X + CityTraffic.CarLength / 2, CityCrosswalk.HalfWidth) - Math.Max(car.X - CityTraffic.CarLength / 2, -CityCrosswalk.HalfWidth);
        [Test] public void WalkersCrossOnlyOnTheGreenWhileTheCarsWait()
        {
            var life = new CityLife(); var walkers = life.Crowd.Walkers; var wasOn = new bool[walkers.Count];
            var start = new float[walkers.Count]; int crossings = 0, waits = 0;
            for (int frame = 0; frame < 30000; frame++)
            {
                if (frame % 3100 == 700) life.Signal.RequestWalk();
                life.Advance(0.02, false, 0, 0);
                for (int i = 0; i < walkers.Count; i++)
                {
                    var w = walkers[i];
                    if (w.InCrosswalk)
                    {
                        if (!wasOn[i]) { crossings++; Assert.That(life.Signal.Phase, Is.EqualTo(CrosswalkPhase.Walk), "walker " + i + " stepped onto the zebra"); }
                        Assert.That(life.Signal.Phase, Is.AnyOf(CrosswalkPhase.Walk, CrosswalkPhase.WalkEnding, CrosswalkPhase.ClearToCars));
                        foreach (var car in life.Traffic.Cars) Assert.That(Overlap(car), Is.LessThanOrEqualTo(0), "car on the zebra with walker " + i);
                    }
                    if (w.Waiting)
                    {
                        waits++; Assert.That(Math.Abs(w.X), Is.EqualTo(0.9f).Within(0.01f));
                        Assert.That(Math.Abs(w.Z - CityCrosswalk.SouthCurbZ) < 0.01f || Math.Abs(w.Z - CityCrosswalk.NorthCurbZ) < 0.01f, Is.True, "waiting at the kerb");
                    }
                    Assert.That(CityDistrictPlan.CanWalk(w.X, w.Z), Is.True);
                    wasOn[i] = w.InCrosswalk;
                }
            }
            Assert.That(crossings, Is.GreaterThanOrEqualTo(25)); Assert.That(waits, Is.GreaterThan(0));
            foreach (var w in walkers) Assert.That(w.Stride, Is.GreaterThan(300), "walker " + w.Index + " is not stuck");
        }
        [Test] public void WalkersStopForThePlayerAndGoOn()
        {
            var crowd = new CityCrowd(); var signal = new CrosswalkSignal(); var w = crowd.Walkers[0];
            double rad = w.Heading * Math.PI / 180; float px = w.X + (float)Math.Sin(rad) * 0.9f, pz = w.Z + (float)Math.Cos(rad) * 0.9f;
            float x = w.X, z = w.Z;
            crowd.Advance(3, signal, true, px, pz);
            Assert.That(w.Moving, Is.False); Assert.That(w.X, Is.EqualTo(x)); Assert.That(w.Z, Is.EqualTo(z));
            crowd.Advance(2, signal, false, px, pz);
            Assert.That(w.Moving, Is.True); Assert.That(Math.Abs(w.X - x) + Math.Abs(w.Z - z), Is.GreaterThan(1.5f));
        }
    }
}
