using Broker.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Broker.Runtime.Office
{
    // The sky and light of the city (Core/DayCycle, stage 4): a directional light — the sun by day, the moon at night — soft
    // three-colour ambient in the shade, a linear haze and the Broker/Sky dome (Core/SkyLook): its horizon sits above the
    // haze, so far blocks fade into soft shapes, and below the horizon it shows the haze itself. BuildAtmosphere sets them
    // up at the approved bright day (PR #12); SetHour (OfficeScene.DayNight) moves them through the day. CameraPost adds the
    // sky's glow to the haze. Every global setting is saved first and restored when the office scene is destroyed.
    public sealed partial class OfficeScene
    {
        // The haze of the bright day, under the dome's horizon and behind the far city.
        public static readonly Color Haze = Rgb(SkyLook.Haze);
        public const string SkyShader = "Broker/Sky";
        private Light sun, previousSun;
        private Material previousSkybox, skyMaterial;
        private Color previousSkyColor, previousEquatorColor, previousGroundColor;
        private float previousShadowDistance;
        private bool atmosphereSaved;

        private void BuildAtmosphere()
        {
            atmosphereSaved = true;
            previousSun = RenderSettings.sun; previousSkybox = RenderSettings.skybox;
            previousSkyColor = RenderSettings.ambientSkyColor; previousEquatorColor = RenderSettings.ambientEquatorColor;
            previousGroundColor = RenderSettings.ambientGroundColor; previousShadowDistance = QualitySettings.shadowDistance;
            // Colours, intensity, direction and ambient come from DayCycle (ApplyDayLight below).
            var sunObject = new GameObject("Sun"); sunObject.transform.SetParent(transform, false);
            sun = sunObject.AddComponent<Light>(); sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft; sun.shadowBias = 0.05f; sun.shadowNormalBias = 0.35f;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = Haze;
            RenderSettings.fogStartDistance = CityScenery.FogStart; RenderSettings.fogEndDistance = CityScenery.FogEnd;
            QualitySettings.shadowDistance = Mathf.Max(previousShadowDistance, 110);
            Material sky = null;
            var skyShader = GraphicsSettings.currentRenderPipeline == null ? Shader.Find(SkyShader) : null;
            if (skyShader != null && skyShader.isSupported)
            {
                sky = new Material(skyShader) { name = "Broker / sky" }; materials.Add(sky);
                skyMaterial = sky; RenderSettings.skybox = sky;
            }
            else if (previousSkybox != null && previousSkybox.shader != null && previousSkybox.shader.name == "Skybox/Procedural")
            {
                sky = new Material(previousSkybox) { name = "Broker / sky" }; materials.Add(sky);
                SetIf(sky, "_SunSize", 0.06f); SetIf(sky, "_SunSizeConvergence", 3.5f);
                SetIf(sky, "_AtmosphereThickness", 1.45f); SetIf(sky, "_Exposure", 1.2f);
                if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", new Color(0.55f, 0.52f, 0.5f));
                if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", Haze);
                RenderSettings.skybox = sky;
            }
            DynamicGI.UpdateEnvironment();
            // Without the procedural sky the camera clears to the haze, so the horizon still melts into the fog.
            var eye = Player.Eye;
            eye.clearFlags = sky != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            eye.backgroundColor = Haze; eye.farClipPlane = CityScenery.FarClip;
            overview.backgroundColor = new Color(0.2f, 0.17f, 0.15f);
            // Screen pass (occlusion, bloom, tone curve, grade); it switches itself off where the shader cannot run.
            if (eye.GetComponent<CameraPost>() == null) eye.gameObject.AddComponent<CameraPost>();
            if (overview.GetComponent<CameraPost>() == null) overview.gameObject.AddComponent<CameraPost>().Occlusion = false;
            ApplyDayLight(DayCycle.At(DayCycle.DayHour));
        }
        // Every SkyLook value goes into the material, so the shader and the offline preview read the same numbers: the hour's
        // palette (the ground under the horizon is its haze) and the sun or the moon, the rest as SkyLook has it.
        private static void PaintSky(Material sky, DayLight light)
        {
            sky.SetColor("_Zenith", Rgb(light.Zenith)); sky.SetColor("_Horizon", Rgb(light.Horizon)); sky.SetColor("_Ground", Rgb(light.Haze));
            sky.SetColor("_Glow", Rgb(light.Glow)); sky.SetColor("_CloudLit", Rgb(light.CloudLit)); sky.SetColor("_CloudShade", Rgb(light.CloudShade));
            sky.SetVector("_SunDirection", new Vector4(light.SkyX, light.SkyY, light.SkyZ, 0));
            sky.SetVector("_Sun", new Vector4(SkyLook.HaloStrength, SkyLook.HaloPower, SkyLook.GlowStrength, SkyLook.GlowPower));
            sky.SetVector("_Disk", new Vector4(SkyLook.DiskStrength, SkyLook.DiskCos, SkyLook.DiskSoftCos, SkyLook.HorizonFalloff));
            sky.SetVector("_Cloud", new Vector4(SkyLook.CloudCover, SkyLook.CloudSoftness, SkyLook.CloudScale, SkyLook.CloudLift));
            sky.SetVector("_CloudShape", new Vector4(SkyLook.CloudStretch, SkyLook.CloudDrift, SkyLook.CloudFadeLow, SkyLook.CloudFadeHigh));
            sky.SetVector("_CloudLight", new Vector4(SkyLook.CloudOpacity, SkyLook.CloudLightStep, SkyLook.CloudLightGain, SkyLook.CloudSilver));
            sky.SetFloat("_CloudHaze", SkyLook.CloudHaze);
        }
        private static Color Rgb(SkyLook.Rgb c) => new Color(c.R, c.G, c.B);
        private static void SetIf(Material material, string property, float value)
        { if (material.HasProperty(property)) material.SetFloat(property, value); }
        private void RestoreAtmosphere()
        {
            if (!atmosphereSaved) return;
            atmosphereSaved = false;
            RenderSettings.sun = previousSun; RenderSettings.skybox = previousSkybox;
            RenderSettings.ambientSkyColor = previousSkyColor; RenderSettings.ambientEquatorColor = previousEquatorColor;
            RenderSettings.ambientGroundColor = previousGroundColor; QualitySettings.shadowDistance = previousShadowDistance;
        }
    }
}
