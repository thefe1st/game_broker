using System;

namespace Broker.Core
{
    // The screen look of the eye cameras, shared by the Unity post shader (Shaders/BrokerPost.shader) and the offline preview.
    // Scene colours are linear and HDR; Grade turns them into display-linear values that the sRGB target encodes.
    public static class ScreenLook
    {
        // Bloom: soft-knee threshold on the brightest channel, a blurred half-resolution pyramid, added with this intensity.
        public const float BloomThreshold = 1.0f, BloomKnee = 0.5f, BloomIntensity = 0.16f;
        public const int BloomLevels = 6;
        // Screen-space occlusion: hemisphere radius in metres, strength, fade-out between the two view distances.
        public const float OcclusionRadius = 0.55f, OcclusionStrength = 0.9f, OcclusionFadeStart = 40f, OcclusionFadeEnd = 100f;
        public const int OcclusionSamples = 12;
        // Grade: exposure and a warm white balance before the ACES curve, then saturation and contrast around mid grey.
        public const float Exposure = 1.0f, WarmR = 1.04f, WarmG = 1.0f, WarmB = 0.92f;
        public const float Saturation = 0.9f, Contrast = 1.05f, MidGrey = 0.18f;
        // Vignette: darkening at the screen corners (0 = none), over the aspect-corrected distance from the centre.
        public const float Vignette = 0.24f;

        // ACES filmic fit (K. Narkowicz, 2015): 0 → 0, monotonic, approaches 1 for bright highlights.
        public static float Aces(float x)
        {
            x = Math.Max(0, x);
            return Math.Min(1, x * (2.51f * x + 0.03f) / (x * (2.43f * x + 0.59f) + 0.14f));
        }
        public static void Grade(ref float r, ref float g, ref float b)
        {
            r = Aces(r * Exposure * WarmR); g = Aces(g * Exposure * WarmG); b = Aces(b * Exposure * WarmB);
            float luma = 0.2126f * r + 0.7152f * g + 0.0722f * b;
            r = Contrasted(luma + (r - luma) * Saturation); g = Contrasted(luma + (g - luma) * Saturation); b = Contrasted(luma + (b - luma) * Saturation);
        }
        private static float Contrasted(float c) => c <= 0 ? 0 : Math.Min(1, MidGrey * (float)Math.Pow(c / MidGrey, Contrast));
        // Multiplier at screen position (u, v ∈ 0..1) for a width / height aspect.
        // d is 0 in the centre and 1 in the corners; d² keeps the darkening in the corners.
        public static float VignetteAt(float u, float v, float aspect)
        {
            float x = (u - 0.5f) * 2, y = (v - 0.5f) * 2;
            float d = (x * x * aspect * aspect + y * y) / (aspect * aspect + 1);
            return 1 - Vignette * d * d;
        }
        // Soft-knee bloom response for the brightest channel (the share of a colour that blooms).
        public static float BloomShare(float brightest)
        {
            float soft = Math.Min(Math.Max(brightest - BloomThreshold + BloomKnee, 0), 2 * BloomKnee);
            soft = soft * soft / (4 * BloomKnee + 1e-5f);
            return Math.Max(soft, brightest - BloomThreshold) / Math.Max(brightest, 1e-5f);
        }

        // Exact sRGB transfer functions (what sRGB textures and render targets do in hardware).
        public static float SrgbToLinear(float c) => c <= 0.04045f ? c / 12.92f : (float)Math.Pow((c + 0.055f) / 1.055f, 2.4f);
        public static float LinearToSrgb(float c) => c <= 0.0031308f ? c * 12.92f : 1.055f * (float)Math.Pow(c, 1 / 2.4f) - 0.055f;
        // Unity's gamma-to-linear for colours that may exceed 1 (light colour × intensity): power 2.2 above one.
        public static float GammaToLinear(float c) => c <= 1 ? SrgbToLinear(Math.Max(0, c)) : (float)Math.Pow(c, 2.2f);
    }
}
