using System;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class SkyLookTests
    {
        private static readonly SkyLook.Palette Sky = SkyLook.Palette.Default(true);
        // Toward the low evening sun of the city scene (the light shines along OfficeScene.SunDirection, its opposite).
        private static readonly float Sx, Sy, Sz;
        static SkyLookTests() { float l = (float)Math.Sqrt(0.88 * 0.88 + 0.29 * 0.29 + 0.31 * 0.31); Sx = -0.88f / l; Sy = 0.29f / l; Sz = -0.31f / l; }

        private static SkyLook.Rgb At(float dx, float dy, float dz, float time = 0)
        {
            float l = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            return SkyLook.Evaluate(dx / l, dy / l, dz / l, Sx, Sy, Sz, Sky, time);
        }
        // A direction at height dy (the sine of the elevation) and azimuth angle (radians) around the up axis.
        private static SkyLook.Rgb Around(float dy, double angle, float time = 0)
        {
            float h = (float)Math.Sqrt(Math.Max(0, 1 - dy * dy));
            return SkyLook.Evaluate((float)Math.Cos(angle) * h, dy, (float)Math.Sin(angle) * h, Sx, Sy, Sz, Sky, time);
        }
        private static float Luma(SkyLook.Rgb c) => 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;
        private static float Saturation(SkyLook.Rgb c) => (c.Brightest - Math.Min(c.R, Math.Min(c.G, c.B))) / c.Brightest;
        private static void AssertColour(SkyLook.Rgb actual, SkyLook.Rgb expected, string message)
        {
            Assert.That(actual.R, Is.EqualTo(expected.R).Within(1e-6), message); Assert.That(actual.G, Is.EqualTo(expected.G).Within(1e-6), message);
            Assert.That(actual.B, Is.EqualTo(expected.B).Within(1e-6), message);
        }
        // The cloudless gradient at height dy, away from the sun.
        private static SkyLook.Rgb Clear(float dy) => SkyLook.Rgb.Lerp(Sky.Zenith, Sky.Horizon, (float)Math.Exp(-Math.Max(dy, 0) * SkyLook.HorizonFalloff));
        private static bool AwayFromTheSun(float dy, double angle)
        {
            float h = (float)Math.Sqrt(Math.Max(0, 1 - dy * dy));
            return (float)Math.Cos(angle) * h * Sx + dy * Sy + (float)Math.Sin(angle) * h * Sz <= 0;
        }

        [Test] public void ThePaletteIsAWarmHazeUnderAPeachHorizonAndALilacZenith()
        {
            Assert.That(SkyLook.Ground.R, Is.EqualTo(SkyLook.Haze.R)); Assert.That(SkyLook.Ground.G, Is.EqualTo(SkyLook.Haze.G)); Assert.That(SkyLook.Ground.B, Is.EqualTo(SkyLook.Haze.B));
            Assert.That(Luma(SkyLook.Haze), Is.LessThan(Luma(SkyLook.Horizon)), "far blocks read as silhouettes against the horizon");
            Assert.That(Saturation(SkyLook.Haze), Is.GreaterThan(Saturation(SkyLook.Horizon)), "the haze is a deeper orange");
            foreach (var warm in new[] { SkyLook.Horizon, SkyLook.Haze, SkyLook.Glow, SkyLook.CloudLit }) { Assert.That(warm.R, Is.GreaterThan(warm.G)); Assert.That(warm.G, Is.GreaterThan(warm.B)); }
            Assert.That(SkyLook.Zenith.B, Is.GreaterThan(SkyLook.Zenith.R).And.GreaterThan(SkyLook.Zenith.G), "lilac-grey zenith");
            Assert.That(Luma(SkyLook.Zenith), Is.LessThan(Luma(SkyLook.Horizon)));
            Assert.That(Luma(SkyLook.CloudShade), Is.LessThan(Luma(SkyLook.CloudLit)));
            var srgb = SkyLook.Palette.Default(false);
            Assert.That(srgb.Horizon.R, Is.EqualTo(SkyLook.Horizon.R)); Assert.That(Sky.Horizon.R, Is.EqualTo(ScreenLook.SrgbToLinear(SkyLook.Horizon.R)));
        }
        [Test] public void TheHorizonAwayFromTheSunIsThePaletteHorizonAndBelowItTheHaze()
        {
            AssertColour(At(-Sx, 0, -Sz), Sky.Horizon, "horizon opposite the sun");
            AssertColour(At(Sz, 0, -Sx), Sky.Horizon, "horizon at right angles to the sun");
            AssertColour(At(0, -1, 0), Sky.Ground, "straight down");
            AssertColour(At(-Sx, -0.5f, -Sz), Sky.Ground, "the lower dome is the haze");
            // Just under the horizon the dome blends from the horizon into the haze.
            var under = At(-Sx, -0.05f, -Sz);
            Assert.That(under.R, Is.InRange(Math.Min(Sky.Horizon.R, Sky.Ground.R), Math.Max(Sky.Horizon.R, Sky.Ground.R)));
            Assert.That(under.B, Is.InRange(Math.Min(Sky.Horizon.B, Sky.Ground.B), Math.Max(Sky.Horizon.B, Sky.Ground.B)));
        }
        [Test] public void TheSkyDarkensUpwardAndBrightensTowardTheSun()
        {
            double Mean(float dy) { double sum = 0; for (int a = 0; a < 72; a++) sum += Luma(Around(dy, a * Math.PI / 36)); return sum / 72; }
            Assert.That(Mean(0.95f), Is.LessThan(Mean(0) * 0.75), "the zenith is darker than the horizon, clouds included");
            Assert.That(Luma(At(0, 1, 0)), Is.LessThan(Luma(Sky.Horizon)));
            // Along the horizon (no clouds there) brightness and warmth grow toward the sun's azimuth.
            double sun = Math.Atan2(Sz, Sx); float previous = float.MaxValue;
            for (int step = 0; step <= 18; step++)
            {
                var c = Around(0, sun + step * Math.PI / 18);
                Assert.That(Luma(c), Is.LessThanOrEqualTo(previous + 1e-6f), "turning away from the sun, step " + step); previous = Luma(c);
            }
            var toward = Around(0, sun);
            Assert.That(Luma(toward), Is.GreaterThan(Luma(Sky.Horizon) * 1.5f)); Assert.That(toward.R / toward.B, Is.GreaterThan(Sky.Horizon.R / Sky.Horizon.B), "warmer toward the sun");
            var disk = At(Sx, Sy, Sz);
            Assert.That(disk.Brightest, Is.GreaterThan(SkyLook.DiskStrength * (1 - SkyLook.CloudOpacity)), "clouds never hide the disk completely");
            Assert.That(disk.Brightest, Is.GreaterThan(Around(0, sun).Brightest), "the disk is the brightest point");
        }
        [Test] public void CloudsFadeOutAtTheHorizonAndLeaveGapsAboveIt()
        {
            int cloudy = 0, total = 0;
            for (int a = 0; a < 72; a++)
            {
                double angle = a * Math.PI / 36;
                // Down at the horizon there are no clouds: the colour is the clear gradient (the halo only lights the sun side).
                foreach (float dy in new[] { 0f, 0.005f, SkyLook.CloudFadeLow })
                    if (AwayFromTheSun(dy, angle)) AssertColour(Around(dy, angle), Clear(dy), "clear horizon at " + a * 5 + "°, dy " + dy);
                // Higher up the cloud layer covers part of the sky.
                foreach (float dy in new[] { 0.3f, 0.5f, 0.7f })
                {
                    if (!AwayFromTheSun(dy, angle)) continue;
                    var c = Around(dy, angle); var clear = Clear(dy); total++;
                    if (Math.Abs(c.R - clear.R) + Math.Abs(c.G - clear.G) + Math.Abs(c.B - clear.B) > 1e-3f) cloudy++;
                }
            }
            Assert.That(total, Is.GreaterThan(50));
            Assert.That(cloudy, Is.GreaterThan(total / 5), "clouds above the horizon band");
            Assert.That(cloudy, Is.LessThan(total), "with gaps of clear sky");
            // Clouds drift with time; the horizon stays the same.
            int moved = 0;
            for (int a = 0; a < 72; a++) if (Math.Abs(Around(0.5f, a * Math.PI / 36).R - Around(0.5f, a * Math.PI / 36, 600).R) > 1e-4f) moved++;
            Assert.That(moved, Is.GreaterThan(0)); AssertColour(At(-Sx, 0, -Sz, 600), Sky.Horizon, "the horizon does not drift");
        }
        [Test] public void EveryDirectionAndTimeGivesAFiniteNonNegativeColour()
        {
            foreach (float time in new[] { 0f, 37.5f, 600f, 86400f })
            {
                for (int e = -10; e <= 10; e++)
                    for (int a = 0; a < 36; a++)
                    {
                        float dy = e / 10f; var c = Around(dy, a * Math.PI / 18, time);
                        foreach (float v in new[] { c.R, c.G, c.B }) Assert.That(float.IsNaN(v) || float.IsInfinity(v) || v < 0, Is.False, "dy " + dy + ", azimuth " + a * 10 + ", time " + time);
                    }
                var sun = At(Sx, Sy, Sz, time);
                foreach (float v in new[] { sun.R, sun.G, sun.B }) Assert.That(float.IsNaN(v) || float.IsInfinity(v) || v < 0, Is.False, "the sun at " + time);
            }
        }
        [Test] public void NoiseStaysInTheUnitRangeAndSpreadsOverIt()
        {
            float min = 1, max = 0;
            for (int y = -40; y < 40; y++)
                for (int x = -40; x < 40; x++)
                {
                    float fx = x * 0.37f + 0.13f, fy = y * 0.29f - 0.41f;
                    float hash = SkyLook.Hash(fx * 7, fy * 7), noise = SkyLook.Noise(fx, fy), fbm = SkyLook.Fbm(fx, fy);
                    Assert.That(hash, Is.GreaterThanOrEqualTo(0).And.LessThan(1)); Assert.That(noise, Is.GreaterThanOrEqualTo(0).And.LessThan(1));
                    Assert.That(fbm, Is.GreaterThanOrEqualTo(0).And.LessThan(1));
                    min = Math.Min(min, fbm); max = Math.Max(max, fbm);
                }
            Assert.That(min, Is.LessThan(0.3f)); Assert.That(max, Is.GreaterThan(0.7f));
            Assert.That(SkyLook.Fbm(1.25f, -3.5f), Is.EqualTo(SkyLook.Fbm(1.25f, -3.5f)), "deterministic");
            Assert.That(SkyLook.Noise(3, 4), Is.EqualTo(SkyLook.Hash(3, 4)).Within(1e-6), "value noise passes through the lattice hashes");
            Assert.That(SkyLook.SmoothStep(0.2f, 0.4f, 0.1f), Is.Zero); Assert.That(SkyLook.SmoothStep(0.2f, 0.4f, 0.5f), Is.EqualTo(1));
            Assert.That(SkyLook.SmoothStep(0.2f, 0.4f, 0.3f), Is.EqualTo(0.5f).Within(1e-6));
        }
    }
}
