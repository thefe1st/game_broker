// Explicit bootstrap test doubles. NOT Unity assemblies, rendering, GUI layout or physics.
// Actual Core/Bootstrap/Voice/Dialogue sources execute; scene, controls and GUI are replaced.
using System;
using System.Collections.Generic;
using Broker.Core;
using Broker.Runtime.Office;

namespace UnityEngine
{
    public partial class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { }
    }
    public static partial class Mathf
    {
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Clamp01(float f) => Math.Clamp(f, 0, 1);
        public static int CeilToInt(float f) => (int)Math.Ceiling(f);
    }
    public static partial class Time { public static float unscaledDeltaTime = 1; }
    public static partial class Application { public static string persistentDataPath; }
    public static partial class JsonUtility { public static string ToJson(object o, bool prettyPrint) => ToJson(o); }
    public enum RuntimeInitializeLoadType { AfterSceneLoad }
    [AttributeUsage(AttributeTargets.Method)] public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2();
    }
    public struct Vector3
    {
        public Vector3(float x, float y, float z) { }
        public static Vector3 one => new Vector3(1,1,1);
        public static Vector3 operator *(Vector3 v, float f) => v;
    }
    public struct Quaternion { public static Quaternion identity => new Quaternion(); }
    public struct Matrix4x4
    {
        public static Matrix4x4 identity => new Matrix4x4();
        public static Matrix4x4 TRS(Vector3 p, Quaternion q, Vector3 s) => identity;
    }
    public struct Color { public Color(float r, float g, float b, float a = 1) { } }
    public struct Rect { public Rect(float x, float y, float w, float h) { } }
    public class Texture2D { public static Texture2D whiteTexture = new Texture2D(); }
    public class GUIStyle { public float CalcHeight(GUIContent c, float w) => 24; }
    public class GUIContent { public GUIContent(string text) { } }
    public static class Screen { public static int width = 1440, height = 960; }
    public enum KeyCode { Return, KeypadEnter }
    public enum EventType { KeyDown }
    public class Event
    { public static Event current = new Event(); public EventType type; public KeyCode keyCode; public void Use() { } }
    public static class GUI
    {
        public static bool enabled = true;
        public static Matrix4x4 matrix; public static Color color;
        public static string Click;
        public static readonly List<string> Labels = new List<string>();
        public static void Reset() { enabled = true; Click = null; Labels.Clear(); }
        public static void Label(Rect r, string text, GUIStyle s) { Labels.Add(text); }
        public static void Box(Rect r, string text, GUIStyle s) { }
        public static void DrawTexture(Rect r, Texture2D t) { }
        public static bool Button(Rect r, string text, GUIStyle s)
        { Labels.Add(text); if (!enabled || Click != text) return false; Click = null; return true; }
        public static bool Toggle(Rect r, bool value, string label, GUIStyle s) => value;
        public static Vector2 BeginScrollView(Rect r, Vector2 pos, Rect view) => pos;
        public static void EndScrollView() { }
        public static void SetNextControlName(string s) { }
        public static string TextField(Rect r, string text, int max, GUIStyle s) => text;
        public static void FocusControl(string s) { }
        public static string GetNameOfFocusedControl() => "";
    }
}
namespace Broker.Runtime.Office
{
    public sealed class BrokerTheme
    {
        public UnityEngine.Color Border, Accent, Ink, Gold, Red, Green;
        public UnityEngine.GUIStyle Text = new UnityEngine.GUIStyle(), Small = new UnityEngine.GUIStyle(),
            Heading = new UnityEngine.GUIStyle(), Title = new UnityEngine.GUIStyle(), Tag = new UnityEngine.GUIStyle(),
            Display = new UnityEngine.GUIStyle(), Metric = new UnityEngine.GUIStyle(), Field = new UnityEngine.GUIStyle(),
            Button = new UnityEngine.GUIStyle(), Primary = new UnityEngine.GUIStyle(), PanelStyle = new UnityEngine.GUIStyle();
        public static UnityEngine.Color Hex(string s) => new UnityEngine.Color();
        public void Dispose() { }
    }
    public static class OfficeInput
    {
        public static bool Available = true, Escape;
        public static bool EscapePressed() { bool value = Escape; Escape = false; return value; }
    }
    public sealed class FakePlayer
    {
        public Action<OfficeInteractable> OnInteraction; public Action OnFocusLost;
        public bool ControlEnabled, Captured; public OfficeInteractable Target;
        public void SetControl(bool value) { ControlEnabled = value; if (!value) Captured = false; }
        public void Capture() { Captured = ControlEnabled; }
    }
    public sealed class OfficeScene : UnityEngine.MonoBehaviour
    {
        public FakePlayer Player = new FakePlayer();
        public bool IsInCity, WorldMotionEnabled;
        public CareerMode Mode;
        public void VisitCity(bool outside) { IsInCity = outside; }
        public void Present(bool atDesk, float scale, UnityEngine.Vector2 offset) { }
        public void Refresh(BrokerDay day) { }
        public void ConfigureCareerMode(CareerMode mode) { Mode = mode; }
        public void ResetPlayer() { IsInCity = false; }
    }
}
