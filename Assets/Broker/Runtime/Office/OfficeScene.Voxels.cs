using Broker.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Broker.Runtime.Office
{
    // The low-poly models (buildings, street, people, interiors, cars) share one point-filtered 8×8 palette and the glowing
    // material on the Broker/Voxel shader, which darkens corners with the per-vertex shade; without it (or under a render
    // pipeline) Standard is used. The voxel generators themselves (VoxelProps) are no longer placed: the tests keep them
    // as the reference sizes of the models.
    public sealed partial class OfficeScene
    {
        private Texture2D voxelPalette;
        private Material voxelGlow;

        private void EnsureVoxelMaterials()
        {
            if (voxelGlow != null) return;
            byte[] rgba = VoxelPalette.TextureRgba();
            var pixels = new Color32[VoxelPalette.Count];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], 255);
            voxelPalette = new Texture2D(VoxelPalette.TextureSize, VoxelPalette.TextureSize, TextureFormat.RGBA32, false);
            voxelPalette.name = "Broker / voxel palette";
            voxelPalette.filterMode = FilterMode.Point; voxelPalette.wrapMode = TextureWrapMode.Clamp;
            voxelPalette.SetPixels32(pixels); voxelPalette.Apply(false, true);
            var shader = GraphicsSettings.currentRenderPipeline == null ? Shader.Find(VoxelShader) : null;
            if (shader != null && !shader.isSupported) shader = null;
            // Glowing cells are mostly emission: a dim albedo keeps lit windows amber in direct sun instead of washing out to white.
            // The emission is HDR, so lamps, lit windows and headlights reach the bloom threshold of the screen pass.
            voxelGlow = VoxelMat(shader, "#7A7A7A"); voxelGlow.name = "Broker / voxel glow";
            UseTexture(voxelGlow, voxelPalette, 0.25f);
            voxelGlow.EnableKeyword("_EMISSION");
            voxelGlow.SetTexture("_EmissionMap", voxelPalette);
            voxelGlow.SetColor("_EmissionColor", new Color(GlowEmission, GlowEmission, GlowEmission));
            voxelGlow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        public const string VoxelShader = "Broker/Voxel";
        public const float GlowEmission = 0.9f;
        private Material VoxelMat(Shader shader, string hex)
        {
            if (shader == null) return Mat(hex);
            var material = new Material(shader); ColorUtility.TryParseHtmlString(hex, out var color); material.color = color;
            materials.Add(material); return material;
        }
        private static void UseTexture(Material material, Texture texture, float smoothness)
        {
            // Without a texture the shader's default (white) stays: the colour alone.
            if (texture != null && material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (texture != null && material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        }
        private void ReleaseVoxels()
        {
            if (voxelPalette != null) Destroy(voxelPalette);
        }
    }
}
