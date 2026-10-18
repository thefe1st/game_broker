using System;
using System.Collections.Generic;
using UnityEngine;

namespace Broker.Runtime.Office
{
    // Local CC0 images, decoded once per scene. There are no downloads in the game.
    // Native textures can override packed sources without changing scene code.
    public sealed class SurfaceLibrary : IDisposable
    {
        private readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        private readonly List<Texture2D> owned = new List<Texture2D>();
        public Texture2D Load(string key, bool linear = false)
        {
            string cacheKey = key + (linear ? ":linear" : ":srgb");
            if (cache.TryGetValue(cacheKey, out Texture2D found)) return found;
            string path = "BrokerMaterials/" + key;
            var native = Resources.Load<Texture2D>(path);
            if (native != null) { cache[cacheKey] = native; return native; }
            var packed = Resources.Load<TextAsset>(path + ".b64");
            if (packed == null) { cache[cacheKey] = null; return null; }
            Texture2D texture = null;
            try
            {
                if (packed.text.Length > 3000000) throw new FormatException("Packed surface exceeds limit");
                byte[] bytes = Convert.FromBase64String(packed.text);
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear);
                if (!ImageConversion.LoadImage(texture, bytes, true)) throw new FormatException("Invalid surface image");
                texture.name = "Broker / " + key;
                texture.wrapMode = TextureWrapMode.Repeat; texture.filterMode = FilterMode.Trilinear; texture.anisoLevel = 4;
                owned.Add(texture); cache[cacheKey] = texture; return texture;
            }
            catch (Exception exception) when (exception is FormatException || exception is ArgumentException)
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
                cache[cacheKey] = null;
                Debug.LogWarning("Broker material '" + key + "' could not load. Using a flat fallback.");
                return null;
            }
        }
        public void Apply(Material material, string key, float smoothness, float normalStrength)
        {
            Texture2D albedo = Load(key + "_albedo");
            if (albedo != null)
            {
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", albedo);
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", albedo);
            }
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (!material.HasProperty("_BumpMap")) return;
            Texture2D normal = Load(key + "_normal", true);
            if (normal == null) return;
            material.SetTexture("_BumpMap", normal); material.EnableKeyword("_NORMALMAP");
            if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", normalStrength);
        }
        public void Dispose()
        {
            foreach (var texture in owned) if (texture != null) UnityEngine.Object.Destroy(texture);
            owned.Clear(); cache.Clear();
        }
    }
}
