using System;
using System.Linq;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class DayCycleTests
    {
        private const float Minute = 1f / 60;
        private static float Luma(SkyLook.Rgb c) => 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;
        private static void Same(SkyLook.Rgb actual, float r, float g, float b, string what)
        {
            Assert.That(actual.R, Is.EqualTo(r).Within(1e-6f), what + " R"); Assert.That(actual.G, Is.EqualTo(g).Within(1e-6f), what + " G");
            Assert.That(actual.B, Is.EqualTo(b).Within(1e-6f), what + " B");
        }
        private static void Same(SkyLook.Rgb actual, SkyLook.Rgb expected, string what) => Same(actual, expected.R, expected.G, expected.B, what);
        private static void Travels(DayLight l, float[] travel, string what)
        {
            float length = (float)Math.Sqrt(travel[0] * travel[0] + travel[1] * travel[1] + travel[2] * travel[2]);
            Assert.That(l.LightX, Is.EqualTo(travel[0] / length).Within(1e-5f), what + " x");
            Assert.That(l.LightY, Is.EqualTo(travel[1] / length).Within(1e-5f), what + " y");
            Assert.That(l.LightZ, Is.EqualTo(travel[2] / length).Within(1e-5f), what + " z");
        }
        private static float[] Hours(float step) => Enumerable.Range(0, (int)Math.Round(24 / step)).Select(i => i * step).ToArray();

        [Test] public void TheWorkDayRunsFromNineToMidnightOnTheCityClock()
        {
            Assert.That(DayCycle.HourAt(0, BrokerDay.DurationSeconds), Is.EqualTo(9));
            Assert.That(DayCycle.HourAt(BrokerDay.DurationSeconds / 2, BrokerDay.DurationSeconds), Is.EqualTo(16.5f));
            Assert.That(DayCycle.HourAt(BrokerDay.DurationSeconds, BrokerDay.DurationSeconds), Is.EqualTo(24));
            Assert.That(DayCycle.HourAt(40, 600), Is.EqualTo(10).Within(1e-5f), "forty seconds an hour");
            Assert.That(DayCycle.HourAt(-5, 600), Is.EqualTo(9)); Assert.That(DayCycle.HourAt(900, 600), Is.EqualTo(24));
            Assert.That(DayCycle.HourAt(10, 0), Is.EqualTo(9)); Assert.That(DayCycle.HourAt(float.NaN, 600), Is.EqualTo(9));
            var day = new BrokerDay(); Assert.That(DayCycle.HourOf(day), Is.EqualTo(9)); Assert.That(DayCycle.HourOf(null), Is.EqualTo(9));
            day.AdvanceTime(BrokerDay.DurationSeconds * 0.75f);
            Assert.That(DayCycle.HourOf(day), Is.EqualTo(DayCycle.NightHour).Within(1e-3f), "night from 20:15: the last quarter of the day");
            // In real seconds: four minutes of the bright day, two and a half of night.
            float Seconds(float from, float to) => (to - from) / (DayCycle.EndHour - DayCycle.StartHour) * BrokerDay.DurationSeconds;
            Assert.That(Seconds(DayCycle.StartHour, DayCycle.DayHour), Is.EqualTo(240).Within(1e-3f));
            Assert.That(Seconds(DayCycle.NightHour, DayCycle.EndHour), Is.EqualTo(150).Within(1e-3f));
        }

        [Test] public void TheClockReadsHoursAndMinutes()
        {
            Assert.That(DayCycle.Clock(9), Is.EqualTo("09:00")); Assert.That(DayCycle.Clock(18.5f), Is.EqualTo("18:30"));
            Assert.That(DayCycle.Clock(20.25f), Is.EqualTo("20:15")); Assert.That(DayCycle.Clock(23.99f), Is.EqualTo("23:59"));
            Assert.That(DayCycle.Clock(24), Is.EqualTo("00:00")); Assert.That(DayCycle.Clock(-1), Is.EqualTo("23:00"));
            Assert.That(DayCycle.Clock(DayCycle.EndHour), Is.EqualTo("00:00"), "the day ends at midnight");
            Assert.That(DayCycle.Clock(DayCycle.HourOf(new BrokerDay())), Is.EqualTo("09:00"), "a fresh day starts at nine");
        }

        [Test] public void TheBrightDayIsTheApprovedOne()
        {
            var l = DayCycle.At(DayCycle.DayHour);
            Same(l.Sun, 1f, 0.94f, 0.84f, "sun"); Assert.That(l.Intensity, Is.EqualTo(1.45f)); Assert.That(l.Shadow, Is.EqualTo(0.8f));
            Same(l.AmbientSky, 0.50f, 0.58f, 0.72f, "sky ambient"); Same(l.AmbientEquator, 0.54f, 0.55f, 0.56f, "equator ambient");
            Same(l.AmbientGround, 0.30f, 0.29f, 0.27f, "ground ambient"); Same(l.Haze, SkyLook.Haze, "haze");
            Same(l.Zenith, SkyLook.Zenith, "zenith"); Same(l.Horizon, SkyLook.Horizon, "horizon"); Same(l.Glow, SkyLook.Glow, "glow");
            Same(l.CloudLit, SkyLook.CloudLit, "lit cloud"); Same(l.CloudShade, SkyLook.CloudShade, "cloud shade");
            Travels(l, DayCycle.DayTravel, "the sun of PR #12");
            Assert.That(-l.SkyX, Is.EqualTo(l.LightX).Within(1e-6f)); Assert.That(-l.SkyY, Is.EqualTo(l.LightY).Within(1e-6f));
            Assert.That(l.Lights, Is.Zero); Assert.That(l.Moon, Is.False);
        }

        [Test] public void TheGoldenHourIsThatOfPr8()
        {
            var l = DayCycle.At(DayCycle.GoldenHour);
            Same(l.Sun, 1f, 0.72f, 0.45f, "sun"); Assert.That(l.Intensity, Is.EqualTo(1.4f)); Assert.That(l.Shadow, Is.EqualTo(0.86f));
            Same(l.AmbientSky, 0.42f, 0.45f, 0.56f, "sky ambient"); Same(l.AmbientEquator, 0.58f, 0.48f, 0.42f, "equator ambient");
            Same(l.AmbientGround, 0.3f, 0.25f, 0.22f, "ground ambient"); Same(l.Haze, 0.66f, 0.47f, 0.37f, "haze");
            Same(l.Zenith, 0.47f, 0.47f, 0.57f, "zenith"); Same(l.Horizon, 0.72f, 0.58f, 0.52f, "horizon"); Same(l.Glow, 1.0f, 0.64f, 0.36f, "glow");
            Same(l.CloudLit, 0.92f, 0.78f, 0.68f, "lit cloud"); Same(l.CloudShade, 0.58f, 0.52f, 0.56f, "cloud shade");
            Travels(l, DayCycle.GoldenTravel, "the sun of PR #8");
            Assert.That(l.Lights, Is.Zero);
        }

        [Test] public void TheWorkDayStaysBrightTillMidAfternoonWhileTheSunGoesRound()
        {
            for (float h = DayCycle.StartHour; h <= DayCycle.DayHour + 1e-4f; h += 0.25f)
            {
                var l = DayCycle.At(h); string at = DayCycle.Clock(h);
                Same(l.Sun, DayCycle.BrightDay.Sun, at + " sun"); Assert.That(l.Intensity, Is.EqualTo(DayCycle.BrightDay.Intensity), at);
                Same(l.AmbientSky, DayCycle.BrightDay.AmbientSky, at + " ambient"); Same(l.Haze, SkyLook.Haze, at + " haze");
                Same(l.Zenith, SkyLook.Zenith, at + " zenith"); Assert.That(l.Lights, Is.Zero, at);
                Assert.That(l.Elevation, Is.GreaterThan(25), at + ": a high sun");
            }
            Assert.That(DayCycle.At(9).Azimuth, Is.InRange(90f, 150f), "the morning sun in the east-south-east");
            Assert.That(DayCycle.At(12).Azimuth, Is.InRange(160f, 200f), "about south at noon");
            Assert.That(DayCycle.At(17).Azimuth, Is.GreaterThan(240), "in the west in the afternoon");
            for (float h = 9; h < 18.9f; h += 0.5f) Assert.That(DayCycle.At(h + 0.5f).Azimuth, Is.GreaterThanOrEqualTo(DayCycle.At(h).Azimuth - 0.01f), "east to west");
        }

        [Test] public void TheDayEndsAtNightWithTheCityLit()
        {
            var end = DayCycle.At(DayCycle.EndHour);
            Assert.That(end.Moon, Is.True); Assert.That(end.Lights, Is.EqualTo(1)); Assert.That(end.Intensity, Is.InRange(0.15f, 0.5f), "soft moonlight");
            Assert.That(end.Sun.B, Is.GreaterThan(end.Sun.R), "the moon is cold");
            Assert.That(DayCycle.At(DayCycle.DuskHour).Lights, Is.EqualTo(1), "every light on at dusk");
            Assert.That(DayCycle.At(DayCycle.SunsetHour).Lights, Is.InRange(0.1f, 0.6f), "the lamps come on at sunset");
            Assert.That(DayCycle.At(DayCycle.SunsetHour).Elevation, Is.InRange(-1f, 3f), "the sun on the horizon");
            foreach (float h in new[] { 20.5f, 22f, 23.5f, 1f, 3.5f })
            {
                var l = DayCycle.At(h);
                Assert.That(l.Lights, Is.EqualTo(1), h + ""); Assert.That(l.Moon, Is.True, h + "");
                Assert.That(Luma(l.Zenith), Is.LessThan(0.06f), h + ": a dark zenith"); Assert.That(Luma(l.Horizon), Is.GreaterThan(Luma(l.Zenith) * 2), h + ": the city glow on the horizon");
                Assert.That(Luma(l.Haze), Is.LessThan(Luma(SkyLook.Haze) / 4), h + ": a dark haze");
                Assert.That(Luma(l.AmbientSky), Is.InRange(0.08f, 0.25f), h + ": dark, still readable");
                Assert.That(l.Zenith.B, Is.GreaterThan(l.Zenith.R), h + ": a blue night");
            }
            for (float h = 9; h < DayCycle.GoldenHour; h += 0.25f) Assert.That(DayCycle.At(h).Lights, Is.Zero, "no night lights by day, " + DayCycle.Clock(h));
        }

        [Test] public void TheLightNeverShinesFromBelowAndItsDirectionsAreUnit()
        {
            float most = -(float)Math.Sin(DayCycle.MinLightElevation * Math.PI / 180);
            foreach (float h in Hours(Minute))
            {
                var l = DayCycle.At(h); string at = DayCycle.Clock(h);
                Assert.That(l.LightY, Is.LessThanOrEqualTo(most + 1e-5f), at);
                Assert.That(l.LightX * l.LightX + l.LightY * l.LightY + l.LightZ * l.LightZ, Is.EqualTo(1).Within(1e-4f), at);
                Assert.That(l.SkyX * l.SkyX + l.SkyY * l.SkyY + l.SkyZ * l.SkyZ, Is.EqualTo(1).Within(1e-4f), at);
                Assert.That(l.Intensity, Is.InRange(0f, 1.5f), at); Assert.That(l.Lights, Is.InRange(0f, 1f), at);
                foreach (var c in new[] { l.Sun, l.AmbientSky, l.AmbientEquator, l.AmbientGround, l.Haze, l.Zenith, l.Horizon, l.Glow, l.CloudLit, l.CloudShade })
                    Assert.That(Math.Min(c.R, Math.Min(c.G, c.B)) >= 0 && c.Brightest <= 1, at + ": colours in range");
                if (l.Elevation < 0) Assert.That(l.Intensity, Is.LessThan(0.5f), at + ": a sun under the horizon gives little light");
            }
        }

        [Test] public void NothingJumpsFromMinuteToMinute()
        {
            // A minute of the city clock is 0.67 s of the work day; frame to frame the change is far smaller still.
            foreach (float h in Hours(Minute))
            {
                DayLight a = DayCycle.At(h), b = DayCycle.At(h + Minute); string at = DayCycle.Clock(h);
                // The light as a vector (direction × intensity): turning over to the moon happens while it is out.
                float dx = b.LightX * b.Intensity - a.LightX * a.Intensity, dy = b.LightY * b.Intensity - a.LightY * a.Intensity, dz = b.LightZ * b.Intensity - a.LightZ * a.Intensity;
                Assert.That(Math.Sqrt(dx * dx + dy * dy + dz * dz), Is.LessThan(0.06), at + ": the light");
                Assert.That(Math.Abs(b.Lights - a.Lights), Is.LessThan(0.05f), at + ": the night lights");
                foreach (var (p, q) in new[] { (a.Sun, b.Sun), (a.AmbientSky, b.AmbientSky), (a.Haze, b.Haze), (a.Zenith, b.Zenith), (a.Horizon, b.Horizon), (a.Glow, b.Glow) })
                    Assert.That(Math.Max(Math.Abs(p.R - q.R), Math.Max(Math.Abs(p.G - q.G), Math.Abs(p.B - q.B))), Is.LessThan(0.04f), at + ": colours");
                if (a.Moon == b.Moon) Assert.That(Math.Abs(b.SkyY - a.SkyY), Is.LessThan(0.02f), at + ": the sun or the moon in the sky");
            }
        }

        [Test] public void KeysGoRoundTheClockAndChangeTheLightOverOnlyWhereItIsOut()
        {
            var keys = DayCycle.Keys;
            for (int i = 0; i < keys.Length; i++)
            {
                Assert.That(keys[i].Hour, Is.InRange(0f, 24f));
                if (i > 0) Assert.That(keys[i].Hour, Is.GreaterThan(keys[i - 1].Hour));
                var before = keys[(i + keys.Length - 1) % keys.Length];
                if (before.Moon != keys[i].Moon) Assert.That(before.Look.Intensity, Is.Zero, "the light is out before it changes over at " + DayCycle.Clock(keys[i].Hour));
            }
            Assert.That(keys.Count(k => k.Moon != keys[(Array.IndexOf(keys, k) + keys.Length - 1) % keys.Length].Moon), Is.EqualTo(2), "moonrise and sunrise");
        }

        [Test] public void NightLightsAreWarmAndTheLampsReachThePavement()
        {
            foreach (var warm in new[] { DayCycle.WindowGlow, DayCycle.LampLight }) { Assert.That(warm.R, Is.GreaterThan(warm.G)); Assert.That(warm.G, Is.GreaterThan(warm.B)); }
            float bulb = StreetModels.LampGlassY - DayCycle.LampDrop;
            Assert.That(bulb, Is.GreaterThan(4), "the light hangs high, under the glass");
            float d2 = bulb * bulb / (DayCycle.LampRange * DayCycle.LampRange);
            Assert.That(d2, Is.LessThan(0.25f), "the pavement under the lamp is well inside the light's range");
            // A pool a few metres wide: 3 m out along the pavement the light still reaches the ground.
            Assert.That(bulb * bulb + 9, Is.LessThan(DayCycle.LampRange * DayCycle.LampRange * 0.64f));
            Assert.That(DayCycle.WindowStrength * DayCycle.WindowGlow.Brightest, Is.GreaterThan(1), "lit windows reach the bloom");
            Assert.That(DayCycle.LampGlassDay, Is.InRange(0.1f, DayCycle.LampGlassEmission / 3), "unlit lamp glass is pale, far from lit");
            Assert.That(DayCycle.HeadlightBoost, Is.GreaterThan(0)); Assert.That(DayCycle.TailLightBoost, Is.GreaterThan(0));
            Assert.That(DayCycle.RoomBoost, Is.InRange(0.2f, 1.5f), "the rooms' lamps make up for the night ambient, not more");
            Assert.That(DayCycle.DoorIntensity, Is.LessThan(DayCycle.LampIntensity)); Assert.That(DayCycle.DoorRange, Is.LessThan(DayCycle.LampRange));
        }

        [Test] public void TheNightLightHangsUnderTheMiddleOfTheLampGlass()
        {
            var lamp = StreetModels.StreetLamp(); var glass = lamp.Triangles[(int)BuildingPart.Glow];
            Assert.That(glass.Length, Is.GreaterThan(0));
            float minZ = float.MaxValue, maxZ = float.MinValue, minX = float.MaxValue, maxX = float.MinValue;
            foreach (int v in glass)
            {
                Assert.That(lamp.Positions[v * 3 + 1], Is.EqualTo(StreetModels.LampGlassY).Within(1e-4f), "the glass at LampGlassY");
                minX = Math.Min(minX, lamp.Positions[v * 3]); maxX = Math.Max(maxX, lamp.Positions[v * 3]);
                minZ = Math.Min(minZ, lamp.Positions[v * 3 + 2]); maxZ = Math.Max(maxZ, lamp.Positions[v * 3 + 2]);
            }
            Assert.That((minZ + maxZ) / 2, Is.EqualTo(StreetModels.LampGlassZ).Within(0.01f), "LampGlassZ is the middle of the glass");
            Assert.That((minX + maxX) / 2, Is.EqualTo(0).Within(1e-4f));
        }
    }
}
