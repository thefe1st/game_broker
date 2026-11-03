// Software still renderer that approximates Unity's built-in forward path in gamma colour space:
// Standard BRDF (Disney diffuse + GGX), trilight ambient, sky reflections, cascaded soft sun shadows,
// up to three point lights, linear fog and the "Skybox/Procedural" shader. It is a composition and palette check, not a lookdev tool.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Broker.Tools.ScenePreview
{
    public struct Affine
    {
        public Vector3 X, Y, Z, T;
        public static Affine TRS(Vector3 t, Quaternion r, Vector3 s) =>
            new Affine { X = r * Vector3.right * s.x, Y = r * Vector3.up * s.y, Z = r * Vector3.forward * s.z, T = t };
        public Vector3 Point(Vector3 p) => X * p.x + Y * p.y + Z * p.z + T;
        public Vector3 Dir(Vector3 d) => X * d.x + Y * d.y + Z * d.z;
        public static Affine operator *(Affine a, Affine b) => new Affine { X = a.Dir(b.X), Y = a.Dir(b.Y), Z = a.Dir(b.Z), T = a.Point(b.T) };
        public Vector3 Normal(Vector3 n)
        {
            Vector3 cx = Vector3.Cross(Y, Z), cy = Vector3.Cross(Z, X), cz = Vector3.Cross(X, Y);
            float det = Vector3.Dot(X, cx);
            return ((cx * n.x + cy * n.y + cz * n.z) / det).normalized;
        }
    }

    public sealed class Tex
    {
        private readonly int[] w, h; private readonly float[][] rgb; private readonly bool point, clamp;
        private static readonly Dictionary<Texture2D, Tex> cache = new Dictionary<Texture2D, Tex>();
        public static Tex From(Texture texture)
        {
            if (!(texture is Texture2D t) || t.Pixels == null) return null;
            if (!cache.TryGetValue(t, out var found)) cache[t] = found = new Tex(t);
            return found;
        }
        private Tex(Texture2D t)
        {
            point = t.filterMode == FilterMode.Point; clamp = t.wrapMode == TextureWrapMode.Clamp;
            var lw = new List<int> { t.width }; var lh = new List<int> { t.height };
            var levels = new List<float[]>();
            var top = new float[t.width * t.height * 3];
            for (int i = 0; i < t.width * t.height; i++) { var c = t.Pixels[i]; top[i * 3] = c.r / 255f; top[i * 3 + 1] = c.g / 255f; top[i * 3 + 2] = c.b / 255f; }
            levels.Add(top);
            while (t.MipChain && (lw[lw.Count - 1] > 1 || lh[lh.Count - 1] > 1))
            {
                int pw = lw[lw.Count - 1], ph = lh[lh.Count - 1], nw = Math.Max(1, pw / 2), nh = Math.Max(1, ph / 2);
                var prev = levels[levels.Count - 1]; var next = new float[nw * nh * 3];
                for (int y = 0; y < nh; y++) for (int x = 0; x < nw; x++) for (int k = 0; k < 3; k++)
                {
                    float sum = 0; int n = 0;
                    for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++)
                    {
                        int sx = Math.Min(pw - 1, x * 2 + dx), sy = Math.Min(ph - 1, y * 2 + dy);
                        sum += prev[(sy * pw + sx) * 3 + k]; n++;
                    }
                    next[(y * nw + x) * 3 + k] = sum / n;
                }
                levels.Add(next); lw.Add(nw); lh.Add(nh);
            }
            w = lw.ToArray(); h = lh.ToArray(); rgb = levels.ToArray();
        }
        public int Width => w[0]; public int Height => h[0];
        private int Wrap(int i, int n) => clamp ? (i < 0 ? 0 : i >= n ? n - 1 : i) : ((i % n) + n) % n;
        public Vector3 Sample(float u, float v, float lod)
        {
            int level = Math.Max(0, Math.Min(rgb.Length - 1, (int)MathF.Floor(lod + 0.5f)));
            int lw = w[level], lh = h[level]; var data = rgb[level];
            if (point)
            {
                int x = Wrap((int)MathF.Floor(u * lw), lw), y = Wrap((int)MathF.Floor(v * lh), lh); int i = (y * lw + x) * 3;
                return new Vector3(data[i], data[i + 1], data[i + 2]);
            }
            float fx = u * lw - 0.5f, fy = v * lh - 0.5f; int x0 = (int)MathF.Floor(fx), y0 = (int)MathF.Floor(fy);
            float tx = fx - x0, ty = fy - y0; var result = Vector3.zero;
            for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++)
            {
                int i = (Wrap(y0 + dy, lh) * lw + Wrap(x0 + dx, lw)) * 3; float k = (dx == 0 ? 1 - tx : tx) * (dy == 0 ? 1 - ty : ty);
                result += new Vector3(data[i], data[i + 1], data[i + 2]) * k;
            }
            return result;
        }
    }

    public sealed class MaterialInfo
    {
        public string Name; public Vector3 Albedo; public Tex Main; public bool Unlit;
        public bool Emissive; public Vector3 Emission; public Tex EmissionMap; public float Smoothness, Metallic;
        public static MaterialInfo From(Material m)
        {
            string shader = m?.shader?.name ?? "Standard";
            var c = m == null ? Color.white : m.color;
            var info = new MaterialInfo { Name = m?.name ?? "(none)", Albedo = new Vector3(c.r, c.g, c.b), Unlit = shader.StartsWith("Unlit") };
            if (m == null) return info;
            info.Main = Tex.From(m.GetTexture("_MainTex"));
            info.Smoothness = m.Floats.TryGetValue("_Glossiness", out var s) ? s : 0.5f;
            info.Metallic = m.Floats.TryGetValue("_Metallic", out var mm) ? mm : 0;
            info.Emissive = m.IsKeywordEnabled("_EMISSION");
            var e = m.GetColor("_EmissionColor"); info.Emission = new Vector3(e.r, e.g, e.b);
            info.EmissionMap = Tex.From(m.GetTexture("_EmissionMap"));
            return info;
        }
    }

    // All renderers flattened into world-space triangle soup.
    public sealed class SceneSoup
    {
        public float[] P, N, UV; public int[] Tri, TriMat; public byte[] Flags; public int Triangles, Vertices;
        public readonly List<MaterialInfo> Materials = new List<MaterialInfo>();
        public const byte Visible = 1, Casts = 2, Receives = 4;
        public Vector3 CasterMin = new Vector3(1e9f, 1e9f, 1e9f), CasterMax = new Vector3(-1e9f, -1e9f, -1e9f);

        public static SceneSoup Collect()
        {
            var soup = new SceneSoup(); var worlds = new Dictionary<Transform, Affine>();
            var materialIndex = new Dictionary<Material, int>();
            var pos = new List<float>(); var nrm = new List<float>(); var uvs = new List<float>();
            var tris = new List<int>(); var mats = new List<int>(); var flags = new List<byte>();
            Affine World(Transform t)
            {
                if (worlds.TryGetValue(t, out var m)) return m;
                var local = Affine.TRS(t.localPosition, t.localRotation, t.localScale);
                m = t.parent == null ? local : World(t.parent) * local; worlds[t] = m; return m;
            }
            foreach (var go in GameObject.All)
            {
                if (!go.activeInHierarchy) continue;
                var renderer = go.GetComponent<MeshRenderer>(); var filter = go.GetComponent<MeshFilter>();
                if (renderer == null || !renderer.enabled || filter == null || filter.sharedMesh == null) continue;
                var mesh = filter.sharedMesh; var world = World(go.transform);
                bool visible = renderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly, casts = renderer.shadowCastingMode != ShadowCastingMode.Off;
                byte flag = (byte)((visible ? Visible : 0) | (casts ? Casts : 0) | (renderer.receiveShadows ? Receives : 0));
                int baseVertex = pos.Count / 3; var v = mesh.vertices; var n = mesh.normals; var uv = mesh.uv;
                for (int i = 0; i < v.Length; i++)
                {
                    var p = world.Point(v[i]); pos.Add(p.x); pos.Add(p.y); pos.Add(p.z);
                    var q = i < n.Length ? world.Normal(n[i]) : Vector3.up; nrm.Add(q.x); nrm.Add(q.y); nrm.Add(q.z);
                    if (i < uv.Length) { uvs.Add(uv[i].x); uvs.Add(uv[i].y); } else { uvs.Add(0); uvs.Add(0); }
                    if (casts)
                    {
                        soup.CasterMin = new Vector3(Math.Min(soup.CasterMin.x, p.x), Math.Min(soup.CasterMin.y, p.y), Math.Min(soup.CasterMin.z, p.z));
                        soup.CasterMax = new Vector3(Math.Max(soup.CasterMax.x, p.x), Math.Max(soup.CasterMax.y, p.y), Math.Max(soup.CasterMax.z, p.z));
                    }
                }
                var materials = renderer.sharedMaterials;
                for (int sub = 0; sub < mesh.subMeshCount && sub < Math.Max(1, materials.Length); sub++)
                {
                    var material = materials.Length == 0 ? null : materials[Math.Min(sub, materials.Length - 1)];
                    int index;
                    if (material == null) index = -1;
                    else if (!materialIndex.TryGetValue(material, out index)) { index = soup.Materials.Count; materialIndex[material] = index; soup.Materials.Add(MaterialInfo.From(material)); }
                    if (index < 0) continue;
                    var t = mesh.GetTriangles(sub);
                    for (int i = 0; i + 2 < t.Length; i += 3)
                    { tris.Add(baseVertex + t[i]); tris.Add(baseVertex + t[i + 1]); tris.Add(baseVertex + t[i + 2]); mats.Add(index); flags.Add(flag); }
                }
            }
            soup.P = pos.ToArray(); soup.N = nrm.ToArray(); soup.UV = uvs.ToArray(); soup.Tri = tris.ToArray(); soup.TriMat = mats.ToArray(); soup.Flags = flags.ToArray();
            soup.Triangles = mats.Count; soup.Vertices = pos.Count / 3; return soup;
        }
        public Vector3 Pos(int v) => new Vector3(P[v * 3], P[v * 3 + 1], P[v * 3 + 2]);
        public Vector3 Nrm(int v) => new Vector3(N[v * 3], N[v * 3 + 1], N[v * 3 + 2]);
    }

    // Unity built-in "Skybox/Procedural", high-quality sun disk, evaluated in gamma colour space.
    public sealed class ProceduralSky
    {
        private const float OuterRadius = 1.025f, OuterRadius2 = OuterRadius * OuterRadius, InnerRadius = 1, InnerRadius2 = 1, CameraHeight = 0.0001f;
        private const float Mie = 0.0010f, SunBrightness = 20, MaxScatter = 50, HdSundisk = 15;
        private const float KmESun = Mie * SunBrightness, Km4PI = Mie * 4 * MathF.PI, Scale = 1 / (OuterRadius - 1), ScaleDepth = 0.25f, ScaleOverScaleDepth = Scale / ScaleDepth;
        private const float MieG = -0.990f, MieG2 = 0.9801f, GroundThreshold = 0.02f;
        private readonly Vector3 invWavelength, ground, lightPos, lightColor; private readonly float kr4PI, krESun, exposure, sunSize, convergence;
        public readonly bool Valid;
        public ProceduralSky(Material sky, Light sun)
        {
            Valid = sky != null && sky.shader != null && sky.shader.name == "Skybox/Procedural";
            float F(string p, float d) => sky != null && sky.Floats.TryGetValue(p, out var v) ? v : d;
            Color C(string p, Color d) => sky != null && sky.Colors.TryGetValue(p, out var v) ? v : d;
            float thickness = F("_AtmosphereThickness", 1); exposure = F("_Exposure", 1.3f); sunSize = F("_SunSize", 0.04f); convergence = F("_SunSizeConvergence", 5);
            var tint = C("_SkyTint", new Color(0.5f, 0.5f, 0.5f)); var g = C("_GroundColor", new Color(0.369f, 0.349f, 0.341f));
            ground = new Vector3(g.r * g.r, g.g * g.g, g.b * g.b);
            float rayleigh = 0.0025f * MathF.Pow(thickness, 2.5f);
            kr4PI = rayleigh * 4 * MathF.PI; krESun = rayleigh * SunBrightness;
            Vector3 wl = new Vector3(0.65f - 0.15f + 0.3f * (1 - tint.r), 0.57f - 0.15f + 0.3f * (1 - tint.g), 0.475f - 0.15f + 0.3f * (1 - tint.b));
            invWavelength = new Vector3(1 / MathF.Pow(wl.x, 4), 1 / MathF.Pow(wl.y, 4), 1 / MathF.Pow(wl.z, 4));
            if (sun != null)
            {
                lightPos = -(sun.transform.rotation * Vector3.forward).normalized;
                lightColor = new Vector3(sun.color.r, sun.color.g, sun.color.b) * sun.intensity;
            }
            else { lightPos = new Vector3(0, 1, 0); lightColor = Vector3.zero; }
        }
        private static float ScaleFn(float inCos) { float x = 1 - inCos; return 0.25f * MathF.Exp(-0.00287f + x * (0.459f + x * (3.83f + x * (-6.80f + x * 5.25f)))); }
        private Vector3 Attenuate(float scatter)
        {
            float s = Math.Clamp(scatter, 0, MaxScatter);
            return new Vector3(MathF.Exp(-s * (invWavelength.x * kr4PI + Km4PI)), MathF.Exp(-s * (invWavelength.y * kr4PI + Km4PI)), MathF.Exp(-s * (invWavelength.z * kr4PI + Km4PI)));
        }
        public Vector3 Eval(Vector3 eyeRay)
        {
            eyeRay = eyeRay.normalized; var cameraPos = new Vector3(0, InnerRadius + CameraHeight, 0);
            Vector3 cIn, cOut;
            if (eyeRay.y >= 0)
            {
                float far = MathF.Sqrt(OuterRadius2 + InnerRadius2 * eyeRay.y * eyeRay.y - InnerRadius2) - InnerRadius * eyeRay.y;
                float height = InnerRadius + CameraHeight, depth = MathF.Exp(ScaleOverScaleDepth * -CameraHeight);
                float startAngle = Vector3.Dot(eyeRay, cameraPos) / height, startOffset = depth * ScaleFn(startAngle);
                float sampleLength = far / 2, scaledLength = sampleLength * Scale; var sampleRay = eyeRay * sampleLength; var samplePoint = cameraPos + sampleRay * 0.5f;
                var front = Vector3.zero;
                for (int i = 0; i < 2; i++)
                {
                    float h = samplePoint.magnitude, d = MathF.Exp(ScaleOverScaleDepth * (InnerRadius - h));
                    float lightAngle = Vector3.Dot(lightPos, samplePoint) / h, cameraAngle = Vector3.Dot(eyeRay, samplePoint) / h;
                    front += Attenuate(startOffset + d * (ScaleFn(lightAngle) - ScaleFn(cameraAngle))) * (d * scaledLength);
                    samplePoint += sampleRay;
                }
                cIn = Vector3.Scale(front, invWavelength * krESun); cOut = front * KmESun;
            }
            else
            {
                float far = -CameraHeight / MathF.Min(-0.001f, eyeRay.y); var pos = cameraPos + eyeRay * far;
                float depth = MathF.Exp(-CameraHeight * (1 / ScaleDepth));
                float cameraAngle = Vector3.Dot(-eyeRay, pos), lightAngle = Vector3.Dot(lightPos, pos);
                float cameraScale = ScaleFn(cameraAngle), lightScale = ScaleFn(lightAngle), cameraOffset = depth * cameraScale, temp = lightScale + cameraScale;
                float sampleLength = far / 2, scaledLength = sampleLength * Scale; var sampleRay = eyeRay * sampleLength; var samplePoint = cameraPos + sampleRay * 0.5f;
                float h = samplePoint.magnitude, d = MathF.Exp(ScaleOverScaleDepth * (InnerRadius - h));
                var attenuate = Attenuate(d * temp - cameraOffset);
                var front = attenuate * (d * scaledLength);
                cIn = Vector3.Scale(front, invWavelength * krESun + Vector3.one * KmESun);
                cOut = new Vector3(Math.Clamp(attenuate.x, 0, 1), Math.Clamp(attenuate.y, 0, 1), Math.Clamp(attenuate.z, 0, 1));
            }
            float eyeCos = Vector3.Dot(lightPos, -eyeRay);
            var groundColor = (cIn + Vector3.Scale(ground, cOut)) * exposure;
            var skyColor = cIn * (exposure * (0.75f + 0.75f * eyeCos * eyeCos));
            float intensity = Math.Clamp(lightColor.magnitude, 0.25f, 1);
            var sunColor = Vector3.Scale(new Vector3(Math.Clamp(cOut.x, 0, 1), Math.Clamp(cOut.y, 0, 1), Math.Clamp(cOut.z, 0, 1)), lightColor) * (HdSundisk / intensity);
            float y = -eyeRay.y / GroundThreshold;
            var col = Vector3.Lerp(skyColor, groundColor, y);
            if (y < 0)
            {
                float focused = MathF.Pow(Math.Clamp(Vector3.Dot(lightPos, eyeRay), 0, 1), convergence);
                float ec = -focused, ec2 = focused * focused;
                float temp = 1 + MieG2 - 2 * MieG * ec; temp = MathF.Pow(temp, MathF.Pow(sunSize, 0.65f) * 10); temp = MathF.Max(temp, 1e-4f);
                float mie = 1.5f * ((1 - MieG2) / (2 + MieG2)) * (1 + ec2) / temp;
                col += sunColor * mie;
            }
            return new Vector3(MathF.Sqrt(MathF.Max(0, col.x)), MathF.Sqrt(MathF.Max(0, col.y)), MathF.Sqrt(MathF.Max(0, col.z)));
        }
    }

    // Lat-long environment, pre-convolved for a few roughness levels (stand-in for the skybox reflection probe).
    public sealed class Environment
    {
        private const int W = 64, H = 32;
        private static readonly float[] Roughness = { 0f, 0.5f, 0.7f, 0.85f, 1f };
        private readonly Vector3[][] levels;
        private readonly Func<Vector3, Vector3> sharp;
        public Environment(Func<Vector3, Vector3> sky)
        {
            sharp = sky;
            var dirs = new Vector3[W * H]; var src = new Vector3[W * H]; var area = new float[W * H];
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                float theta = (y + 0.5f) / H * MathF.PI, phi = (x + 0.5f) / W * 2 * MathF.PI - MathF.PI;
                var d = new Vector3(MathF.Sin(theta) * MathF.Cos(phi), MathF.Cos(theta), MathF.Sin(theta) * MathF.Sin(phi));
                dirs[y * W + x] = d; src[y * W + x] = sky(d); area[y * W + x] = MathF.Sin(theta);
            }
            levels = new Vector3[Roughness.Length][];
            levels[0] = src;
            for (int l = 1; l < Roughness.Length; l++)
            {
                float a = Roughness[l] * Roughness[l], power = MathF.Max(0, 2 / (a * a) - 2);
                var dst = new Vector3[W * H];
                Parallel.For(0, W * H, i =>
                {
                    var sum = Vector3.zero; float weight = 0;
                    for (int j = 0; j < W * H; j++)
                    {
                        float c = Vector3.Dot(dirs[i], dirs[j]); if (c <= 0) continue;
                        float k = MathF.Pow(c, power) * area[j]; sum += src[j] * k; weight += k;
                    }
                    dst[i] = weight > 0 ? sum / weight : src[i];
                });
                levels[l] = dst;
            }
        }
        private static Vector3 Lookup(Vector3[] map, Vector3 d)
        {
            float theta = MathF.Acos(Math.Clamp(d.y, -1, 1)), phi = MathF.Atan2(d.z, d.x);
            float fx = (phi + MathF.PI) / (2 * MathF.PI) * W - 0.5f, fy = theta / MathF.PI * H - 0.5f;
            int x0 = (int)MathF.Floor(fx), y0 = (int)MathF.Floor(fy); float tx = fx - x0, ty = fy - y0; var r = Vector3.zero;
            for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++)
            {
                int x = ((x0 + dx) % W + W) % W, y = Math.Clamp(y0 + dy, 0, H - 1);
                r += map[y * W + x] * ((dx == 0 ? 1 - tx : tx) * (dy == 0 ? 1 - ty : ty));
            }
            return r;
        }
        public Vector3 Sample(Vector3 dir, float perceptualRoughness)
        {
            float pr = Math.Clamp(perceptualRoughness, 0, 1);
            int hi = 1; while (hi < Roughness.Length - 1 && Roughness[hi] < pr) hi++;
            int lo = hi - 1; float t = (pr - Roughness[lo]) / (Roughness[hi] - Roughness[lo]);
            var a = lo == 0 ? sharp(dir) : Lookup(levels[lo], dir); var b = Lookup(levels[hi], dir);
            return Vector3.Lerp(a, b, t);
        }
    }

    public sealed class ViewCamera
    {
        public Vector3 Position, Right, Up, Forward; public float Fov = 60, Near = 0.3f, Far = 1000; public bool Skybox = true; public Color Background;
        public bool Orthographic; public float OrthoSize = 5;
        public static ViewCamera FromYawPitch(Vector3 position, float yaw, float pitch, float fov, float near, float far)
        {
            var r = Quaternion.Euler(pitch, yaw, 0);
            return new ViewCamera { Position = position, Right = r * Vector3.right, Up = r * Vector3.up, Forward = r * Vector3.forward, Fov = fov, Near = near, Far = far };
        }
        public static ViewCamera From(Camera camera)
        {
            var r = camera.transform.rotation;
            return new ViewCamera
            {
                Position = camera.transform.position, Right = r * Vector3.right, Up = r * Vector3.up, Forward = r * Vector3.forward, Fov = camera.fieldOfView,
                Near = camera.nearClipPlane, Far = camera.farClipPlane, Skybox = camera.clearFlags == CameraClearFlags.Skybox, Background = camera.backgroundColor,
                Orthographic = camera.orthographic, OrthoSize = camera.orthographicSize
            };
        }
    }

    public sealed class ShadowCascade
    {
        public Vector3 Center, Lx, Ly, Lz; public float Radius, Split; public int Res; public float[] Depth;
        public float Texel => 2 * Radius / Res;
    }

    public sealed class PointLightInfo { public Vector3 Position, Color; public float Range; }

    public sealed class Renderer
    {
        public int Width = 1440, Height = 960, Supersample = 2;
        public float[] Rgb; public float[] Depth; public int[] Id; public int W, H;
        private SceneSoup soup; private ViewCamera cam; private ProceduralSky sky; private Environment env;
        private Vector3 sunDir, sunColor; private bool hasSun; private float shadowStrength = 1;
        private readonly List<ShadowCascade> cascades = new List<ShadowCascade>();
        private readonly List<PointLightInfo> points = new List<PointLightInfo>();
        private Vector3 ambientSky, ambientEquator, ambientGround; private bool fog; private float fogStart, fogEnd; private Vector3 fogColor;
        private float shadowDistance; private float fx, fy;
        public string Stats = "";

        public void Render(SceneSoup scene, ViewCamera camera)
        {
            soup = scene; cam = camera; W = Width * Supersample; H = Height * Supersample;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            SetupLights();
            sky = new ProceduralSky(RenderSettings.skybox, RenderSettings.sun);
            env = new Environment(d => sky.Valid ? sky.Eval(d) : new Vector3(0.5f, 0.5f, 0.5f));
            float tEnv = clock.ElapsedMilliseconds;
            BuildShadows();
            float tShadow = clock.ElapsedMilliseconds;
            Rasterize();
            float tRaster = clock.ElapsedMilliseconds;
            Shade();
            Stats = $"{soup.Triangles} tris, env {tEnv:0}ms, shadows {tShadow - tEnv:0}ms, raster {tRaster - tShadow:0}ms, shade {clock.ElapsedMilliseconds - tRaster:0}ms";
        }

        private void SetupLights()
        {
            points.Clear(); hasSun = false;
            foreach (var go in GameObject.All)
            {
                if (!go.activeInHierarchy) continue;
                var light = go.GetComponent<Light>(); if (light == null || !light.enabled) continue;
                var color = new Vector3(light.color.r, light.color.g, light.color.b) * light.intensity;
                if (light.type == LightType.Directional && !hasSun)
                { hasSun = true; sunDir = (go.transform.rotation * Vector3.forward).normalized; sunColor = color; shadowStrength = light.shadows == LightShadows.None ? 0 : light.shadowStrength; }
                else if (light.type == LightType.Point) points.Add(new PointLightInfo { Position = go.transform.position, Color = color, Range = light.range });
            }
            var mode = RenderSettings.ambientMode;
            Vector3 V(Color c) => new Vector3(c.r, c.g, c.b);
            ambientSky = V(RenderSettings.ambientSkyColor);
            ambientEquator = mode == AmbientMode.Trilight ? V(RenderSettings.ambientEquatorColor) : ambientSky;
            ambientGround = mode == AmbientMode.Trilight ? V(RenderSettings.ambientGroundColor) : ambientSky;
            fog = RenderSettings.fog && RenderSettings.fogMode == FogMode.Linear; fogStart = RenderSettings.fogStartDistance; fogEnd = RenderSettings.fogEndDistance;
            fogColor = V(RenderSettings.fogColor);
            shadowDistance = Math.Min(QualitySettings.shadowDistance, cam.Far);
        }

        // Four stable-fit cascades (Unity's default 6.7% / 20% / 46.7% splits) of 2048² each, like the Ultra preset.
        private void BuildShadows()
        {
            cascades.Clear();
            if (!hasSun || shadowStrength <= 0) return;
            float[] splits = { 0, 0.067f, 0.2f, 0.467f, 1 };
            var lz = sunDir; var lx = Vector3.Cross(Vector3.up, lz).normalized; var ly = Vector3.Cross(lz, lx);
            float tanY = MathF.Tan(cam.Fov * 0.5f * Mathf.Deg2Rad), tanX = tanY * Width / Height;
            for (int c = 0; c < 4; c++)
            {
                float n = MathF.Max(cam.Near, splits[c] * shadowDistance), f = splits[c + 1] * shadowDistance;
                var corners = new List<Vector3>();
                foreach (float d in new[] { n, f }) foreach (float sx in new[] { -1f, 1f }) foreach (float sy in new[] { -1f, 1f })
                    corners.Add(cam.Position + cam.Forward * d + cam.Right * (sx * tanX * d) + cam.Up * (sy * tanY * d));
                var centre = Vector3.zero; foreach (var p in corners) centre += p; centre /= corners.Count;
                float radius = corners.Max(p => (p - centre).magnitude) * 1.02f;
                var cascade = new ShadowCascade { Center = centre, Lx = lx, Ly = ly, Lz = lz, Radius = radius, Split = f, Res = 2048 };
                // Snap the centre to texels like Unity's stable fit.
                float texel = cascade.Texel; float cx = MathF.Round(Vector3.Dot(centre, lx) / texel) * texel, cy = MathF.Round(Vector3.Dot(centre, ly) / texel) * texel;
                cascade.Center = lx * cx + ly * cy + lz * Vector3.Dot(centre, lz);
                cascade.Depth = new float[cascade.Res * cascade.Res]; Array.Fill(cascade.Depth, float.MaxValue);
                RasterShadow(cascade); cascades.Add(cascade);
            }
        }

        private void RasterShadow(ShadowCascade c)
        {
            int res = c.Res; float scale = res / (2 * c.Radius), half = res * 0.5f;
            float cx = Vector3.Dot(c.Center, c.Lx), cy = Vector3.Dot(c.Center, c.Ly);
            var P = soup.P; var depth = c.Depth;
            Span<float> X = stackalloc float[3], Y = stackalloc float[3], Z = stackalloc float[3];
            for (int t = 0; t < soup.Triangles; t++)
            {
                if ((soup.Flags[t] & SceneSoup.Casts) == 0) continue;
                for (int k = 0; k < 3; k++)
                {
                    int v = soup.Tri[t * 3 + k] * 3; float px = P[v], py = P[v + 1], pz = P[v + 2];
                    X[k] = (px * c.Lx.x + py * c.Lx.y + pz * c.Lx.z - cx) * scale + half;
                    Y[k] = half - (px * c.Ly.x + py * c.Ly.y + pz * c.Ly.z - cy) * scale;
                    Z[k] = px * c.Lz.x + py * c.Lz.y + pz * c.Lz.z;
                }
                FillTriangle(X, Y, Z, res, res, (i, z) => { if (z < depth[i]) depth[i] = z; }, false);
            }
        }

        // Rasterises a screen triangle; perspective = interpolate 1/z (Z holds 1/z) and keep the greatest.
        private static void FillTriangle(Span<float> X, Span<float> Y, Span<float> Z, int width, int height, Action<int, float> write, bool unused)
        {
            float area = (X[1] - X[0]) * (Y[2] - Y[0]) - (X[2] - X[0]) * (Y[1] - Y[0]);
            if (MathF.Abs(area) < 1e-12f) return;
            int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(X[0], MathF.Min(X[1], X[2])))), maxX = Math.Min(width - 1, (int)MathF.Ceiling(MathF.Max(X[0], MathF.Max(X[1], X[2]))));
            int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(Y[0], MathF.Min(Y[1], Y[2])))), maxY = Math.Min(height - 1, (int)MathF.Ceiling(MathF.Max(Y[0], MathF.Max(Y[1], Y[2]))));
            if (minX > maxX || minY > maxY) return;
            float inv = 1 / area;
            for (int y = minY; y <= maxY; y++)
            {
                float py = y + 0.5f;
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + 0.5f;
                    float w0 = ((X[1] - px) * (Y[2] - py) - (X[2] - px) * (Y[1] - py)) * inv;
                    float w1 = ((X[2] - px) * (Y[0] - py) - (X[0] - px) * (Y[2] - py)) * inv;
                    float w2 = 1 - w0 - w1;
                    if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                    write(y * width + x, w0 * Z[0] + w1 * Z[1] + w2 * Z[2]);
                }
            }
        }

        private void Rasterize()
        {
            Depth = new float[W * H]; Id = new int[W * H]; Array.Fill(Id, -1);
            float tanY = MathF.Tan(cam.Fov * 0.5f * Mathf.Deg2Rad);
            fy = H * 0.5f / tanY; fx = fy;
            float invFar = 1 / cam.Far, near = cam.Near; var P = soup.P; var depth = Depth; var id = Id;
            var o = cam.Position; Vector3 R = cam.Right, U = cam.Up, F = cam.Forward;
            float halfW = W * 0.5f, halfH = H * 0.5f, limitX = halfW / fx * 1.05f, limitY = halfH / fy * 1.05f;
            Span<float> vx = stackalloc float[3], vy = stackalloc float[3], vz = stackalloc float[3];
            Span<float> cxs = stackalloc float[8], cys = stackalloc float[8], czs = stackalloc float[8];
            Span<float> X = stackalloc float[3], Y = stackalloc float[3], Z = stackalloc float[3];
            for (int t = 0; t < soup.Triangles; t++)
            {
                if ((soup.Flags[t] & SceneSoup.Visible) == 0) continue;
                int a = soup.Tri[t * 3] * 3, b = soup.Tri[t * 3 + 1] * 3, c = soup.Tri[t * 3 + 2] * 3;
                float e1x = P[b] - P[a], e1y = P[b + 1] - P[a + 1], e1z = P[b + 2] - P[a + 2];
                float e2x = P[c] - P[a], e2y = P[c + 1] - P[a + 1], e2z = P[c + 2] - P[a + 2];
                float nx = e1y * e2z - e1z * e2y, ny = e1z * e2x - e1x * e2z, nz = e1x * e2y - e1y * e2x;
                if (nx * (o.x - P[a]) + ny * (o.y - P[a + 1]) + nz * (o.z - P[a + 2]) <= 0) continue;
                bool allNear = true, allFar = true, allL = true, allR = true, allT = true, allB = true;
                for (int k = 0; k < 3; k++)
                {
                    int v = k == 0 ? a : k == 1 ? b : c;
                    float dx = P[v] - o.x, dy = P[v + 1] - o.y, dz = P[v + 2] - o.z;
                    vx[k] = dx * R.x + dy * R.y + dz * R.z; vy[k] = dx * U.x + dy * U.y + dz * U.z; vz[k] = dx * F.x + dy * F.y + dz * F.z;
                    allNear &= vz[k] < near; allFar &= vz[k] > cam.Far;
                    allL &= vx[k] < -limitX * vz[k]; allR &= vx[k] > limitX * vz[k]; allB &= vy[k] < -limitY * vz[k]; allT &= vy[k] > limitY * vz[k];
                }
                if (allNear || allFar || (allL || allR || allB || allT) && vz[0] > near && vz[1] > near && vz[2] > near) continue;
                // Clip against the near plane.
                int count = 0;
                for (int k = 0; k < 3; k++)
                {
                    int n = (k + 1) % 3; bool inK = vz[k] >= near, inN = vz[n] >= near;
                    if (inK) { cxs[count] = vx[k]; cys[count] = vy[k]; czs[count] = vz[k]; count++; }
                    if (inK != inN)
                    {
                        float s = (near - vz[k]) / (vz[n] - vz[k]);
                        cxs[count] = vx[k] + (vx[n] - vx[k]) * s; cys[count] = vy[k] + (vy[n] - vy[k]) * s; czs[count] = near; count++;
                    }
                }
                for (int k = 1; k + 1 < count; k++)
                {
                    int i0 = 0, i1 = k, i2 = k + 1;
                    X[0] = halfW + cxs[i0] / czs[i0] * fx; Y[0] = halfH - cys[i0] / czs[i0] * fy; Z[0] = 1 / czs[i0];
                    X[1] = halfW + cxs[i1] / czs[i1] * fx; Y[1] = halfH - cys[i1] / czs[i1] * fy; Z[1] = 1 / czs[i1];
                    X[2] = halfW + cxs[i2] / czs[i2] * fx; Y[2] = halfH - cys[i2] / czs[i2] * fy; Z[2] = 1 / czs[i2];
                    int tri = t;
                    FillTriangle(X, Y, Z, W, H, (i, iz) => { if (iz > depth[i] && iz >= invFar) { depth[i] = iz; id[i] = tri; } }, true);
                }
            }
        }

        private float Shadow(Vector3 p, Vector3 n, float viewZ, float distance)
        {
            if (cascades.Count == 0 || viewZ >= shadowDistance) return 1;
            int ci = 0; while (ci < cascades.Count - 1 && viewZ > cascades[ci].Split) ci++;
            var c = cascades[ci]; float texel = c.Texel;
            float nl = Vector3.Dot(n, -sunDir);
            var q = p + n * (texel * 1.4f * (1.2f - MathF.Max(0, nl) * 0.6f));
            float scale = c.Res / (2 * c.Radius), half = c.Res * 0.5f;
            float u = (Vector3.Dot(q, c.Lx) - Vector3.Dot(c.Center, c.Lx)) * scale + half - 0.5f;
            float v = half - (Vector3.Dot(q, c.Ly) - Vector3.Dot(c.Center, c.Ly)) * scale - 0.5f;
            float d = Vector3.Dot(q, c.Lz) - texel * 0.6f - 0.01f;
            int x0 = (int)MathF.Floor(u), y0 = (int)MathF.Floor(v); float tx = u - x0, ty = v - y0;
            // 3×3 tent: bilinear PCF over a 4×4 texel footprint.
            float lit = 0, total = 0;
            for (int dy = -1; dy <= 2; dy++)
            {
                float wy = dy == -1 ? 1 - ty : dy == 2 ? ty : 1;
                for (int dx = -1; dx <= 2; dx++)
                {
                    float wx = dx == -1 ? 1 - tx : dx == 2 ? tx : 1;
                    int sx = x0 + dx, sy = y0 + dy; float k = wx * wy; total += k;
                    if (sx < 0 || sy < 0 || sx >= c.Res || sy >= c.Res) { lit += k; continue; }
                    if (d <= c.Depth[sy * c.Res + sx]) lit += k;
                }
            }
            float visible = lit / total;
            float fade = Math.Clamp((distance - shadowDistance * 0.9f) / (shadowDistance * 0.1f), 0, 1);
            visible = visible + (1 - visible) * fade;
            return 1 - shadowStrength + shadowStrength * visible;
        }

        private static float Pow5(float x) { float x2 = x * x; return x2 * x2 * x; }
        // Unity BRDF1 (Standard shader), gamma colour space.
        private static Vector3 Brdf(Vector3 diffColor, Vector3 specColor, float oneMinusReflectivity, float smoothness, Vector3 n, Vector3 v,
            Vector3 l, Vector3 lightColor, Vector3 giDiffuse, Vector3 giSpecular)
        {
            float pr = 1 - smoothness; var hv = (l + v).normalized;
            float nv = MathF.Abs(Vector3.Dot(n, v)), nl = Math.Clamp(Vector3.Dot(n, l), 0, 1), nh = Math.Clamp(Vector3.Dot(n, hv), 0, 1), lh = Math.Clamp(Vector3.Dot(l, hv), 0, 1);
            float fd90 = 0.5f + 2 * lh * lh * pr;
            float diffuseTerm = (1 + (fd90 - 1) * Pow5(1 - nl)) * (1 + (fd90 - 1) * Pow5(1 - nv)) * nl;
            float roughness = MathF.Max(pr * pr, 0.002f);
            float lambdaV = nl * (nv * (1 - roughness) + roughness), lambdaL = nv * (nl * (1 - roughness) + roughness);
            float vis = 0.5f / (lambdaV + lambdaL + 1e-5f);
            float a2 = roughness * roughness, dd = (nh * a2 - nh) * nh + 1, D = a2 / (MathF.PI * (dd * dd + 1e-7f));
            float spec = MathF.Sqrt(MathF.Max(1e-4f, vis * D * MathF.PI));
            spec = MathF.Max(0, spec * nl);
            float surfaceReduction = 1 - 0.28f * roughness * pr;
            float grazing = Math.Clamp(smoothness + (1 - oneMinusReflectivity), 0, 1);
            float f5 = Pow5(1 - lh), fv = Pow5(1 - nv);
            var fresnel = specColor + (Vector3.one - specColor) * f5;
            var fresnelLerp = specColor + (Vector3.one * grazing - specColor) * fv;
            return Vector3.Scale(diffColor, giDiffuse + lightColor * diffuseTerm) + Vector3.Scale(lightColor * spec, fresnel)
                + Vector3.Scale(giSpecular * surfaceReduction, fresnelLerp);
        }

        private Vector3 Ambient(Vector3 n)
        {
            float y = n.y;
            return y >= 0 ? Vector3.Lerp(ambientEquator, ambientSky, y) : Vector3.Lerp(ambientEquator, ambientGround, -y);
        }

        private Vector3 Background(Vector3 dir) => cam.Skybox && sky.Valid ? sky.Eval(dir) : new Vector3(cam.Background.r, cam.Background.g, cam.Background.b);

        private void Shade()
        {
            Rgb = new float[Width * Height * 3];
            var samples = new Vector3[W * H];
            float halfW = W * 0.5f, halfH = H * 0.5f; var o = cam.Position;
            const float dielectric = 0.220916301f;
            Parallel.For(0, H, y =>
            {
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    var dir = cam.Forward + cam.Right * ((x + 0.5f - halfW) / fx) + cam.Up * (-(y + 0.5f - halfH) / fy);
                    int t = Id[i];
                    if (t < 0) { samples[i] = Background(dir); continue; }
                    int ia = soup.Tri[t * 3], ib = soup.Tri[t * 3 + 1], ic = soup.Tri[t * 3 + 2];
                    Vector3 A = soup.Pos(ia), B = soup.Pos(ib), C = soup.Pos(ic);
                    Vector3 e1 = B - A, e2 = C - A, tv = o - A;
                    Bary(dir, e1, e2, tv, out float bu, out float bv, out float depth);
                    var p = o + dir * depth;
                    var n = (soup.Nrm(ia) * (1 - bu - bv) + soup.Nrm(ib) * bu + soup.Nrm(ic) * bv).normalized;
                    var mat = soup.Materials[soup.TriMat[t]];
                    float u0 = soup.UV[ia * 2] * (1 - bu - bv) + soup.UV[ib * 2] * bu + soup.UV[ic * 2] * bv;
                    float v0 = soup.UV[ia * 2 + 1] * (1 - bu - bv) + soup.UV[ib * 2 + 1] * bu + soup.UV[ic * 2 + 1] * bv;
                    var albedo = mat.Albedo; float lod = 0;
                    if (mat.Main != null || mat.EmissionMap != null)
                    {
                        // Footprint of one output pixel (Supersample samples) for mip selection.
                        float step = Supersample;
                        Bary(dir + cam.Right * (step / fx), e1, e2, tv, out float xu, out float xv, out _);
                        Bary(dir - cam.Up * (step / fy), e1, e2, tv, out float yu, out float yv, out _);
                        float ux = soup.UV[ia * 2] * (1 - xu - xv) + soup.UV[ib * 2] * xu + soup.UV[ic * 2] * xv - u0;
                        float vx = soup.UV[ia * 2 + 1] * (1 - xu - xv) + soup.UV[ib * 2 + 1] * xu + soup.UV[ic * 2 + 1] * xv - v0;
                        float uy = soup.UV[ia * 2] * (1 - yu - yv) + soup.UV[ib * 2] * yu + soup.UV[ic * 2] * yv - u0;
                        float vy = soup.UV[ia * 2 + 1] * (1 - yu - yv) + soup.UV[ib * 2 + 1] * yu + soup.UV[ic * 2 + 1] * yv - v0;
                        var tex = mat.Main ?? mat.EmissionMap; float tw = tex.Width, th = tex.Height;
                        float fpx = MathF.Sqrt(ux * ux * tw * tw + vx * vx * th * th), fpy = MathF.Sqrt(uy * uy * tw * tw + vy * vy * th * th);
                        float fp = MathF.Max(fpx, fpy); lod = fp > 1e-6f && !float.IsNaN(fp) ? MathF.Log2(fp) : 0;
                    }
                    if (mat.Main != null) albedo = Vector3.Scale(albedo, mat.Main.Sample(u0, v0, lod));
                    var viewDir = (-dir).normalized; float viewZ = depth; float distance = (p - o).magnitude;
                    Vector3 color;
                    if (mat.Unlit) color = albedo;
                    else
                    {
                        var specColor = Vector3.Lerp(Vector3.one * dielectric, albedo, mat.Metallic);
                        float omr = (1 - dielectric) * (1 - mat.Metallic); var diff = albedo * omr;
                        var refl = dir.normalized - n * (2 * Vector3.Dot(dir.normalized, n));
                        var giSpec = env.Sample(refl, 1 - mat.Smoothness);
                        Vector3 light = Vector3.zero, l = Vector3.up;
                        if (hasSun)
                        {
                            l = -sunDir; float shadow = Vector3.Dot(n, l) > 0 ? Shadow(p, n, viewZ, distance) : 1;
                            light = sunColor * shadow;
                        }
                        color = Brdf(diff, specColor, omr, mat.Smoothness, n, viewDir, l, light, Ambient(n), giSpec);
                        if (mat.Emissive)
                        {
                            var e = mat.Emission; if (mat.EmissionMap != null) e = Vector3.Scale(e, mat.EmissionMap.Sample(u0, v0, lod));
                            color += e;
                        }
                        float fogFactor = fog ? Math.Clamp((fogEnd - viewZ) / (fogEnd - fogStart), 0, 1) : 1;
                        if (fog) color = Vector3.Lerp(fogColor, color, fogFactor);
                        foreach (var pl in points)
                        {
                            var toLight = pl.Position - p; float d2 = toLight.sqrMagnitude / (pl.Range * pl.Range);
                            if (d2 >= 1) continue;
                            float atten = 1 / (1 + 25 * d2); if (d2 >= 0.64f) atten *= 1 - (d2 - 0.64f) / 0.36f;
                            color += Brdf(diff, specColor, omr, mat.Smoothness, n, viewDir, toLight.normalized, pl.Color * atten, Vector3.zero, Vector3.zero) * fogFactor;
                        }
                    }
                    if (mat.Unlit && fog) color = Vector3.Lerp(fogColor, color, Math.Clamp((fogEnd - viewZ) / (fogEnd - fogStart), 0, 1));
                    samples[i] = color;
                }
            });
            for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
            {
                var sum = Vector3.zero;
                for (int sy = 0; sy < Supersample; sy++) for (int sx = 0; sx < Supersample; sx++)
                {
                    var s = samples[(y * Supersample + sy) * W + x * Supersample + sx];
                    sum += new Vector3(Math.Clamp(s.x, 0, 1), Math.Clamp(s.y, 0, 1), Math.Clamp(s.z, 0, 1));
                }
                sum /= Supersample * Supersample; int o3 = (y * Width + x) * 3;
                Rgb[o3] = sum.x; Rgb[o3 + 1] = sum.y; Rgb[o3 + 2] = sum.z;
            }
        }

        // Share of samples that hit geometry, and the 5th–95th percentile luminance range of the final image.
        public float Coverage() => Id.Count(i => i >= 0) / (float)Id.Length;
        public float LuminanceSpread()
        {
            var lum = new float[Width * Height];
            for (int i = 0; i < lum.Length; i++) lum[i] = 0.3f * Rgb[i * 3] + 0.59f * Rgb[i * 3 + 1] + 0.11f * Rgb[i * 3 + 2];
            Array.Sort(lum); return lum[(int)(lum.Length * 0.95f)] - lum[(int)(lum.Length * 0.05f)];
        }

        // Möller–Trumbore barycentrics (u, v) and ray parameter for an infinite-plane hit; dir is not normalised (t = view depth).
        private static void Bary(Vector3 dir, Vector3 e1, Vector3 e2, Vector3 tv, out float u, out float v, out float t)
        {
            var pv = Vector3.Cross(dir, e2); float det = Vector3.Dot(e1, pv);
            if (MathF.Abs(det) < 1e-20f) { u = v = 0; t = 0; return; }
            float inv = 1 / det; u = Vector3.Dot(tv, pv) * inv; var qv = Vector3.Cross(tv, e1); v = Vector3.Dot(dir, qv) * inv; t = Vector3.Dot(e2, qv) * inv;
        }

        // Screen position (output pixels) and view depth of a world point; false when behind the camera.
        public bool Project(Vector3 world, out float sx, out float sy, out float depth)
        {
            var d = world - cam.Position; float z = Vector3.Dot(d, cam.Forward); depth = z; sx = sy = 0;
            if (z <= cam.Near) return false;
            sx = (W * 0.5f + Vector3.Dot(d, cam.Right) / z * fx) / Supersample; sy = (H * 0.5f - Vector3.Dot(d, cam.Up) / z * fy) / Supersample;
            return true;
        }
        // Depth (view z) stored at an output pixel centre, or +inf for sky.
        public float DepthAt(float sx, float sy)
        {
            int x = Math.Clamp((int)(sx * Supersample), 0, W - 1), y = Math.Clamp((int)(sy * Supersample), 0, H - 1);
            float iz = Depth[y * W + x]; return iz > 0 ? 1 / iz : float.PositiveInfinity;
        }
        public float FocalPixels => fy / Supersample;

        public void SavePpm(string path)
        {
            using var file = System.IO.File.Create(path);
            var header = System.Text.Encoding.ASCII.GetBytes($"P6\n{Width} {Height}\n255\n"); file.Write(header, 0, header.Length);
            var bytes = new byte[Width * Height * 3];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)Math.Clamp((int)MathF.Round(Rgb[i] * 255), 0, 255);
            file.Write(bytes, 0, bytes.Length);
        }
    }
}
