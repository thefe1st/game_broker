using System;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class ScreenLookTests
    {
        private const float Aspect = 16f / 9;
        private static void Graded(float scene, out float r, out float g, out float b) { r = g = b = scene; ScreenLook.Grade(ref r, ref g, ref b); }

        [Test] public void AcesKeepsBlackAndRisesMonotonicallyTowardWhite()
        {
            Assert.That(ScreenLook.Aces(0), Is.EqualTo(0).Within(1e-6)); Assert.That(ScreenLook.Aces(-2), Is.EqualTo(0).Within(1e-6));
            float previous = 0;
            for (float x = 0.001f; x < 32; x *= 1.1f)
            {
                float y = ScreenLook.Aces(x);
                Assert.That(y, Is.GreaterThan(previous).Or.EqualTo(1f), "x = " + x); Assert.That(y, Is.LessThanOrEqualTo(1)); previous = y;
            }
            Assert.That(ScreenLook.Aces(0.18f), Is.InRange(0.15f, 0.3f), "mid grey stays a mid tone");
            Assert.That(ScreenLook.Aces(16), Is.GreaterThan(0.97f));
        }
        [Test] public void GradeWarmsGreysKeepsTheirOrderAndStaysOnScreen()
        {
            Graded(0, out float r0, out float g0, out float b0);
            Assert.That(new[] { r0, g0, b0 }, Is.All.EqualTo(0).Within(1e-6));
            float previous = 0;
            for (float scene = 0.002f; scene < 64; scene *= 1.15f)
            {
                Graded(scene, out float r, out float g, out float b);
                Assert.That(new[] { r, g, b }, Is.All.InRange(0f, 1f), "scene " + scene);
                Assert.That(r, Is.GreaterThanOrEqualTo(g - 1e-6f), "warm white balance at " + scene); Assert.That(g, Is.GreaterThanOrEqualTo(b - 1e-6f), "warm white balance at " + scene);
                Assert.That(g, Is.GreaterThanOrEqualTo(previous - 1e-6f), "a brighter scene is a brighter screen"); previous = g;
            }
            Graded(ScreenLook.MidGrey, out _, out float mid, out _);
            Assert.That(mid, Is.InRange(0.15f, 0.23f), "scene mid grey stays near display mid grey");
            Graded(64, out _, out float white, out _);
            Assert.That(white, Is.GreaterThan(0.95f), "highlights reach white");
            // Saturation below one pulls a colour toward its grey; contrast never pushes a channel off the screen.
            float cr = 0.9f, cg = 0.3f, cb = 0.1f; ScreenLook.Grade(ref cr, ref cg, ref cb);
            Assert.That(cr, Is.GreaterThan(cg)); Assert.That(cg, Is.GreaterThan(cb)); Assert.That(new[] { cr, cg, cb }, Is.All.InRange(0f, 1f));
        }
        [Test] public void VignetteDarkensOnlyTowardTheCorners()
        {
            Assert.That(ScreenLook.VignetteAt(0.5f, 0.5f, Aspect), Is.EqualTo(1).Within(1e-6));
            foreach (var (u, v) in new[] { (0f, 0f), (1f, 0f), (0f, 1f), (1f, 1f) })
                Assert.That(ScreenLook.VignetteAt(u, v, Aspect), Is.EqualTo(1 - ScreenLook.Vignette).Within(1e-5), "corner " + u + ", " + v);
            float previous = 1;
            for (float t = 0; t <= 0.5f; t += 1f / 32)
            {
                float m = ScreenLook.VignetteAt(0.5f + t, 0.5f - t, Aspect);
                Assert.That(m, Is.LessThanOrEqualTo(previous + 1e-6f)); Assert.That(m, Is.EqualTo(ScreenLook.VignetteAt(0.5f - t, 0.5f + t, Aspect)).Within(1e-6), "symmetric");
                previous = m;
            }
            float side = ScreenLook.VignetteAt(1, 0.5f, Aspect), top = ScreenLook.VignetteAt(0.5f, 1, Aspect);
            Assert.That(side, Is.InRange(1 - ScreenLook.Vignette, 1f)); Assert.That(top, Is.InRange(side, 1f), "the wide sides darken more than top and bottom");
            Assert.That(ScreenLook.VignetteAt(0.5f, 0.6f, Aspect), Is.GreaterThan(0.999f), "the middle of the screen stays untouched");
        }
        [Test] public void BloomTakesOnlyTheBrightPartWithASoftKnee()
        {
            Assert.That(ScreenLook.BloomShare(0), Is.EqualTo(0).Within(1e-6));
            Assert.That(ScreenLook.BloomShare(ScreenLook.BloomThreshold - ScreenLook.BloomKnee), Is.EqualTo(0).Within(1e-6), "nothing below the knee");
            Assert.That(ScreenLook.BloomShare(ScreenLook.BloomThreshold), Is.GreaterThan(0).And.LessThan(0.25f), "the knee starts softly");
            float previous = 0;
            for (float x = 0.05f; x < 100; x *= 1.1f)
            {
                float share = ScreenLook.BloomShare(x);
                Assert.That(share, Is.InRange(0f, 1f)); Assert.That(share, Is.GreaterThanOrEqualTo(previous - 1e-6f), "x = " + x); previous = share;
            }
            Assert.That(ScreenLook.BloomShare(64), Is.GreaterThan(0.95f), "the sun disk blooms almost entirely");
        }
        [Test] public void TransferFunctionsRoundTripAndFollowTheSrgbCurve()
        {
            for (float c = 0; c <= 1; c += 1f / 64)
            {
                Assert.That(ScreenLook.LinearToSrgb(ScreenLook.SrgbToLinear(c)), Is.EqualTo(c).Within(1e-4), "sRGB " + c);
                Assert.That(ScreenLook.SrgbToLinear(ScreenLook.LinearToSrgb(c)), Is.EqualTo(c).Within(1e-4), "linear " + c);
                Assert.That(ScreenLook.SrgbToLinear(c), Is.LessThanOrEqualTo(c + 1e-6f), "linear values are darker");
            }
            Assert.That(ScreenLook.SrgbToLinear(0.5f), Is.EqualTo(0.2140f).Within(1e-4)); Assert.That(ScreenLook.SrgbToLinear(1), Is.EqualTo(1).Within(1e-6));
            Assert.That(ScreenLook.SrgbToLinear(0.04f), Is.EqualTo(0.04f / 12.92f).Within(1e-7), "linear toe");
            Assert.That(ScreenLook.GammaToLinear(1), Is.EqualTo(1).Within(1e-6), "continuous at one");
            Assert.That(ScreenLook.GammaToLinear(1.4f), Is.EqualTo((float)Math.Pow(1.4, 2.2)).Within(1e-5), "HDR light colours");
            Assert.That(ScreenLook.GammaToLinear(0.5f), Is.EqualTo(ScreenLook.SrgbToLinear(0.5f))); Assert.That(ScreenLook.GammaToLinear(-1), Is.Zero);
        }
        [Test] public void HazeGlowFollowsTheFogTowardTheSunLikeTheSkyHalo()
        {
            const float start = CityScenery.FogStart, end = CityScenery.FogEnd;
            float full = ScreenLook.HazeGlowAt(end, start, end, 1, 0);
            Assert.That(full, Is.EqualTo(SkyLook.HaloStrength).Within(1e-6), "a fully hazed block toward the sun takes the whole halo");
            Assert.That(ScreenLook.HazeGlowAt(end * 3, start, end, 1, 0), Is.EqualTo(full).Within(1e-6), "the fog saturates");
            Assert.That(ScreenLook.HazeGlowAt(start * 0.5f, start, end, 1, 0), Is.Zero, "no glow before the fog starts");
            Assert.That(ScreenLook.HazeGlowAt((start + end) / 2, start, end, 1, 0), Is.EqualTo(full / 2).Within(1e-5), "linear in the fog");
            Assert.That(ScreenLook.HazeGlowAt(end, start, end, 0, 0), Is.Zero, "nothing at right angles to the sun");
            Assert.That(ScreenLook.HazeGlowAt(end, start, end, -0.6f, 0), Is.Zero, "nothing away from the sun");
            float previous = 0;
            for (float cos = 0.05f; cos <= 1; cos += 0.05f)
            {
                float glow = ScreenLook.HazeGlowAt(end, start, end, cos, 0);
                Assert.That(glow, Is.GreaterThan(previous), "brighter toward the sun"); previous = glow;
                // The same term as the sky's halo: blocks melt into the sky behind them.
                Assert.That(glow, Is.EqualTo(SkyLook.HaloStrength * Math.Pow(cos, SkyLook.HaloPower)).Within(1e-5));
            }
            Assert.That(ScreenLook.HazeGlowAt(end, start, end, 1, -0.3f), Is.EqualTo(full).Within(1e-6), "below the horizon the band is full");
            Assert.That(ScreenLook.HazeGlowAt(end, start, end, 1, 0.2f), Is.EqualTo(full * Math.Exp(-0.2 * SkyLook.HorizonFalloff)).Within(1e-5), "fades up the sky like the horizon band");
            Assert.That(ScreenLook.HazeGlowAt(60, 50, 50, 1, 0), Is.EqualTo(full).Within(1e-6), "an empty fog range does not divide by zero");
        }
    }
}
