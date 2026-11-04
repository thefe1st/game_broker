// Minimal engine stand-ins with the same signatures as the Unity APIs the office scene uses.
// They build a plain object graph that Preview.cs rasterises; nothing here simulates physics or input.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace UnityEngine
{
    public class Object
    {
        public string name { get; set; } = "";
        public HideFlags hideFlags { get; set; }
        public static void Destroy(Object obj)
        {
            if (obj is Transform) return;
            if (obj is Component c && c.gameObject != null) c.gameObject.RemoveComponent(c);
            else if (obj is GameObject g) g.Destroyed = true;
        }
        public static void DestroyImmediate(Object obj) => Destroy(obj);
        public static T Instantiate<T>(T original) where T : Object
        {
            if (original is Mesh mesh) return (T)(Object)mesh.Clone();
            throw new NotSupportedException("Preview stubs only clone meshes.");
        }
        public override string ToString() => name;
    }
    public class Component : Object
    {
        public GameObject gameObject { get; internal set; }
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() => gameObject.GetComponent<T>();
        public T GetComponentInParent<T>()
        {
            for (var t = transform; t != null; t = t.parent) { var found = t.gameObject.GetComponent<T>(); if (found != null) return found; }
            return default;
        }
        public T[] GetComponentsInChildren<T>() => gameObject.GetComponentsInChildren<T>();
    }
    public class Behaviour : Component { public bool enabled { get; set; } = true; public bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy; }
    public class MonoBehaviour : Behaviour { }
    public sealed class GameObject : Object
    {
        public static readonly List<GameObject> All = new List<GameObject>();
        private readonly List<Component> components = new List<Component>();
        public Transform transform { get; }
        public int layer { get; set; }
        public bool activeSelf { get; private set; } = true;
        internal bool Destroyed;
        public GameObject(string name)
        {
            this.name = name; transform = new Transform { gameObject = this }; components.Add(transform); All.Add(this);
        }
        public GameObject() : this("New Game Object") { }
        public bool activeInHierarchy => !Destroyed && activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        public void SetActive(bool value) => activeSelf = value;
        public IReadOnlyList<Component> Components => components;
        public T AddComponent<T>() where T : Component
        {
            var component = (T)Activator.CreateInstance(typeof(T), true);
            component.gameObject = this; components.Add(component);
            if (component is TextMesh && GetComponent<MeshRenderer>() == null) AddComponent<MeshRenderer>();
            if (component is MonoBehaviour)
                typeof(T).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.Invoke(component, null);
            return component;
        }
        public T GetComponent<T>() { foreach (var c in components) if (c is T t) return t; return default; }
        public T[] GetComponentsInChildren<T>()
        {
            var list = new List<T>();
            void Walk(Transform t) { foreach (var c in t.gameObject.components) if (c is T x) list.Add(x); foreach (Transform child in t) Walk(child); }
            Walk(transform); return list.ToArray();
        }
        internal void RemoveComponent(Component c) => components.Remove(c);
        public static GameObject CreatePrimitive(PrimitiveType type)
        {
            var obj = new GameObject(type.ToString());
            obj.AddComponent<MeshFilter>().sharedMesh = PrimitiveMeshes.Get(type);
            obj.AddComponent<MeshRenderer>().sharedMaterial = PrimitiveMeshes.DefaultMaterial;
            if (type == PrimitiveType.Cube) obj.AddComponent<BoxCollider>();
            else if (type == PrimitiveType.Sphere) obj.AddComponent<SphereCollider>();
            else obj.AddComponent<CapsuleCollider>();
            return obj;
        }
    }
    public enum Space { World, Self }
    public class Transform : Component, IEnumerable
    {
        private Transform parentTransform; private readonly List<Transform> children = new List<Transform>();
        public Vector3 localPosition { get; set; } = Vector3.zero;
        public Quaternion localRotation { get; set; } = Quaternion.identity;
        public Vector3 localScale { get; set; } = Vector3.one;
        public Transform parent { get => parentTransform; set => SetParent(value, true); }
        public void SetParent(Transform p) => SetParent(p, true);
        public void SetParent(Transform p, bool worldPositionStays)
        {
            Vector3 wp = position; Quaternion wr = rotation; Vector3 ws = lossyScale;
            parentTransform?.children.Remove(this); parentTransform = p; p?.children.Add(this);
            if (!worldPositionStays) return;
            position = wp; rotation = wr;
            if (p == null) localScale = ws;
            else { var ps = p.lossyScale; localScale = new Vector3(ws.x / ps.x, ws.y / ps.y, ws.z / ps.z); }
        }
        public Vector3 position
        {
            get => parentTransform == null ? localPosition : parentTransform.TransformPoint(localPosition);
            set => localPosition = parentTransform == null ? value : parentTransform.InverseTransformPoint(value);
        }
        public Quaternion rotation
        {
            get => parentTransform == null ? localRotation : parentTransform.rotation * localRotation;
            set => localRotation = parentTransform == null ? value : Quaternion.Inverse(parentTransform.rotation) * value;
        }
        public Vector3 lossyScale => parentTransform == null ? localScale : Vector3.Scale(parentTransform.lossyScale, localScale);
        public Vector3 localEulerAngles { set => localRotation = Quaternion.Euler(value); }
        public Vector3 TransformPoint(Vector3 p)
        {
            var local = localRotation * Vector3.Scale(localScale, p) + localPosition;
            return parentTransform == null ? local : parentTransform.TransformPoint(local);
        }
        public Vector3 InverseTransformPoint(Vector3 p)
        {
            var q = parentTransform == null ? p : parentTransform.InverseTransformPoint(p);
            var r = Quaternion.Inverse(localRotation) * (q - localPosition);
            return new Vector3(r.x / localScale.x, r.y / localScale.y, r.z / localScale.z);
        }
        public Vector3 TransformDirection(Vector3 d) => rotation * d;
        public Vector3 InverseTransformDirection(Vector3 d) => Quaternion.Inverse(rotation) * d;
        public Vector3 forward => rotation * Vector3.forward;
        public Vector3 right => rotation * Vector3.right;
        public Vector3 up => rotation * Vector3.up;
        public void LookAt(Vector3 target) => rotation = Quaternion.LookRotation(target - position, Vector3.up);
        public void SetPositionAndRotation(Vector3 p, Quaternion r) { position = p; rotation = r; }
        public void Rotate(float x, float y, float z, Space space = Space.Self) => localRotation = localRotation * Quaternion.Euler(x, y, z);
        public int childCount => children.Count;
        public Transform GetChild(int i) => children[i];
        public IEnumerator GetEnumerator() => children.ToList().GetEnumerator();
    }

    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
    }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float s) => new Vector2(a.x * s, a.y * s);
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; z = 0; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 down => new Vector3(0, -1, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public static Vector3 back => new Vector3(0, 0, -1);
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 left => new Vector3(-1, 0, 0);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float s) => new Vector3(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator *(float s, Vector3 a) => new Vector3(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator /(Vector3 a, float s) => new Vector3(a.x / s, a.y / s, a.z / s);
        public float magnitude => MathF.Sqrt(x * x + y * y + z * z);
        public float sqrMagnitude => x * x + y * y + z * z;
        public Vector3 normalized { get { float m = magnitude; return m > 1e-9f ? this / m : zero; } }
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b - a) * Mathf.Clamp01(t);
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###}, {2:0.###})", x, y, z);
    }
    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0, 0, 0, 1);
        public static Quaternion AngleAxis(float degrees, Vector3 axis)
        {
            var n = axis.normalized; float h = degrees * Mathf.Deg2Rad * 0.5f, s = MathF.Sin(h);
            return new Quaternion(n.x * s, n.y * s, n.z * s, MathF.Cos(h));
        }
        // Unity order: Z, then X, then Y.
        public static Quaternion Euler(float x, float y, float z) => AngleAxis(y, Vector3.up) * AngleAxis(x, Vector3.right) * AngleAxis(z, Vector3.forward);
        public static Quaternion Euler(Vector3 e) => Euler(e.x, e.y, e.z);
        public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion(
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
            a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
            a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
            a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            float x2 = q.x * 2, y2 = q.y * 2, z2 = q.z * 2, xx = q.x * x2, yy = q.y * y2, zz = q.z * z2, xy = q.x * y2, xz = q.x * z2, yz = q.y * z2;
            float wx = q.w * x2, wy = q.w * y2, wz = q.w * z2;
            return new Vector3((1 - (yy + zz)) * v.x + (xy - wz) * v.y + (xz + wy) * v.z,
                (xy + wz) * v.x + (1 - (xx + zz)) * v.y + (yz - wx) * v.z,
                (xz - wy) * v.x + (yz + wx) * v.y + (1 - (xx + yy)) * v.z);
        }
        public static Quaternion Inverse(Quaternion q) { float n = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w; return new Quaternion(-q.x / n, -q.y / n, -q.z / n, q.w / n); }
        public static Quaternion LookRotation(Vector3 forward) => LookRotation(forward, Vector3.up);
        public static Quaternion LookRotation(Vector3 forward, Vector3 up)
        {
            var f = forward.normalized; var r = Vector3.Cross(up, f).normalized;
            if (r.sqrMagnitude < 1e-9f) r = Vector3.right;
            var u = Vector3.Cross(f, r);
            float m00 = r.x, m01 = u.x, m02 = f.x, m10 = r.y, m11 = u.y, m12 = f.y, m20 = r.z, m21 = u.z, m22 = f.z;
            float trace = m00 + m11 + m22;
            if (trace > 0) { float s = MathF.Sqrt(trace + 1) * 2; return new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s); }
            if (m00 > m11 && m00 > m22) { float s = MathF.Sqrt(1 + m00 - m11 - m22) * 2; return new Quaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s); }
            if (m11 > m22) { float s = MathF.Sqrt(1 + m11 - m00 - m22) * 2; return new Quaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s); }
            { float s = MathF.Sqrt(1 + m22 - m00 - m11) * 2; return new Quaternion((m02 + m20) / s, (m12 + m21) / s, 0.25f * s, (m10 - m01) / s); }
        }
    }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1);
        public static Color black => new Color(0, 0, 0);
        public static Color clear => new Color(0, 0, 0, 0);
        public Color linear => new Color(Broker.Core.ScreenLook.SrgbToLinear(r), Broker.Core.ScreenLook.SrgbToLinear(g), Broker.Core.ScreenLook.SrgbToLinear(b), a);
        public static Color operator *(Color c, float s) => new Color(c.r * s, c.g * s, c.b * s, c.a * s);
        public static Color operator *(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
        public static Color operator +(Color a, Color b) => new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a);
        public static Color Lerp(Color a, Color b, float t) { t = Mathf.Clamp01(t); return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t); }
    }
    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }
    public static class ColorUtility
    {
        public static bool TryParseHtmlString(string html, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrEmpty(html) || html[0] != '#' || (html.Length != 7 && html.Length != 9)) return false;
            uint v = uint.Parse(html.Substring(1, 6), NumberStyles.HexNumber);
            float a = html.Length == 9 ? int.Parse(html.Substring(7, 2), NumberStyles.HexNumber) / 255f : 1;
            color = new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, a); return true;
        }
    }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
    }
    public static class Mathf
    {
        public const float PI = MathF.PI, Deg2Rad = MathF.PI / 180f, Rad2Deg = 180f / MathF.PI;
        public static float Sin(float f) => MathF.Sin(f); public static float Cos(float f) => MathF.Cos(f);
        public static float Abs(float f) => MathF.Abs(f); public static int Abs(int f) => Math.Abs(f);
        public static float Sqrt(float f) => MathF.Sqrt(f); public static float Pow(float f, float p) => MathF.Pow(f, p);
        public static float Max(float a, float b) => MathF.Max(a, b); public static float Min(float a, float b) => MathF.Min(a, b);
        public static int Max(int a, int b) => Math.Max(a, b); public static int Min(int a, int b) => Math.Min(a, b);
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
        public static float Clamp01(float v) => Clamp(v, 0, 1);
        public static float Sign(float f) => f >= 0 ? 1 : -1;
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static int CeilToInt(float f) => (int)MathF.Ceiling(f); public static int FloorToInt(float f) => (int)MathF.Floor(f);
        public static int RoundToInt(float f) => (int)MathF.Round(f);
        public static float Atan2(float y, float x) => MathF.Atan2(y, x); public static float Exp(float f) => MathF.Exp(f);
        public static float Repeat(float t, float length) => Clamp(t - MathF.Floor(t / length) * length, 0, length);
        public static float DeltaAngle(float a, float b) { float d = Repeat(b - a, 360); return d > 180 ? d - 360 : d; }
        public static float LerpAngle(float a, float b, float t) => a + DeltaAngle(a, b) * Clamp01(t);
    }
    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }
    public enum TextureFormat { Alpha8 = 1, RGB24 = 3, RGBA32 = 4, ARGB32 = 5 }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureWrapMode { Repeat, Clamp, Mirror, MirrorOnce }
    public enum CameraClearFlags { Skybox = 1, Color = 2, SolidColor = 2, Depth = 3, Nothing = 4 }
    public enum LightType { Spot, Directional, Point, Area, Rectangle = 3, Disc }
    public enum LightShadows { None, Hard, Soft }
    public enum FogMode { Linear = 1, Exponential, ExponentialSquared }
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    [Flags] public enum MaterialGlobalIlluminationFlags { None = 0, RealtimeEmissive = 1, BakedEmissive = 2, AnyEmissive = 3, EmissiveIsBlack = 4 }

    public class Shader : Object
    {
        private static readonly Dictionary<string, Shader> found = new Dictionary<string, Shader>();
        private static readonly Dictionary<string, int> ids = new Dictionary<string, int>();
        public static Shader Find(string name) { if (!found.TryGetValue(name, out var s)) found[name] = s = new Shader { name = name }; return s; }
        public static int PropertyToID(string name) { if (!ids.TryGetValue(name, out int id)) ids[name] = id = ids.Count + 1; return id; }
        public bool isSupported => true;
    }
    [Flags] public enum HideFlags { None = 0, HideInHierarchy = 1, HideInInspector = 2, DontSaveInEditor = 4, NotEditable = 8, DontSaveInBuild = 16, DontUnloadUnusedAsset = 32, DontSave = 52, HideAndDontSave = 61 }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)] public sealed class RequireComponent : Attribute { public RequireComponent(Type type) { } }
    public enum RenderTextureFormat { ARGB32 = 0, Depth = 1, ARGBHalf = 2, Default = 7, DefaultHDR = 9, ARGBFloat = 11, RGHalf = 13, RHalf = 15, R8 = 16 }
    public enum RenderTextureReadWrite { Default, Linear, sRGB }
    [Flags] public enum DepthTextureMode { None = 0, Depth = 1, DepthNormals = 2, MotionVectors = 4 }
    public enum ColorSpace { Uninitialized = -1, Gamma = 0, Linear = 1 }
    public class RenderTexture : Texture
    {
        public static int Live;
        public RenderTextureFormat format { get; private set; }
        public static RenderTexture GetTemporary(int width, int height, int depthBuffer, RenderTextureFormat format, RenderTextureReadWrite readWrite)
        { Live++; return new RenderTexture { width = width, height = height, format = format }; }
        public static void ReleaseTemporary(RenderTexture texture) { if (texture != null) Live--; }
    }
    public static class Graphics
    {
        public static int Blits;
        public static void Blit(Texture source, RenderTexture dest) => Blits++;
        public static void Blit(Texture source, RenderTexture dest, Material material, int pass) => Blits++;
    }
    public static class SystemInfo { public static bool SupportsRenderTextureFormat(RenderTextureFormat format) => true; }
    public class Material : Object
    {
        private static readonly string[] standard = { "_Color", "_MainTex", "_Glossiness", "_Metallic", "_BumpMap", "_BumpScale", "_EmissionColor", "_EmissionMap" };
        private static readonly string[] sky = { "_SunDisk", "_SunSize", "_SunSizeConvergence", "_AtmosphereThickness", "_SkyTint", "_GroundColor", "_Exposure" };
        public readonly Dictionary<string, float> Floats = new Dictionary<string, float>();
        public readonly Dictionary<string, Color> Colors = new Dictionary<string, Color>();
        public readonly Dictionary<string, Texture> Textures = new Dictionary<string, Texture>();
        public readonly HashSet<string> Keywords = new HashSet<string>();
        public Shader shader { get; set; }
        public MaterialGlobalIlluminationFlags globalIlluminationFlags { get; set; }
        public Material(Shader shader) { this.shader = shader; Colors["_Color"] = Color.white; }
        public Material(Material source)
        {
            shader = source.shader; name = source.name;
            foreach (var p in source.Floats) Floats[p.Key] = p.Value; foreach (var p in source.Colors) Colors[p.Key] = p.Value;
            foreach (var p in source.Textures) Textures[p.Key] = p.Value; foreach (var k in source.Keywords) Keywords.Add(k);
        }
        public Color color { get => Colors["_Color"]; set => Colors["_Color"] = value; }
        public Texture mainTexture { get => GetTexture("_MainTex"); set => SetTexture("_MainTex", value); }
        public bool HasProperty(string property) => Array.IndexOf(shader != null && shader.name.StartsWith("Skybox") ? sky : standard, property) >= 0;
        public void SetFloat(string p, float v) => Floats[p] = v;
        public float GetFloat(string p) => Floats.TryGetValue(p, out var v) ? v : 0;
        public void SetColor(string p, Color v) => Colors[p] = v;
        public Color GetColor(string p) => Colors.TryGetValue(p, out var v) ? v : Color.black;
        public void SetTexture(string p, Texture t) => Textures[p] = t;
        public void SetTexture(int id, Texture t) => Textures["#" + id] = t;
        public void SetVector(int id, Vector4 v) => Vectors[id] = v;
        public void SetVector(string p, Vector4 v) => NamedVectors[p] = v;
        public readonly Dictionary<string, Vector4> NamedVectors = new Dictionary<string, Vector4>();
        public readonly Dictionary<int, Vector4> Vectors = new Dictionary<int, Vector4>();
        public Texture GetTexture(string p) => Textures.TryGetValue(p, out var t) ? t : null;
        public void EnableKeyword(string k) => Keywords.Add(k);
        public void DisableKeyword(string k) => Keywords.Remove(k);
        public bool IsKeywordEnabled(string k) => Keywords.Contains(k);
    }
    public class Texture : Object
    {
        public int width { get; protected set; } public int height { get; protected set; }
        public FilterMode filterMode { get; set; } = FilterMode.Bilinear;
        public TextureWrapMode wrapMode { get; set; }
        public int anisoLevel { get; set; } = 1;
    }
    public class Texture2D : Texture
    {
        private static Texture2D white, black;
        public static Texture2D blackTexture
        {
            get
            {
                if (black != null) return black;
                black = new Texture2D(4, 4); for (int i = 0; i < 16; i++) black.Pixels[i] = new Color32(0, 0, 0, 0); return black;
            }
        }
        public Color32[] Pixels;
        public readonly bool MipChain;
        public Texture2D(int width, int height) : this(width, height, TextureFormat.RGBA32, true) { }
        public Texture2D(int width, int height, TextureFormat format, bool mipChain) : this(width, height, format, mipChain, false) { }
        public Texture2D(int width, int height, TextureFormat format, bool mipChain, bool linear)
        { this.width = width; this.height = height; MipChain = mipChain; Pixels = new Color32[width * height]; }
        public static Texture2D whiteTexture
        {
            get
            {
                if (white != null) return white;
                white = new Texture2D(4, 4); for (int i = 0; i < 16; i++) white.Pixels[i] = new Color32(255, 255, 255, 255); return white;
            }
        }
        public void SetPixels32(Color32[] pixels) { if (pixels.Length != Pixels.Length) throw new ArgumentException("size"); Array.Copy(pixels, Pixels, pixels.Length); }
        public Color32[] GetPixels32() => (Color32[])Pixels.Clone();
        public void Apply() { } public void Apply(bool updateMipmaps) { } public void Apply(bool updateMipmaps, bool makeNoLongerReadable) { }
    }
    public static class Resources
    {
        public static T GetBuiltinResource<T>(string path) where T : Object => (T)Activator.CreateInstance(typeof(T), true);
    }
    public class Font : Object
    {
        private Material fontMaterial;
        public Material material => fontMaterial ?? (fontMaterial = new Material(Shader.Find("GUI/Text Shader")));
    }
    public class Mesh : Object
    {
        private readonly List<int[]> submeshes = new List<int[]> { new int[0] };
        public Vector3[] vertices { get; set; } = new Vector3[0];
        public Vector3[] normals { get; set; } = new Vector3[0];
        public Vector2[] uv { get; set; } = new Vector2[0];
        public Color32[] colors32 { get; set; } = new Color32[0];
        public Rendering.IndexFormat indexFormat { get; set; }
        public int[] triangles { get => submeshes.SelectMany(s => s).ToArray(); set { submeshes.Clear(); submeshes.Add(value); } }
        public int subMeshCount
        {
            get => submeshes.Count;
            set { while (submeshes.Count < value) submeshes.Add(new int[0]); while (submeshes.Count > value) submeshes.RemoveAt(submeshes.Count - 1); }
        }
        public void SetTriangles(int[] tris, int submesh)
        {
            if (submesh >= submeshes.Count) throw new ArgumentOutOfRangeException(nameof(submesh));
            if (indexFormat == Rendering.IndexFormat.UInt16 && vertices.Length > 65535) throw new InvalidOperationException("16-bit index overflow");
            foreach (int i in tris) if (i < 0 || i >= vertices.Length) throw new ArgumentOutOfRangeException(nameof(tris));
            submeshes[submesh] = (int[])tris.Clone();
        }
        public int[] GetTriangles(int submesh) => submeshes[submesh];
        public void RecalculateBounds() { } public void RecalculateTangents() { } public void RecalculateNormals() { }
        public Mesh Clone()
        {
            var m = new Mesh { name = name, vertices = (Vector3[])vertices.Clone(), normals = (Vector3[])normals.Clone(), uv = (Vector2[])uv.Clone(), colors32 = (Color32[])colors32.Clone(), indexFormat = indexFormat };
            m.subMeshCount = subMeshCount; for (int i = 0; i < subMeshCount; i++) m.submeshes[i] = (int[])submeshes[i].Clone();
            return m;
        }
    }
    public class MeshFilter : Component { public Mesh sharedMesh { get; set; } public Mesh mesh { get => sharedMesh; set => sharedMesh = value; } }
    public class Renderer : Component
    {
        public bool enabled { get; set; } = true;
        public Material[] sharedMaterials { get; set; } = new Material[0];
        public Material sharedMaterial { get => sharedMaterials.Length > 0 ? sharedMaterials[0] : null; set => sharedMaterials = new[] { value }; }
        public Material material { get => sharedMaterial; set => sharedMaterial = value; }
        public Rendering.ShadowCastingMode shadowCastingMode { get; set; } = Rendering.ShadowCastingMode.On;
        public bool receiveShadows { get; set; } = true;
    }
    public class MeshRenderer : Renderer { }
    public class Collider : Component { public bool enabled { get; set; } = true; }
    public class BoxCollider : Collider { public Vector3 center { get; set; } public Vector3 size { get; set; } = Vector3.one; }
    public class SphereCollider : Collider { public float radius { get; set; } = 0.5f; }
    public class CapsuleCollider : Collider { public float height { get; set; } = 2; public float radius { get; set; } = 0.5f; public Vector3 center { get; set; } }
    public class CharacterController : Collider { public float height { get; set; } public float radius { get; set; } public Vector3 center { get; set; } }
    public class Camera : Behaviour
    {
        public float fieldOfView { get; set; } = 60; public float nearClipPlane { get; set; } = 0.3f; public float farClipPlane { get; set; } = 1000;
        public float depth { get; set; } public CameraClearFlags clearFlags { get; set; } = CameraClearFlags.Skybox;
        public Color backgroundColor { get; set; } public bool orthographic { get; set; } public float orthographicSize { get; set; } = 5;
        public Rect rect { get; set; } = new Rect(0, 0, 1, 1);
        public bool allowHDR { get; set; } = true; public bool allowMSAA { get; set; } = true;
        public DepthTextureMode depthTextureMode { get; set; }
    }
    public class Light : Behaviour
    {
        public LightType type { get; set; } = LightType.Point; public Color color { get; set; } = Color.white; public float intensity { get; set; } = 1;
        public float range { get; set; } = 10; public LightShadows shadows { get; set; }
        public float shadowStrength { get; set; } = 1; public float shadowBias { get; set; } = 0.05f; public float shadowNormalBias { get; set; } = 0.4f;
    }
    public class AudioListener : Behaviour { }
    public class TextMesh : Component
    {
        public string text { get; set; } = ""; public Font font { get; set; } public int fontSize { get; set; }
        public float characterSize { get; set; } = 1; public TextAnchor anchor { get; set; } public Color color { get; set; } = Color.white;
    }
    public static class RenderSettings
    {
        public static bool fog; public static Color fogColor = new Color(0.5f, 0.5f, 0.5f); public static FogMode fogMode = FogMode.ExponentialSquared;
        public static float fogDensity = 0.01f, fogStartDistance = 0, fogEndDistance = 300;
        public static Rendering.AmbientMode ambientMode = Rendering.AmbientMode.Skybox;
        public static Color ambientSkyColor = new Color(0.212f, 0.227f, 0.259f), ambientEquatorColor = new Color(0.114f, 0.125f, 0.133f), ambientGroundColor = new Color(0.047f, 0.043f, 0.035f);
        public static Color ambientLight { get => ambientSkyColor; set => ambientSkyColor = value; }
        public static Material skybox = DefaultSkybox();
        private static Material DefaultSkybox()
        {
            var sky = new Material(Shader.Find("Skybox/Procedural")) { name = "Default-Skybox" };
            sky.SetFloat("_SunDisk", 2); sky.SetFloat("_SunSize", 0.04f); sky.SetFloat("_SunSizeConvergence", 5);
            sky.SetFloat("_AtmosphereThickness", 1); sky.SetFloat("_Exposure", 1.3f);
            sky.SetColor("_SkyTint", new Color(0.5f, 0.5f, 0.5f)); sky.SetColor("_GroundColor", new Color(0.369f, 0.349f, 0.341f));
            return sky;
        }
        public static Light sun;
    }
    public static class QualitySettings
    {
        public static float shadowDistance = 150;
        public static ColorSpace activeColorSpace = ColorSpace.Gamma;
    }
    public static class DynamicGI { public static int Updates; public static void UpdateEnvironment() => Updates++; }
    public static class Time { public static float unscaledTime = 3.2f, unscaledDeltaTime = 0.016f, time = 3.2f, deltaTime = 0.016f; }
    public static class Debug
    {
        public static readonly List<string> Warnings = new List<string>();
        public static void Log(object m) { } public static void LogWarning(object m) => Warnings.Add(m?.ToString()); public static void LogError(object m) => Warnings.Add("ERROR " + m);
    }
}
namespace UnityEngine.Rendering
{
    public enum AmbientMode { Skybox = 0, Trilight = 1, Flat = 3, Custom = 4 }
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
    public enum IndexFormat { UInt16, UInt32 }
    public class RenderPipelineAsset : Object { }
    public static class GraphicsSettings { public static RenderPipelineAsset currentRenderPipeline => null; }
}
