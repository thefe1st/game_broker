// Built-in primitive meshes (Unity sizes: unit cube, sphere radius 0.5, cylinder and capsule 2 tall) and a camera-only player.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine
{
    public static class PrimitiveMeshes
    {
        private static readonly Dictionary<PrimitiveType, Mesh> cache = new Dictionary<PrimitiveType, Mesh>();
        private static Material defaultMaterial;
        public static Material DefaultMaterial => defaultMaterial ?? (defaultMaterial = new Material(Shader.Find("Standard")) { name = "Default-Material" });
        public static Mesh Get(PrimitiveType type)
        {
            if (cache.TryGetValue(type, out var mesh)) return mesh;
            mesh = type == PrimitiveType.Cube ? Cube() : type == PrimitiveType.Sphere ? Lathe(type, 24, 16, 0, 0)
                : type == PrimitiveType.Cylinder ? Cylinder(24) : type == PrimitiveType.Capsule ? Lathe(type, 24, 16, 0.5f, 0.5f)
                : type == PrimitiveType.Plane ? Flat(10, true) : Flat(1, false);
            mesh.name = type.ToString(); cache[type] = mesh; return mesh;
        }
        private sealed class Builder
        {
            public readonly List<Vector3> V = new List<Vector3>(), N = new List<Vector3>();
            public readonly List<Vector2> U = new List<Vector2>(); public readonly List<int> T = new List<int>();
            public int Add(Vector3 v, Vector3 n, Vector2 u) { V.Add(v); N.Add(n); U.Add(u); return V.Count - 1; }
            // Emits a triangle whose cross(b - a, c - a) points along the wanted outward normal.
            public void Tri(int a, int b, int c, Vector3 outward)
            {
                var n = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
                if (Vector3.Dot(n, outward) >= 0) { T.Add(a); T.Add(b); T.Add(c); } else { T.Add(a); T.Add(c); T.Add(b); }
            }
            public Mesh Build() { var m = new Mesh { vertices = V.ToArray(), normals = N.ToArray(), uv = U.ToArray() }; m.triangles = T.ToArray(); return m; }
        }
        private static Mesh Cube()
        {
            var b = new Builder();
            Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var n in axes)
            {
                Vector3 t = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up, s = Vector3.Cross(n, t);
                int i0 = b.V.Count;
                for (int k = 0; k < 4; k++)
                {
                    float a = (k == 1 || k == 2) ? 0.5f : -0.5f, c = k >= 2 ? 0.5f : -0.5f;
                    b.Add(n * 0.5f + s * a + t * c, n, new Vector2(a + 0.5f, c + 0.5f));
                }
                b.Tri(i0, i0 + 1, i0 + 2, n); b.Tri(i0, i0 + 2, i0 + 3, n);
            }
            return b.Build();
        }
        // Sphere (half = 0) or capsule (half = 0.5: hemispheres moved apart by the straight section).
        private static Mesh Lathe(PrimitiveType type, int segments, int rings, float half, float unused)
        {
            var b = new Builder();
            var grid = new int[rings + 1, segments + 1];
            for (int r = 0; r <= rings; r++)
            {
                float phi = Mathf.PI * r / rings, y = Mathf.Cos(phi) * 0.5f, ring = Mathf.Sin(phi) * 0.5f;
                float offset = half <= 0 ? 0 : (r <= rings / 2 ? half : -half);
                for (int s = 0; s <= segments; s++)
                {
                    float th = 2 * Mathf.PI * s / segments;
                    var n = new Vector3(Mathf.Cos(th) * Mathf.Sin(phi), Mathf.Cos(phi), Mathf.Sin(th) * Mathf.Sin(phi));
                    grid[r, s] = b.Add(new Vector3(Mathf.Cos(th) * ring, y + offset, Mathf.Sin(th) * ring), n, new Vector2((float)s / segments, 1 - (float)r / rings));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segments; s++)
                {
                    int a = grid[r, s], c = grid[r + 1, s], d = grid[r + 1, s + 1], e = grid[r, s + 1];
                    var centre = (b.V[a] + b.V[c] + b.V[d] + b.V[e]) * 0.25f;
                    var outward = new Vector3(centre.x, centre.y - (half <= 0 ? 0 : Mathf.Sign(centre.y) * Mathf.Min(Mathf.Abs(centre.y), half)), centre.z);
                    if (r > 0) b.Tri(a, c, e, outward);
                    if (r < rings - 1) b.Tri(e, c, d, outward);
                }
            return b.Build();
        }
        private static Mesh Cylinder(int segments)
        {
            var b = new Builder();
            for (int s = 0; s < segments; s++)
            {
                float t0 = 2 * Mathf.PI * s / segments, t1 = 2 * Mathf.PI * (s + 1) / segments;
                var n0 = new Vector3(Mathf.Cos(t0), 0, Mathf.Sin(t0)); var n1 = new Vector3(Mathf.Cos(t1), 0, Mathf.Sin(t1));
                int a = b.Add(n0 * 0.5f + Vector3.up, n0, new Vector2((float)s / segments, 1)), c = b.Add(n0 * 0.5f + Vector3.down, n0, new Vector2((float)s / segments, 0));
                int d = b.Add(n1 * 0.5f + Vector3.down, n1, new Vector2((float)(s + 1) / segments, 0)), e = b.Add(n1 * 0.5f + Vector3.up, n1, new Vector2((float)(s + 1) / segments, 1));
                var mid = (n0 + n1) * 0.5f; b.Tri(a, c, d, mid); b.Tri(a, d, e, mid);
                foreach (float y in new[] { 1f, -1f })
                {
                    var n = new Vector3(0, y, 0);
                    int o = b.Add(n, n, new Vector2(0.5f, 0.5f));
                    int p = b.Add(n0 * 0.5f + n, n, new Vector2(0.5f + n0.x * 0.5f, 0.5f + n0.z * 0.5f));
                    int q = b.Add(n1 * 0.5f + n, n, new Vector2(0.5f + n1.x * 0.5f, 0.5f + n1.z * 0.5f));
                    b.Tri(o, p, q, n);
                }
            }
            return b.Build();
        }
        // Plane: 10 × 10 facing up. Quad: 1 × 1 facing -Z (its visible side looks toward the default camera).
        private static Mesh Flat(float size, bool plane)
        {
            var b = new Builder(); var n = plane ? Vector3.up : Vector3.back; float h = size * 0.5f;
            int i0 = b.Add(plane ? new Vector3(-h, 0, -h) : new Vector3(-h, -h, 0), n, new Vector2(0, 0));
            int i1 = b.Add(plane ? new Vector3(h, 0, -h) : new Vector3(h, -h, 0), n, new Vector2(1, 0));
            int i2 = b.Add(plane ? new Vector3(h, 0, h) : new Vector3(h, h, 0), n, new Vector2(1, 1));
            int i3 = b.Add(plane ? new Vector3(-h, 0, h) : new Vector3(-h, h, 0), n, new Vector2(0, 1));
            b.Tri(i0, i1, i2, n); b.Tri(i0, i2, i3, n); return b.Build();
        }
    }
}

namespace Broker.Runtime.Office
{
    // Camera-only stand-in for the real controller (which needs input, cursor and physics).
    public sealed class FirstPersonOfficeController : MonoBehaviour
    {
        public Camera Eye { get; private set; }
        private void Awake()
        {
            var eyes = new GameObject("Player eyes"); eyes.transform.SetParent(transform, false);
            eyes.transform.localPosition = new Vector3(0, 1.65f, 0);
            Eye = eyes.AddComponent<Camera>(); Eye.depth = 10; Eye.fieldOfView = 72; Eye.nearClipPlane = 0.05f; Eye.farClipPlane = 180;
            Eye.clearFlags = CameraClearFlags.SolidColor; Eye.backgroundColor = new Color(0.09f, 0.14f, 0.22f);
        }
        public void SetControl(bool enabled) { }
        public void ResetAt(Vector3 position)
        {
            transform.SetPositionAndRotation(position, Quaternion.identity); Eye.transform.localRotation = Quaternion.identity;
        }
    }
}
