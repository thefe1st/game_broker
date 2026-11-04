using System;

namespace Broker.Core
{
    // Time of day over the work day (stage 4, PR #17; the author chose a night till midnight, the sun going round and the
    // city clock in the HUD). The ten real minutes of BrokerDay run from 9:00 to midnight on the city clock, 40 s an hour:
    // the approved bright day (PR #12) until mid-afternoon with the sun going round from the east, the golden hour of PR #8
    // at 18:00, sunset at 19:00, the blue hour at 19:45 and night from 20:15 — the last two and a half minutes —, when lit
    // windows, shop fronts, street lamps and headlights make the picture. At(hour) gives the light of any hour of the 24
    // from moods at key hours, eased (smoothstep) between neighbours. The light is the sun by day and the moon at night; it
    // changes over only at a key where the light before it is out, so nothing jumps.
    public static class DayCycle
    {
        // The work day on the city clock: 9:00 to midnight (24, 00:00 on the clock).
        public const float StartHour = 9f, EndHour = 24f;
        // Key hours: the approved bright day (the preview's default), the golden hour of PR #8, sunset, twilight (the sun out),
        // dusk with every light on, and night.
        public const float DayHour = 15f, GoldenHour = 18f, SunsetHour = 19f, TwilightHour = 19f + 20f / 60, DuskHour = 19.75f, NightHour = 20.25f;
        // The light never shines from below: under this elevation (degrees) the sun is out anyway, but the sky still glows there.
        public const float MinLightElevation = 2f;
        // Direction the light travels at the bright day (PR #12) and at the golden hour (PR #8), as they were.
        public static readonly float[] DayTravel = { 0.722f, -0.643f, 0.254f }, GoldenTravel = { 0.88f, -0.29f, 0.31f };
        // After dusk (OfficeScene.DayNight): the night glass of the buildings glows with a warm window light (sRGB) at full
        // strength, the street lamps' glass glows and a point light hangs LampDrop under it (colour, intensity, range in metres);
        // a smaller light of the same colour hangs over the office door on the plaza. By day the lamps are out, their glass a
        // pale frosted one (a little emission, LampGlassDay) rather than a dark hole under the head.
        public static readonly SkyLook.Rgb WindowGlow = new SkyLook.Rgb(1.0f, 0.74f, 0.44f), LampLight = new SkyLook.Rgb(1.0f, 0.78f, 0.52f);
        public const float WindowStrength = 1.05f, LampGlassEmission = 1.6f, LampGlassDay = 0.45f, LampIntensity = 2.0f, LampRange = 11f, LampDrop = 0.25f;
        public const float DoorIntensity = 1.4f, DoorRange = 6f;
        // Headlights and tail lights burn brighter after dark: their emission × (1 + boost × Lights). The lamps of the office,
        // the café and the exchange hall do the same, so the rooms stay lit while the ambient falls with the night outside.
        public const float HeadlightBoost = 1.0f, TailLightBoost = 2.0f, RoomBoost = 0.6f;

        // A mood: the light's colour, intensity (Unity's) and shadow strength; trilight ambient; the haze (fog colour and the
        // sky under the horizon); the sky's palette; and how far the night lights are on (0–1). Colours are sRGB.
        public sealed class Look
        {
            public SkyLook.Rgb Sun; public float Intensity, Shadow;
            public SkyLook.Rgb AmbientSky, AmbientEquator, AmbientGround, Haze;
            public SkyLook.Rgb Zenith, Horizon, Glow, CloudLit, CloudShade;
            public float Lights;
        }
        private static SkyLook.Rgb C(float r, float g, float b) => new SkyLook.Rgb(r, g, b);

        // The approved bright day: the sun, ambient and haze of OfficeScene.Atmosphere and the sky of SkyLook (PR #12).
        public static readonly Look BrightDay = new Look
        {
            Sun = C(1f, 0.94f, 0.84f), Intensity = 1.45f, Shadow = 0.8f,
            AmbientSky = C(0.50f, 0.58f, 0.72f), AmbientEquator = C(0.54f, 0.55f, 0.56f), AmbientGround = C(0.30f, 0.29f, 0.27f), Haze = SkyLook.Haze,
            Zenith = SkyLook.Zenith, Horizon = SkyLook.Horizon, Glow = SkyLook.Glow, CloudLit = SkyLook.CloudLit, CloudShade = SkyLook.CloudShade,
        };
        // The golden hour of PR #8: a low warm sun, cool shade, deep orange haze.
        public static readonly Look Golden = new Look
        {
            Sun = C(1f, 0.72f, 0.45f), Intensity = 1.4f, Shadow = 0.86f,
            AmbientSky = C(0.42f, 0.45f, 0.56f), AmbientEquator = C(0.58f, 0.48f, 0.42f), AmbientGround = C(0.3f, 0.25f, 0.22f), Haze = C(0.66f, 0.47f, 0.37f),
            Zenith = C(0.47f, 0.47f, 0.57f), Horizon = C(0.72f, 0.58f, 0.52f), Glow = C(1.0f, 0.64f, 0.36f), CloudLit = C(0.92f, 0.78f, 0.68f), CloudShade = C(0.58f, 0.52f, 0.56f),
        };
        // Sunset: the sun on the horizon, red-orange and weaker, rose haze; the street lamps come on.
        public static readonly Look Sunset = new Look
        {
            Sun = C(1f, 0.55f, 0.32f), Intensity = 0.7f, Shadow = 0.75f,
            AmbientSky = C(0.36f, 0.37f, 0.52f), AmbientEquator = C(0.50f, 0.39f, 0.38f), AmbientGround = C(0.22f, 0.18f, 0.17f), Haze = C(0.56f, 0.40f, 0.38f),
            Zenith = C(0.30f, 0.33f, 0.52f), Horizon = C(0.82f, 0.52f, 0.42f), Glow = C(1.0f, 0.52f, 0.30f), CloudLit = C(0.92f, 0.60f, 0.52f), CloudShade = C(0.44f, 0.38f, 0.48f),
            Lights = 0.3f,
        };
        // Twilight: the sun just under the horizon and out, a violet sky with an afterglow low in the west, windows lighting up.
        public static readonly Look Twilight = new Look
        {
            Sun = C(0.62f, 0.52f, 0.62f), Intensity = 0f, Shadow = 0.6f,
            AmbientSky = C(0.22f, 0.25f, 0.40f), AmbientEquator = C(0.26f, 0.24f, 0.32f), AmbientGround = C(0.11f, 0.11f, 0.13f), Haze = C(0.25f, 0.25f, 0.36f),
            Zenith = C(0.11f, 0.15f, 0.34f), Horizon = C(0.44f, 0.36f, 0.46f), Glow = C(0.62f, 0.34f, 0.24f), CloudLit = C(0.42f, 0.36f, 0.44f), CloudShade = C(0.21f, 0.21f, 0.32f),
            Lights = 0.75f,
        };
        // Dusk, the blue hour: a deep blue sky, the moon up, every light on.
        public static readonly Look Dusk = new Look
        {
            Sun = C(0.62f, 0.72f, 1f), Intensity = 0.25f, Shadow = 0.5f,
            AmbientSky = C(0.21f, 0.26f, 0.42f), AmbientEquator = C(0.19f, 0.20f, 0.29f), AmbientGround = C(0.10f, 0.10f, 0.12f), Haze = C(0.14f, 0.17f, 0.28f),
            Zenith = C(0.04f, 0.07f, 0.19f), Horizon = C(0.21f, 0.23f, 0.38f), Glow = C(0.32f, 0.36f, 0.46f), CloudLit = C(0.23f, 0.25f, 0.36f), CloudShade = C(0.11f, 0.13f, 0.21f),
            Lights = 1f,
        };
        // Night: cold moonlight with soft shadows, a dark navy sky over a lighter city haze, the city lit by its own lamps.
        public static readonly Look Night = new Look
        {
            Sun = C(0.60f, 0.70f, 1f), Intensity = 0.4f, Shadow = 0.55f,
            AmbientSky = C(0.17f, 0.21f, 0.36f), AmbientEquator = C(0.15f, 0.17f, 0.26f), AmbientGround = C(0.08f, 0.08f, 0.10f), Haze = C(0.09f, 0.11f, 0.19f),
            Zenith = C(0.02f, 0.035f, 0.10f), Horizon = C(0.13f, 0.15f, 0.26f), Glow = C(0.36f, 0.39f, 0.48f), CloudLit = C(0.17f, 0.18f, 0.26f), CloudShade = C(0.07f, 0.08f, 0.14f),
            Lights = 1f,
        };
        // The moon set and the sun not yet up: night colours with the light out, before the change-over to the sun.
        public static readonly Look Moonset = With(Night, intensity: 0f);
        // Dawn: the sun under the horizon in the east, a cold pink glow; sunrise: a low warm sun, the lamps going out.
        public static readonly Look Dawn = new Look
        {
            Sun = C(0.9f, 0.62f, 0.55f), Intensity = 0f, Shadow = 0.6f,
            AmbientSky = C(0.24f, 0.27f, 0.42f), AmbientEquator = C(0.30f, 0.27f, 0.33f), AmbientGround = C(0.12f, 0.12f, 0.14f), Haze = C(0.32f, 0.31f, 0.40f),
            Zenith = C(0.14f, 0.19f, 0.38f), Horizon = C(0.56f, 0.46f, 0.52f), Glow = C(0.70f, 0.42f, 0.32f), CloudLit = C(0.52f, 0.44f, 0.50f), CloudShade = C(0.26f, 0.26f, 0.38f),
            Lights = 0.6f,
        };
        public static readonly Look Sunrise = new Look
        {
            Sun = C(1f, 0.66f, 0.44f), Intensity = 0.95f, Shadow = 0.75f,
            AmbientSky = C(0.40f, 0.45f, 0.60f), AmbientEquator = C(0.50f, 0.44f, 0.44f), AmbientGround = C(0.24f, 0.21f, 0.20f), Haze = C(0.62f, 0.56f, 0.58f),
            Zenith = C(0.30f, 0.44f, 0.72f), Horizon = C(0.86f, 0.66f, 0.56f), Glow = C(1.0f, 0.66f, 0.42f), CloudLit = C(0.96f, 0.80f, 0.72f), CloudShade = C(0.56f, 0.56f, 0.66f),
            Lights = 0.1f,
        };
        private static Look With(Look l, float intensity) => new Look
        {
            Sun = l.Sun, Intensity = intensity, Shadow = l.Shadow, AmbientSky = l.AmbientSky, AmbientEquator = l.AmbientEquator,
            AmbientGround = l.AmbientGround, Haze = l.Haze, Zenith = l.Zenith, Horizon = l.Horizon, Glow = l.Glow, CloudLit = l.CloudLit,
            CloudShade = l.CloudShade, Lights = l.Lights,
        };

        // A key: its hour, the sun's or the moon's azimuth (degrees from north, +Z, toward east, +X) and elevation, and its mood.
        // Moon: the light is the moon; on a change-over key (Moon differs from the key before) the direction switches at once.
        public sealed class Key
        {
            public float Hour, Azimuth, Elevation; public bool Moon; public Look Look;
            public Key(float hour, float azimuth, float elevation, Look look, bool moon = false) { Hour = hour; Azimuth = azimuth; Elevation = elevation; Look = look; Moon = moon; }
        }
        // Around the clock from midnight. The bright day keeps its colours from morning to 15:00 while the sun goes round.
        public static readonly Key[] Keys = BuildKeys();
        private static Key[] BuildKeys()
        {
            Angles(DayTravel, out float dayAz, out float dayEl);
            Angles(GoldenTravel, out float goldenAz, out float goldenEl);
            return new[]
            {
                new Key(4f, 225, 26, Night, moon: true),
                new Key(5f, 240, 18, Moonset, moon: true),
                new Key(5.5f, 72, -3, Dawn),
                new Key(6.5f, 82, 4, Sunrise),
                new Key(8.5f, 108, 25, BrightDay),
                new Key(12f, 172, 47, BrightDay),
                new Key(DayHour, dayAz, dayEl, BrightDay),
                new Key(GoldenHour, goldenAz, goldenEl, Golden),
                new Key(SunsetHour, 262, 3, Sunset),
                new Key(TwilightHour, 266, -2, Twilight),
                new Key(DuskHour, 140, 34, Dusk, moon: true),
                new Key(NightHour, 150, 40, Night, moon: true),
            };
        }

        // The work day's clock: StartHour at the start, EndHour (midnight) when its time is up.
        public static float HourAt(float elapsedSeconds, float durationSeconds)
        {
            if (!(durationSeconds > 0) || float.IsNaN(elapsedSeconds)) return StartHour;
            float t = Math.Min(1, Math.Max(0, elapsedSeconds / durationSeconds));
            return StartHour + (EndHour - StartHour) * t;
        }
        // The city clock of a work day, as the HUD shows it and the scene's light follows it: it stands while the day's time
        // does (pause, journal, screens that stop the day).
        public static float HourOf(BrokerDay day) => day == null ? StartHour : HourAt(BrokerDay.DurationSeconds - day.RemainingSeconds, BrokerDay.DurationSeconds);
        // "hh:mm" on the 24-hour clock; midnight is 00:00.
        public static string Clock(float hour)
        {
            int minutes = (int)Math.Floor(Wrap(hour) * 60 + 1e-3) % (24 * 60);
            return (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00");
        }

        public static DayLight At(float hour)
        {
            float h = Wrap(hour);
            int next = 0;
            while (next < Keys.Length && Keys[next].Hour <= h) next++;
            var a = Keys[(next + Keys.Length - 1) % Keys.Length]; var b = Keys[next % Keys.Length];
            float span = b.Hour - a.Hour; if (span <= 0) span += 24;
            float since = h - a.Hour; if (since < 0) since += 24;
            float t = SkyLook.SmoothStep(0, 1, since / span);
            // The light changes over only where the key before has it out: the direction is the new body's at once.
            bool over = a.Moon != b.Moon && t > 0;
            float az = over ? b.Azimuth : a.Azimuth + (b.Azimuth - a.Azimuth) * t;
            float el = over ? b.Elevation : a.Elevation + (b.Elevation - a.Elevation) * t;
            var l = new DayLight { Hour = h, Moon = t > 0 ? b.Moon : a.Moon, Azimuth = az, Elevation = el };
            Toward(az, el, out l.SkyX, out l.SkyY, out l.SkyZ);
            Toward(az, Math.Max(el, MinLightElevation), out float x, out float y, out float z);
            l.LightX = -x; l.LightY = -y; l.LightZ = -z;
            Look p = a.Look, q = b.Look;
            l.Sun = Mix(p.Sun, q.Sun, t); l.Intensity = Mix(p.Intensity, q.Intensity, t); l.Shadow = Mix(p.Shadow, q.Shadow, t);
            l.AmbientSky = Mix(p.AmbientSky, q.AmbientSky, t); l.AmbientEquator = Mix(p.AmbientEquator, q.AmbientEquator, t);
            l.AmbientGround = Mix(p.AmbientGround, q.AmbientGround, t); l.Haze = Mix(p.Haze, q.Haze, t);
            l.Zenith = Mix(p.Zenith, q.Zenith, t); l.Horizon = Mix(p.Horizon, q.Horizon, t); l.Glow = Mix(p.Glow, q.Glow, t);
            l.CloudLit = Mix(p.CloudLit, q.CloudLit, t); l.CloudShade = Mix(p.CloudShade, q.CloudShade, t);
            l.Lights = Mix(p.Lights, q.Lights, t);
            return l;
        }

        // Azimuth and elevation (degrees) of the body a light travelling along `travel` comes from.
        public static void Angles(float[] travel, out float azimuth, out float elevation)
        {
            double x = -travel[0], y = -travel[1], z = -travel[2], length = Math.Sqrt(x * x + y * y + z * z);
            elevation = (float)(Math.Asin(y / length) * 180 / Math.PI);
            double az = Math.Atan2(x, z) * 180 / Math.PI; if (az < 0) az += 360;
            azimuth = (float)az;
        }
        // Unit direction toward a body at the given azimuth and elevation (degrees).
        public static void Toward(float azimuth, float elevation, out float x, out float y, out float z)
        {
            double a = azimuth * Math.PI / 180, e = elevation * Math.PI / 180;
            x = (float)(Math.Sin(a) * Math.Cos(e)); y = (float)Math.Sin(e); z = (float)(Math.Cos(a) * Math.Cos(e));
        }
        private static float Wrap(float hour)
        {
            if (float.IsNaN(hour) || float.IsInfinity(hour)) return DayHour;
            float h = hour % 24; return h < 0 ? h + 24 : h;
        }
        private static float Mix(float a, float b, float t) => a + (b - a) * t;
        private static SkyLook.Rgb Mix(SkyLook.Rgb a, SkyLook.Rgb b, float t) => t <= 0 ? a : t >= 1 ? b : SkyLook.Rgb.Lerp(a, b, t);
    }

    // The light of one hour (DayCycle.At): sRGB colours as written to Unity, directions as unit vectors.
    public sealed class DayLight
    {
        public float Hour, Azimuth, Elevation;
        // The light is the moon (night) rather than the sun.
        public bool Moon;
        // Direction the directional light travels (y < 0: never from below DayCycle.MinLightElevation).
        public float LightX, LightY, LightZ;
        // Toward the sun or the moon for the sky's disk and glow; under the horizon at dawn and twilight.
        public float SkyX, SkyY, SkyZ;
        public SkyLook.Rgb Sun; public float Intensity, Shadow;
        public SkyLook.Rgb AmbientSky, AmbientEquator, AmbientGround, Haze;
        public SkyLook.Rgb Zenith, Horizon, Glow, CloudLit, CloudShade;
        // 0 by day … 1 after dusk: lit windows and shop fronts, street lamps, brighter car lamps.
        public float Lights;
    }
}
