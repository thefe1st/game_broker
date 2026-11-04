using System;

namespace Broker.Core
{
    // The golden-hour sky over the city, shared by the Unity sky shader (Shaders/BrokerSky.shader) and the offline preview:
    // a warm horizon equal to the haze, a cooler zenith, a halo around the low sun and a layer of soft clouds lit from
    // the sun side. The palette is sRGB (as written to the material); Evaluate works on linear colours and returns linear HDR.
    public static class SkyLook
    {
        public readonly struct Rgb
        {
            public readonly float R, G, B;
            public Rgb(float r, float g, float b) { R = r; G = g; B = b; }
            public Rgb Linear => new Rgb(ScreenLook.SrgbToLinear(R), ScreenLook.SrgbToLinear(G), ScreenLook.SrgbToLinear(B));
            public float Brightest => Math.Max(R, Math.Max(G, B));
            public static Rgb operator +(Rgb a, Rgb b) => new Rgb(a.R + b.R, a.G + b.G, a.B + b.B);
            public static Rgb operator *(Rgb a, float k) => new Rgb(a.R * k, a.G * k, a.B * k);
            public static Rgb Lerp(Rgb a, Rgb b, float t) => new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
        }
        public sealed class Palette
        {
            public Rgb Zenith, Horizon, Ground, Glow, CloudLit, CloudShade;
            // The default palette, linearised for a linear-space project (as Unity does with material colours).
            public static Palette Default(bool linear)
            {
                Rgb L(Rgb c) => linear ? c.Linear : c;
                return new Palette { Zenith = L(SkyLook.Zenith), Horizon = L(SkyLook.Horizon), Ground = L(SkyLook.Ground), Glow = L(SkyLook.Glow),
                    CloudLit = L(SkyLook.CloudLit), CloudShade = L(SkyLook.CloudShade) };
            }
        }

        // Palette (sRGB). The horizon is also the fog colour, so the far blocks melt into the sky.
        public static readonly Rgb Zenith = new Rgb(0.42f, 0.52f, 0.70f), Horizon = new Rgb(0.90f, 0.68f, 0.52f), Ground = new Rgb(0.62f, 0.50f, 0.42f);
        public static readonly Rgb Glow = new Rgb(1.0f, 0.64f, 0.36f), CloudLit = new Rgb(1.0f, 0.87f, 0.76f), CloudShade = new Rgb(0.66f, 0.58f, 0.64f);
        // Gradient: exp(−falloff × height) blends the zenith into the horizon.
        public const float HorizonFalloff = 4.5f;
        // Sun: a wide halo low on the sky, a tight glow and the disk (cosines of its radius and of the end of its soft edge).
        public const float HaloStrength = 0.55f, HaloPower = 5f, GlowStrength = 1.4f, GlowPower = 160f, DiskStrength = 16f, DiskCos = 0.99955f, DiskSoftCos = 0.99975f;
        // Clouds: a plane projected over the city, stretched east–west, faded into the haze at the horizon.
        public const float CloudCover = 0.42f, CloudSoftness = 0.2f, CloudScale = 1.5f, CloudLift = 0.18f, CloudStretch = 0.5f, CloudDrift = 0.004f;
        public const float CloudFadeLow = 0.015f, CloudFadeHigh = 0.22f, CloudOpacity = 0.85f;
        // Cloud light: density a step toward the sun; thinner there means a lit edge. Silver lining near the sun, haze near the horizon.
        public const float CloudLightStep = 0.16f, CloudLightGain = 3.5f, CloudSilver = 0.8f, CloudHaze = 0.6f;

        // d: unit view direction, s: unit direction toward the sun, time: seconds (clouds drift slowly).
        public static Rgb Evaluate(float dx, float dy, float dz, float sx, float sy, float sz, Palette p, float time = 0)
        {
            float band = (float)Math.Exp(-Math.Max(dy, 0) * HorizonFalloff);
            var color = Rgb.Lerp(p.Zenith, p.Horizon, band);
            if (dy < 0) color = Rgb.Lerp(p.Horizon, p.Ground, Saturate(-dy * 6));
            float cosSun = dx * sx + dy * sy + dz * sz, c = Math.Max(cosSun, 0);
            color += p.Glow * (HaloStrength * Pow(c, HaloPower) * band + GlowStrength * Pow(c, GlowPower));
            float cloud = 0;
            if (dy > 0)
            {
                float inv = CloudScale / (dy + CloudLift);
                float u = dx * inv * CloudStretch + time * CloudDrift, v = dz * inv;
                float n = Fbm(u, v), toward = Fbm(u + sx * CloudLightStep, v + sz * CloudLightStep);
                cloud = SmoothStep(1 - CloudCover - CloudSoftness, 1 - CloudCover + CloudSoftness, n) * SmoothStep(CloudFadeLow, CloudFadeHigh, dy) * CloudOpacity;
                float lit = Saturate(0.5f + (n - toward) * CloudLightGain);
                var cloudColor = Rgb.Lerp(p.CloudShade, p.CloudLit, lit) + p.Glow * (CloudSilver * Pow(c, 8));
                cloudColor = Rgb.Lerp(cloudColor, p.Horizon, band * CloudHaze);
                color = Rgb.Lerp(color, cloudColor, cloud);
            }
            return color + p.Glow * (DiskStrength * SmoothStep(DiskCos, DiskSoftCos, cosSun) * (1 - cloud));
        }

        // Value noise over a sine-free hash (D. Hoskins, hash12), four octaves; the shader repeats it line for line.
        public static float Hash(float x, float y)
        {
            float a = Frac(x * 0.1031f), b = Frac(y * 0.1031f), c = Frac(x * 0.1031f);
            float k = a * (b + 33.33f) + b * (c + 33.33f) + c * (a + 33.33f);
            a += k; b += k; c += k;
            return Frac((a + b) * c);
        }
        public static float Noise(float x, float y)
        {
            float ix = (float)Math.Floor(x), iy = (float)Math.Floor(y), fx = x - ix, fy = y - iy;
            float ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy);
            float a = Hash(ix, iy), b = Hash(ix + 1, iy), c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
            return Mix(Mix(a, b, ux), Mix(c, d, ux), uy);
        }
        public static float Fbm(float x, float y)
        {
            float sum = 0, amplitude = 0.5f;
            for (int octave = 0; octave < 4; octave++)
            {
                sum += amplitude * Noise(x, y);
                x = x * 2.02f + 5.3f; y = y * 2.02f + 1.7f; amplitude *= 0.5f;
            }
            return sum / 0.9375f;
        }
        private static float Frac(float x) => x - (float)Math.Floor(x);
        private static float Mix(float a, float b, float t) => a + (b - a) * t;
        private static float Saturate(float x) => Math.Min(1, Math.Max(0, x));
        private static float Pow(float x, float power) => x <= 0 ? 0 : (float)Math.Pow(x, power);
        public static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Saturate((x - edge0) / (edge1 - edge0));
            return t * t * (3 - 2 * t);
        }
    }
}
