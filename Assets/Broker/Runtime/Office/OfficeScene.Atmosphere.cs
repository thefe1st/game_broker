using Broker.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Broker.Runtime.Office
{
    // Golden hour: a low warm sun from the west, cool three-colour ambient in the shade, a deep orange linear haze and the
    // Broker/Sky dome (Core/SkyLook): its pale horizon sits above the haze, so far blocks read as warm silhouettes, and
    // below the horizon it shows the haze itself. CameraPost adds the sun's glow to the haze. Every global setting is
    // saved first and restored when the office scene is destroyed.
    public sealed partial class OfficeScene
    {
        public static readonly Color Haze = Rgb(SkyLook.Haze);
        // Direction the light travels: east, down (about 17° sun) and slightly north, so plaza faces and tower fronts catch the sun.
        public static readonly Vector3 SunDirection = new Vector3(0.88f, -0.29f, 0.31f);
        public const string SkyShader = "Broker/Sky";
        private Light sun, previousSun;
        private Material previousSkybox;
        private Color previousSkyColor, previousEquatorColor, previousGroundColor;
        private float previousShadowDistance;
        private bool atmosphereSaved;

        private void BuildAtmosphere()
        {
            atmosphereSaved = true;
            previousSun = RenderSettings.sun; previousSkybox = RenderSettings.skybox;
            previousSkyColor = RenderSettings.ambientSkyColor; previousEquatorColor = RenderSettings.ambientEquatorColor;
            previousGroundColor = RenderSettings.ambientGroundColor; previousShadowDistance = QualitySettings.shadowDistance;
            var sunObject = new GameObject("Golden hour sun"); sunObject.transform.SetParent(transform, false);
            sunObject.transform.rotation = Quaternion.LookRotation(SunDirection.normalized, Vector3.up);
            sun = sunObject.AddComponent<Light>(); sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.72f, 0.45f); sun.intensity = 1.4f;
            sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.86f; sun.shadowBias = 0.05f; sun.shadowNormalBias = 0.35f;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.45f, 0.56f);
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.48f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.25f, 0.22f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = Haze;
            RenderSettings.fogStartDistance = CityScenery.FogStart; RenderSettings.fogEndDistance = CityScenery.FogEnd;
            QualitySettings.shadowDistance = Mathf.Max(previousShadowDistance, 110);
            Material sky = null;
            var skyShader = GraphicsSettings.currentRenderPipeline == null ? Shader.Find(SkyShader) : null;
            if (skyShader != null && skyShader.isSupported)
            {
                sky = new Material(skyShader) { name = "Broker / golden hour sky" }; materials.Add(sky);
                PaintSky(sky); RenderSettings.skybox = sky;
            }
            else if (previousSkybox != null && previousSkybox.shader != null && previousSkybox.shader.name == "Skybox/Procedural")
            {
                sky = new Material(previousSkybox) { name = "Broker / golden hour sky" }; materials.Add(sky);
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
        }
        // Every SkyLook value goes into the material, so the shader and the offline preview read the same numbers.
        private static void PaintSky(Material sky)
        {
            sky.SetColor("_Zenith", Rgb(SkyLook.Zenith)); sky.SetColor("_Horizon", Rgb(SkyLook.Horizon)); sky.SetColor("_Ground", Rgb(SkyLook.Ground));
            sky.SetColor("_Glow", Rgb(SkyLook.Glow)); sky.SetColor("_CloudLit", Rgb(SkyLook.CloudLit)); sky.SetColor("_CloudShade", Rgb(SkyLook.CloudShade));
            var toSun = -SunDirection.normalized;
            sky.SetVector("_SunDirection", new Vector4(toSun.x, toSun.y, toSun.z, 0));
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
