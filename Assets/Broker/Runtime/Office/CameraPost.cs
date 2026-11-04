using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    // Screen pass of an eye camera (Shaders/BrokerPost.shader): screen-space occlusion, the sun's glow in the haze, bloom
    // pyramid, ACES curve, warm grade, vignette and dither with the values of ScreenLook. Occlusion needs the depth texture and a perspective camera.
    // Without the shader, half-float targets or image effects the component switches itself off and the camera renders as before.
    [RequireComponent(typeof(Camera))]
    public sealed class CameraPost : MonoBehaviour
    {
        public const string ShaderName = "Hidden/Broker/Post";
        private const int PrefilterPass = 0, DownPass = 1, UpPass = 2, OcclusionPass = 3, BlurPass = 4, CompositePass = 5;
        private static readonly int BaseTexId = Shader.PropertyToID("_BaseTex"),
            BloomTexId = Shader.PropertyToID("_BloomTex"), OcclusionTexId = Shader.PropertyToID("_OcclusionTex"),
            BloomId = Shader.PropertyToID("_Bloom"), OcclusionId = Shader.PropertyToID("_Occlusion"), GradeId = Shader.PropertyToID("_Grade"),
            WarmId = Shader.PropertyToID("_Warm"), BlurStepId = Shader.PropertyToID("_BlurStep"),
            HazeId = Shader.PropertyToID("_Haze"), HazeSunId = Shader.PropertyToID("_HazeSun"), HazeUpId = Shader.PropertyToID("_HazeUp"),
            HazeGlowId = Shader.PropertyToID("_HazeGlow");
        private readonly RenderTexture[] down = new RenderTexture[ScreenLook.BloomLevels], up = new RenderTexture[ScreenLook.BloomLevels];
        private Camera view;
        private Material material;
        private RenderTextureFormat occlusionFormat;
        // Occlusion costs a depth pre-pass of the scene; the office overview (orthographic) never uses it.
        public bool Occlusion { get; set; } = true;
        public bool Active => material != null && enabled;

        private void Awake()
        {
            view = GetComponent<Camera>();
            var shader = Shader.Find(ShaderName);
            if (shader == null || !shader.isSupported || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
            {
                enabled = false; return;
            }
            material = new Material(shader) { name = "Broker / screen look", hideFlags = HideFlags.HideAndDontSave };
            occlusionFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGHalf) ? RenderTextureFormat.RGHalf : RenderTextureFormat.ARGBHalf;
            view.allowHDR = true;
        }
        private void OnEnable()
        {
            if (material == null) { enabled = false; return; }
            if (UsesOcclusion) view.depthTextureMode |= DepthTextureMode.Depth;
        }
        private void OnDestroy()
        {
            if (material != null) Destroy(material);
            material = null;
        }
        private bool UsesOcclusion => Occlusion && view != null && !view.orthographic;

        // Sun glow in the haze (ScreenLook.HazeGlowAt) needs the depth texture, linear fog and a sun: the fog range, the
        // directions toward the sun and world up in the camera's space (+z forward, as ViewPosition in the shader) and the linear
        // glow colour of the sky. Strength 0 switches it off.
        private void SetHaze(bool depth)
        {
            var sun = RenderSettings.sun;
            bool on = depth && RenderSettings.fog && RenderSettings.fogMode == FogMode.Linear && sun != null && sun.isActiveAndEnabled;
            material.SetVector(HazeId, new Vector4(RenderSettings.fogStartDistance, RenderSettings.fogEndDistance, SkyLook.HaloPower, on ? SkyLook.HaloStrength : 0));
            if (!on) return;
            // The camera's own axes: x right, y up, z along the view, as ViewPosition in the shader.
            Vector3 toSun = view.transform.InverseTransformDirection(-sun.transform.forward), up = view.transform.InverseTransformDirection(Vector3.up);
            material.SetVector(HazeSunId, new Vector4(toSun.x, toSun.y, toSun.z, 0));
            material.SetVector(HazeUpId, new Vector4(up.x, up.y, up.z, SkyLook.HorizonFalloff));
            var glow = new Color(SkyLook.Glow.R, SkyLook.Glow.G, SkyLook.Glow.B).linear;
            material.SetVector(HazeGlowId, new Vector4(glow.r, glow.g, glow.b, 0));
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (material == null) { Graphics.Blit(source, destination); return; }
            material.SetVector(BloomId, new Vector4(ScreenLook.BloomThreshold, ScreenLook.BloomKnee, ScreenLook.BloomIntensity, 0));
            material.SetVector(OcclusionId, new Vector4(ScreenLook.OcclusionRadius, ScreenLook.OcclusionStrength, ScreenLook.OcclusionFadeStart, ScreenLook.OcclusionFadeEnd));
            material.SetVector(GradeId, new Vector4(ScreenLook.Exposure, ScreenLook.Saturation, ScreenLook.Contrast, ScreenLook.Vignette));
            material.SetVector(WarmId, new Vector4(ScreenLook.WarmR, ScreenLook.WarmG, ScreenLook.WarmB, ScreenLook.MidGrey));

            RenderTexture occlusion = null;
            if (UsesOcclusion && (view.depthTextureMode & DepthTextureMode.Depth) != 0)
            {
                int w = Mathf.Max(1, source.width / 2), h = Mathf.Max(1, source.height / 2);
                occlusion = RenderTexture.GetTemporary(w, h, 0, occlusionFormat, RenderTextureReadWrite.Linear);
                var blur = RenderTexture.GetTemporary(w, h, 0, occlusionFormat, RenderTextureReadWrite.Linear);
                occlusion.filterMode = FilterMode.Bilinear; blur.filterMode = FilterMode.Bilinear;
                Graphics.Blit(source, occlusion, material, OcclusionPass);
                material.SetVector(BlurStepId, new Vector4(1f / w, 0, 0, 0)); Graphics.Blit(occlusion, blur, material, BlurPass);
                material.SetVector(BlurStepId, new Vector4(0, 1f / h, 0, 0)); Graphics.Blit(blur, occlusion, material, BlurPass);
                RenderTexture.ReleaseTemporary(blur);
                material.SetTexture(OcclusionTexId, occlusion);
            }
            else material.SetTexture(OcclusionTexId, Texture2D.whiteTexture);
            SetHaze(occlusion != null);

            // Bloom: prefiltered half resolution, downsampled level by level, then upsampled back while adding each level.
            int width = Mathf.Max(1, source.width / 2), height = Mathf.Max(1, source.height / 2), levels = 0;
            var format = RenderTextureFormat.ARGBHalf;
            for (int i = 0; i < ScreenLook.BloomLevels && width >= 2 && height >= 2; i++, levels++)
            {
                down[i] = RenderTexture.GetTemporary(width, height, 0, format, RenderTextureReadWrite.Linear);
                down[i].filterMode = FilterMode.Bilinear;
                Graphics.Blit(i == 0 ? source : down[i - 1], down[i], material, i == 0 ? PrefilterPass : DownPass);
                width /= 2; height /= 2;
            }
            var bloom = levels > 0 ? down[levels - 1] : null;
            for (int i = levels - 2; i >= 0; i--)
            {
                up[i] = RenderTexture.GetTemporary(down[i].width, down[i].height, 0, format, RenderTextureReadWrite.Linear);
                up[i].filterMode = FilterMode.Bilinear;
                material.SetTexture(BaseTexId, down[i]);
                Graphics.Blit(bloom, up[i], material, UpPass);
                bloom = up[i];
            }
            material.SetTexture(BloomTexId, bloom != null ? (Texture)bloom : Texture2D.blackTexture);
            Graphics.Blit(source, destination, material, CompositePass);

            for (int i = 0; i < ScreenLook.BloomLevels; i++)
            {
                if (down[i] != null) { RenderTexture.ReleaseTemporary(down[i]); down[i] = null; }
                if (up[i] != null) { RenderTexture.ReleaseTemporary(up[i]); up[i] = null; }
            }
            if (occlusion != null) RenderTexture.ReleaseTemporary(occlusion);
        }
    }
}
