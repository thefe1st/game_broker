using Broker.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Broker.Runtime.Office
{
    // Golden hour: a low warm sun from the west, three-colour ambient, warm linear haze and a procedural sky.
    // Every global setting is saved first and restored when the office scene is destroyed.
    public sealed partial class OfficeScene
    {
        public static readonly Color Haze = new Color(0.86f, 0.73f, 0.6f);
        // Direction the light travels: east, down and slightly north, so the plaza faces and tower fronts catch the sun.
        public static readonly Vector3 SunDirection = new Vector3(0.875f, -0.376f, 0.305f);
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
            sun.color = new Color(1f, 0.76f, 0.5f); sun.intensity = 1.28f;
            sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.78f; sun.shadowBias = 0.05f; sun.shadowNormalBias = 0.35f;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.56f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.55f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.34f, 0.3f, 0.26f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = Haze;
            RenderSettings.fogStartDistance = CityScenery.FogStart; RenderSettings.fogEndDistance = CityScenery.FogEnd;
            QualitySettings.shadowDistance = Mathf.Max(previousShadowDistance, 110);
            Material sky = null;
            if (previousSkybox != null && previousSkybox.shader != null && previousSkybox.shader.name == "Skybox/Procedural")
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
        }
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
