// Narrow test doubles, NOT Unity reference assemblies. Tests verify branching/cancellation,
// never editor compatibility, real device permissions, recording or audio playback.
using System;
using System.Text;
using System.Text.Json;

namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public static T FindFirstObjectByType<T>() where T : class => null;
        public static void Destroy(Object value) { if (value != null) value.Destroyed = true; }
    }
    public class GameObject
    {
        public T AddComponent<T>() where T : new() => new T();
    }
    public class MonoBehaviour : Object { public GameObject gameObject = new GameObject(); }
    public class AudioListener : Object { }
    public class AudioSource : Object
    {
        public bool playOnAwake, isPlaying;
        public float spatialBlend;
        public AudioClip clip;
        public void Stop() { isPlaying = false; }
        public void Play() { isPlaying = true; }
    }
    public class AudioClip : Object
    {
        public int samples, channels, frequency;
        private float[] data;
        public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream)
            => new AudioClip { samples = lengthSamples, channels = channels, frequency = frequency, data = new float[lengthSamples * channels] };
        public bool GetData(float[] buffer, int offsetSamples)
        {
            Array.Copy(data, offsetSamples * channels, buffer, 0, buffer.Length); return true;
        }
        public bool SetData(float[] buffer, int offsetSamples)
        { Array.Copy(buffer, 0, data, offsetSamples * channels, buffer.Length); return true; }
    }
    public static class Microphone
    {
        public static string[] devices = new[] { "Mic A", "Mic B" };
        public static int Position, Ends;
        public static AudioClip LastClip;
        public static string LastDevice;
        public static void GetDeviceCaps(string device, out int min, out int max) { min = 8000; max = 48000; }
        public static AudioClip Start(string device, bool loop, int lengthSec, int frequency)
        { LastDevice = device; return LastClip = AudioClip.Create("capture", lengthSec * frequency, 1, frequency, false); }
        public static int GetPosition(string device) => Position;
        public static void End(string device) { Ends++; }
    }
    public static class Mathf { public static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max); }
    public static class Time { public static double realtimeSinceStartupAsDouble; }
    public enum UserAuthorization { Microphone }
    public class AsyncOperation { public volatile bool isDone = true; }
    public static class Application
    {
        public static bool Authorized = true;
        public static bool HasUserAuthorization(UserAuthorization value) => Authorized;
        public static AsyncOperation RequestUserAuthorization(UserAuthorization value) => new AsyncOperation();
    }
    public static class JsonUtility
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
        public static string ToJson(object value) => JsonSerializer.Serialize(value, Options);
        public static T FromJson<T>(string text) => JsonSerializer.Deserialize<T>(text, Options);
    }
}
namespace UnityEngine.Networking
{
    public class UploadHandlerRaw { public byte[] data; public UploadHandlerRaw(byte[] value) { data = value; } }
    public class DownloadHandlerBuffer
    { public byte[] data = Array.Empty<byte>(); public string text => Encoding.UTF8.GetString(data); }
    public class UnityWebRequest : IDisposable
    {
        public enum Result { InProgress, Success, ConnectionError, ProtocolError, DataProcessingError }
        public static int Sent, Aborted;
        public static long NextCode = 200;
        public static string NextBody = "{\"status\":\"ok\",\"mode\":\"mock\",\"voice\":\"ai\"}";
        public static bool Hold;
        public static UnityWebRequest Last;
        public static UnityEngine.AsyncOperation LastOperation;
        public UploadHandlerRaw uploadHandler;
        public DownloadHandlerBuffer downloadHandler;
        public int timeout;
        public string url, method;
        public long responseCode;
        public ulong downloadedBytes => (ulong)(downloadHandler?.data.Length ?? 0);
        public Result result;
        public UnityWebRequest(string url, string method) { this.url = url; this.method = method; }
        public static UnityWebRequest Get(string url) => new UnityWebRequest(url, "GET") { downloadHandler = new DownloadHandlerBuffer() };
        public void SetRequestHeader(string name, string value) { }
        public UnityEngine.AsyncOperation SendWebRequest()
        {
            Sent++; Last = this; responseCode = NextCode;
            result = NextCode == 200 ? Result.Success : NextCode == 0 ? Result.ConnectionError : Result.ProtocolError;
            downloadHandler.data = Encoding.UTF8.GetBytes(NextBody);
            return LastOperation = new UnityEngine.AsyncOperation { isDone = !Hold };
        }
        public void Abort() { Aborted++; }
        public void Dispose() { }
    }
}
