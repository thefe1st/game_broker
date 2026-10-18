using System.Collections.Generic;
using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    public sealed partial class OfficeScene
    {
        private readonly SurfaceLibrary surfaces = new SurfaceLibrary();
        private readonly Dictionary<Material, float> surfaceTiles = new Dictionary<Material, float>();
        private readonly List<Mesh> surfaceMeshes = new List<Mesh>();
        private Material Surface(string key, string tint, float metresPerTile, float smoothness, float normalStrength)
        {
            Material material = Mat(tint);
            surfaces.Apply(material, key, smoothness, normalStrength);
            surfaceTiles.Add(material, metresPerTile); return material;
        }
        private void MapSurface(GameObject obj, PrimitiveType kind, Vector3 size, Material material)
        {
            if (kind != PrimitiveType.Cube || !surfaceTiles.TryGetValue(material, out float tile)) return;
            var filter = obj.GetComponent<MeshFilter>();
            Mesh mesh = Instantiate(filter.sharedMesh); mesh.name = "Broker metric UV / " + obj.name;
            Vector3 offset = transform.InverseTransformPoint(obj.transform.position);
            var vertices = mesh.vertices; var normals = mesh.normals; var uv = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 n = normals[i]; int axis = Mathf.Abs(n.x) > 0.5f ? 0 : Mathf.Abs(n.y) > 0.5f ? 1 : 2;
                SurfaceUv point = SurfaceMapping.Box(vertices[i].x + offset.x / Mathf.Abs(size.x),
                    vertices[i].y + offset.y / Mathf.Abs(size.y), vertices[i].z + offset.z / Mathf.Abs(size.z), axis,
                    Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z), tile);
                uv[i] = new Vector2(point.U, point.V);
            }
            mesh.uv = uv; mesh.RecalculateTangents(); filter.sharedMesh = mesh; surfaceMeshes.Add(mesh);
        }
        private void ReleaseSurfaces()
        {
            foreach (var mesh in surfaceMeshes) if (mesh != null) Destroy(mesh);
            surfaceMeshes.Clear(); surfaceTiles.Clear(); surfaces.Dispose();
        }
    }
}
