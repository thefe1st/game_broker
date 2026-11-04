using System;
using System.Collections.Generic;
using Broker.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Broker.Runtime.Office
{
    // Low-poly buildings from BuildingModels on the Broker/Voxel shader (Standard without it): walls on a tinted surface
    // texture box-mapped in metres, details on the voxel palette, dark glossy glass that mirrors the sky. Night glass has
    // a material of its own, the same as the glass by day, so the day–night stage can light those windows after dark.
    // Lamps and flames glow with the voxel props' emissive palette, brass and bronze take a half-metallic palette material.
    // Each model is meshed once; the buildings own no colliders (blockers and the plan's footprints stay as they were).
    public sealed partial class OfficeScene
    {
        private sealed class BuildingAsset { public Mesh Mesh; public int[] Parts; }
        private readonly Dictionary<string, BuildingAsset> buildingAssets = new Dictionary<string, BuildingAsset>();
        private readonly Dictionary<WallLook, Material> buildingWalls = new Dictionary<WallLook, Material>();
        private Material buildingTrim, buildingGlass, buildingNight, buildingMetal;
        // Glass is half metallic: it takes the colour of the sky it reflects, as in Schedule I, instead of a black hole.
        public const float BuildingWallSmoothness = 0.1f, BuildingTrimSmoothness = 0.2f, BuildingGlassSmoothness = 0.88f, BuildingGlassMetallic = 0.5f;
        public const string BuildingGlassColor = "#7F96AB";
        public const float BuildingMetalSmoothness = 0.55f, BuildingMetalMetallic = 0.6f;

        private void EnsureBuildingMaterials()
        {
            if (buildingTrim != null) return;
            EnsureVoxelMaterials();
            var shader = GraphicsSettings.currentRenderPipeline == null ? Shader.Find(VoxelShader) : null;
            if (shader != null && !shader.isSupported) shader = null;
            buildingTrim = VoxelMat(shader, "#FFFFFF"); buildingTrim.name = "Broker / building trim";
            UseTexture(buildingTrim, voxelPalette, BuildingTrimSmoothness);
            buildingGlass = BuildingGlass(shader, "Broker / building glass");
            buildingNight = BuildingGlass(shader, "Broker / building night glass");
            buildingMetal = VoxelMat(shader, "#FFFFFF"); buildingMetal.name = "Broker / building metal";
            UseTexture(buildingMetal, voxelPalette, BuildingMetalSmoothness);
            if (buildingMetal.HasProperty("_Metallic")) buildingMetal.SetFloat("_Metallic", BuildingMetalMetallic);
        }
        private Material BuildingGlass(Shader shader, string name)
        {
            var material = VoxelMat(shader, BuildingGlassColor); material.name = name;
            UseTexture(material, null, BuildingGlassSmoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", BuildingGlassMetallic);
            return material;
        }
        private Material BuildingWall(WallLook look)
        {
            if (buildingWalls.TryGetValue(look, out var found)) return found;
            var shader = GraphicsSettings.currentRenderPipeline == null ? Shader.Find(VoxelShader) : null;
            if (shader != null && !shader.isSupported) shader = null;
            var material = VoxelMat(shader, "#" + look.Tint.ToString("X6")); material.name = "Broker / building wall / " + look;
            UseTexture(material, surfaces.Albedo(look.Surface), BuildingWallSmoothness);
            buildingWalls.Add(look, material); return material;
        }
        // Places a building with its footprint centre at a local position; the front (−Z) turns with yaw. Far scenery that
        // the fog dissolves casts no shadows.
        private GameObject PlaceBuilding(string key, Func<PolyMesh> build, WallLook look, string name, Vector3 position, float yaw = 0,
            bool castShadows = true)
        {
            EnsureBuildingMaterials();
            if (!buildingAssets.TryGetValue(key, out var asset))
            {
                var data = build(); var parts = new List<int>();
                for (int p = 0; p < data.Triangles.Length; p++) if (data.Triangles[p].Length > 0) parts.Add(p);
                asset = new BuildingAsset { Mesh = PolyToMesh(data, "Broker building / " + key, parts.ToArray()), Parts = parts.ToArray() };
                buildingAssets.Add(key, asset);
            }
            var materials = new Material[asset.Parts.Length];
            for (int k = 0; k < materials.Length; k++)
                switch ((BuildingPart)asset.Parts[k])
                {
                    case BuildingPart.Wall: materials[k] = BuildingWall(look); break;
                    case BuildingPart.Glass: materials[k] = buildingGlass; break;
                    case BuildingPart.Night: materials[k] = buildingNight; break;
                    case BuildingPart.Glow: materials[k] = voxelGlow; break;
                    case BuildingPart.Metal: materials[k] = buildingMetal; break;
                    default: materials[k] = buildingTrim; break;
                }
            var obj = new GameObject(name); obj.transform.SetParent(transform, false);
            obj.transform.localPosition = position; obj.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            obj.AddComponent<MeshFilter>().sharedMesh = asset.Mesh;
            var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
            if (!castShadows) renderer.shadowCastingMode = ShadowCastingMode.Off;
            return obj;
        }
        // Unity mesh with the given parts of a PolyMesh as its submeshes; the vertex colour carries the corner shade.
        private static Mesh PolyToMesh(PolyMesh data, string name, int[] parts)
        {
            int count = data.VertexCount;
            var vertices = new Vector3[count]; var normals = new Vector3[count]; var uv = new Vector2[count]; var shades = new Color32[count];
            for (int i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(data.Positions[i * 3], data.Positions[i * 3 + 1], data.Positions[i * 3 + 2]);
                normals[i] = new Vector3(data.Normals[i * 3], data.Normals[i * 3 + 1], data.Normals[i * 3 + 2]);
                uv[i] = new Vector2(data.Uvs[i * 2], data.Uvs[i * 2 + 1]);
                byte shade = data.Shades[i]; shades[i] = new Color32(shade, shade, shade, 255);
            }
            var mesh = new Mesh { name = name };
            if (count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.colors32 = shades;
            mesh.subMeshCount = parts.Length;
            for (int k = 0; k < parts.Length; k++) mesh.SetTriangles(data.Triangles[parts[k]], k);
            mesh.RecalculateBounds();
            return mesh;
        }
        private void ReleaseBuildings()
        {
            foreach (var asset in buildingAssets.Values) if (asset.Mesh != null) Destroy(asset.Mesh);
            buildingAssets.Clear(); buildingWalls.Clear();
        }
    }
}
