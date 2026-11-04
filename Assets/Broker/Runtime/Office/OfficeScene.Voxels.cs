using System;
using System.Collections.Generic;
using Broker.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Broker.Runtime.Office
{
    // Voxel props share one point-filtered 8×8 palette and two materials (plain and glowing) on the Broker/Voxel shader,
    // which darkens corners with the mesher's per-vertex shade; without it (or under a render pipeline) Standard is used.
    // Each distinct model is meshed once per scene; gameplay colliders stay on the original boxes.
    public sealed partial class OfficeScene
    {
        private sealed class VoxelAsset { public Mesh Mesh; public Material[] Materials; }
        private readonly Dictionary<string, VoxelAsset> voxelAssets = new Dictionary<string, VoxelAsset>();
        private Texture2D voxelPalette;
        private Material voxelSolid, voxelGlow;

        private void EnsureVoxelMaterials()
        {
            if (voxelSolid != null) return;
            byte[] rgba = VoxelPalette.TextureRgba();
            var pixels = new Color32[VoxelPalette.Count];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], 255);
            voxelPalette = new Texture2D(VoxelPalette.TextureSize, VoxelPalette.TextureSize, TextureFormat.RGBA32, false);
            voxelPalette.name = "Broker / voxel palette";
            voxelPalette.filterMode = FilterMode.Point; voxelPalette.wrapMode = TextureWrapMode.Clamp;
            voxelPalette.SetPixels32(pixels); voxelPalette.Apply(false, true);
            var shader = GraphicsSettings.currentRenderPipeline == null ? Shader.Find(VoxelShader) : null;
            if (shader != null && !shader.isSupported) shader = null;
            voxelSolid = VoxelMat(shader, "#FFFFFF"); voxelSolid.name = "Broker / voxel";
            UseTexture(voxelSolid, voxelPalette, 0.08f);
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
        public const float GlowEmission = 1.35f;
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
        private VoxelAsset VoxelAssetFor(string key, Func<VoxelModel> build)
        {
            if (voxelAssets.TryGetValue(key, out var cached)) return cached;
            EnsureVoxelMaterials();
            var data = VoxelMesher.Build(build());
            int count = data.VertexCount;
            var vertices = new Vector3[count]; var normals = new Vector3[count]; var uv = new Vector2[count]; var shades = new Color32[count];
            for (int i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(data.Positions[i * 3], data.Positions[i * 3 + 1], data.Positions[i * 3 + 2]);
                normals[i] = new Vector3(data.Normals[i * 3], data.Normals[i * 3 + 1], data.Normals[i * 3 + 2]);
                uv[i] = new Vector2(data.Uvs[i * 2], data.Uvs[i * 2 + 1]);
                byte shade = data.Shades[i]; shades[i] = new Color32(shade, shade, shade, 255);
            }
            var mesh = new Mesh { name = "Broker voxel / " + key };
            if (count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.colors32 = shades;
            bool solid = data.Triangles.Length > 0, glow = data.GlowTriangles.Length > 0;
            mesh.subMeshCount = solid && glow ? 2 : 1;
            if (solid) mesh.SetTriangles(data.Triangles, 0);
            if (glow) mesh.SetTriangles(data.GlowTriangles, solid ? 1 : 0);
            mesh.RecalculateBounds();
            var asset = new VoxelAsset { Mesh = mesh, Materials = solid && glow ? new[] { voxelSolid, voxelGlow } : new[] { solid ? voxelSolid : voxelGlow } };
            voxelAssets.Add(key, asset); return asset;
        }
        // Places a voxel model with its pivot (bottom centre unless the generator says otherwise) at a local position.
        private GameObject PlaceVoxel(string key, Func<VoxelModel> build, string name, Transform parent, Vector3 position, float yaw = 0)
        {
            var asset = VoxelAssetFor(key, build);
            var obj = new GameObject(name); obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position; obj.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            obj.AddComponent<MeshFilter>().sharedMesh = asset.Mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterials = asset.Materials;
            return obj;
        }
        // A jointed person: body at the root, head on the neck, arms on the shoulders.
        private void PlaceFigure(PersonStyle style, Transform root, out Transform head, out Transform leftArm, out Transform rightArm)
        {
            VoxelFigure figure = null;
            VoxelFigure Figure() => figure ?? (figure = VoxelProps.Person(style));
            string key = "person/" + style;
            PlaceVoxel(key + "/body", () => Figure().Body, "Voxel body", root, Vector3.zero);
            head = PlaceVoxel(key + "/head", () => Figure().Head, "Voxel head", root, new Vector3(0, Figure().NeckY, 0)).transform;
            leftArm = PlaceVoxel(key + "/arm", () => Figure().LeftArm, "Voxel left arm", root, new Vector3(-Figure().ShoulderX, Figure().ShoulderY, 0)).transform;
            rightArm = PlaceVoxel(key + "/arm", () => Figure().RightArm, "Voxel right arm", root, new Vector3(Figure().ShoulderX, Figure().ShoulderY, 0)).transform;
        }
        private void ReleaseVoxels()
        {
            foreach (var asset in voxelAssets.Values) if (asset.Mesh != null) Destroy(asset.Mesh);
            voxelAssets.Clear();
            if (voxelPalette != null) Destroy(voxelPalette);
        }
    }
}
