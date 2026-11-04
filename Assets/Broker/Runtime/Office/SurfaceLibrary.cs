using System;
using System.Collections.Generic;
using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    // Smooth surfaces painted from the voxel palette (VoxelSurfaces; bilinear with mipmaps), built once per scene.
    // Everything comes from code: the game ships no texture files and downloads nothing.
    public sealed class SurfaceLibrary : IDisposable
    {
        private readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        private readonly List<Texture2D> owned = new List<Texture2D>();
        public Texture2D Albedo(string key)
        {
            if (cache.TryGetValue(key, out Texture2D found)) return found;
            byte[] rgba = VoxelSurfaces.Rgba(key);
            var pixels = new Color32[VoxelSurfaces.Size * VoxelSurfaces.Size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3]);
            var texture = new Texture2D(VoxelSurfaces.Size, VoxelSurfaces.Size, TextureFormat.RGBA32, true);
            texture.name = "Broker / surface " + key;
            texture.SetPixels32(pixels);
            texture.wrapMode = TextureWrapMode.Repeat; texture.filterMode = FilterMode.Trilinear; texture.anisoLevel = 4;
            texture.Apply(true, true);
            owned.Add(texture); cache[key] = texture; return texture;
        }
        // Unknown keys keep the flat tint and log once: a typo must not break the scene.
        public void Apply(Material material, string key, float smoothness)
        {
            if (VoxelSurfaces.Has(key))
            {
                Texture2D albedo = Albedo(key);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", albedo);
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", albedo);
            }
            else if (!cache.ContainsKey(key)) { cache[key] = null; Debug.LogWarning("Broker surface '" + key + "' has no pattern. Using a flat colour."); }
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        }
        public void Dispose()
        {
            foreach (var texture in owned) if (texture != null) UnityEngine.Object.Destroy(texture);
            owned.Clear(); cache.Clear();
        }
    }
}
