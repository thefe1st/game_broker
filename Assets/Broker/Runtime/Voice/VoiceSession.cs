using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Broker.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Broker.Runtime.Voice
{
    // Explicit two-click recording, preview before sending, no continuous listening.
    public sealed class VoiceSession : MonoBehaviour
    {
        [Serializable] private sealed class Transcript { public string text; }
        [Serializable] private sealed class SpeechInput { public string text; }
        public Action<string> OnTranscript;
        public bool Recording { get; private set; }
        public bool Working { get; private set; }
        public bool Playing => speaker != null && speaker.isPlaying;
        public string Status { get; private set; } = "Голос выключен.";
        private AudioClip microphoneClip;
        private AudioClip playbackClip;
        private AudioSource speaker;
        private string device;
        private double recordingStarted;
        private CancellationTokenSource pending;
        private int revision;

        private void Awake()
        {
            speaker = gameObject.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.spatialBlend = 0f;
            if (UnityEngine.Object.FindFirstObjectByType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();
        }

        private void Update()
        {
            if (Recording && Time.realtimeSinceStartupAsDouble - recordingStarted >= 20.0) FinishRecording();
        }
        private void OnDestroy() { Cancel(); }

        public void Cancel()
        {
            revision++;
            pending?.Cancel(); pending = null;
            if (Recording) Microphone.End(device);
            Recording = false; Working = false;
            if (speaker != null) { speaker.Stop(); speaker.clip = null; }
            if (microphoneClip != null) { Destroy(microphoneClip); microphoneClip = null; }
            if (playbackClip != null) { Destroy(playbackClip); playbackClip = null; }
            Status = "Голос остановлен.";
        }

        public async void BeginRecording()
        {
            Cancel();
            var source = new CancellationTokenSource(); pending = source;
            int version = ++revision;
            Working = true;
            try
            {
                Status = "Запрашиваю доступ к микрофону…";
                if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
                {
                    var permission = Application.RequestUserAuthorization(UserAuthorization.Microphone);
                    while (!permission.isDone) { source.Token.ThrowIfCancellationRequested(); await Task.Yield(); }
                    if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
                        throw new InvalidOperationException("Нет разрешения на микрофон.");
                }
                source.Token.ThrowIfCancellationRequested();
                if (Microphone.devices.Length == 0) throw new InvalidOperationException("Микрофон не найден.");
                device = Microphone.devices[0];
                Microphone.GetDeviceCaps(device, out int minimum, out int maximum);
                int rate = 16000;
                if (minimum > 0 && maximum >= minimum) rate = Mathf.Clamp(rate, minimum, maximum);
                if (rate < 8000 || rate > 48000) throw new InvalidOperationException("Нужен микрофон с частотой 8–48 кГц.");
                microphoneClip = Microphone.Start(device, false, 20, rate);
                if (microphoneClip == null) throw new InvalidOperationException("Не удалось начать запись.");
                recordingStarted = Time.realtimeSinceStartupAsDouble;
                Recording = true;
                Status = "● Идёт запись. Нажми «Закончить запись». Максимум 20 секунд.";
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (version == revision) Status = error.Message; }
            finally
            {
                source.Dispose();
                if (version == revision) { pending = null; Working = false; }
            }
        }

        public void FinishRecording()
        {
            if (!Recording || microphoneClip == null) return;
            try
            {
                int frames = Microphone.GetPosition(device);
                if (frames <= 0 && Time.realtimeSinceStartupAsDouble - recordingStarted >= 19.5)
                    frames = microphoneClip.samples;
                Microphone.End(device); Recording = false;
                if (frames < microphoneClip.frequency / 5) throw new InvalidOperationException("Запись слишком короткая. Попробуй ещё раз.");
                var samples = new float[frames * microphoneClip.channels];
                if (!microphoneClip.GetData(samples, 0)) throw new InvalidOperationException("Не удалось прочитать запись.");
                // Downmix to mono so even 48 kHz devices stay under the upload limit.
                int channels = microphoneClip.channels;
                var mono = new float[frames];
                for (int frame = 0; frame < frames; frame++)
                {
                    float sum = 0f;
                    for (int channel = 0; channel < channels; channel++) sum += samples[frame * channels + channel];
                    mono[frame] = sum / channels;
                }
                byte[] wav = WaveCodec.Encode(mono, 1, microphoneClip.frequency);
                Destroy(microphoneClip); microphoneClip = null;
                Transcribe(wav);
            }
            catch (Exception error) { Cancel(); Status = error.Message; }
        }

        private async void Transcribe(byte[] wav)
        {
            Cancel();
            var source = new CancellationTokenSource(); pending = source;
            int version = ++revision; Working = true; Status = "Распознаю речь…";
            try
            {
                byte[] bytes = await Post("transcribe", wav, "audio/wav", 8192, source.Token);
                var result = JsonUtility.FromJson<Transcript>(Encoding.UTF8.GetString(bytes));
                if (result == null || string.IsNullOrWhiteSpace(result.text) || result.text.Length > 500)
                    throw new InvalidOperationException("Не удалось распознать речь.");
                if (version != revision) return;
                Status = "Текст распознан. Проверь его и нажми «Отправить».";
                OnTranscript?.Invoke(result.text);
            }
            catch (OperationCanceledException) { }
            catch (Exception) { if (version == revision) Status = "Распознавание недоступно. Проверь сервер и VOICE_MODE=ai; можно написать текстом."; }
            finally
            {
                source.Dispose();
                if (version == revision) { Working = false; pending = null; }
            }
        }

        public async void PlayReply(string text)
        {
            Cancel();
            if (string.IsNullOrWhiteSpace(text)) return;
            text = text.Split('\n')[0];
            if (text.StartsWith("Борис: ", StringComparison.Ordinal)) text = text.Substring("Борис: ".Length);
            if (text.Length > 400) text = text.Substring(0, 400);
            var source = new CancellationTokenSource(); pending = source;
            int version = ++revision; Working = true; Status = "Готовлю голос Бориса…";
            try
            {
                var json = JsonUtility.ToJson(new SpeechInput { text = text });
                byte[] bytes = await Post("speech", Encoding.UTF8.GetBytes(json), "application/json", 2000000, source.Token);
                if (version != revision) return;
                WaveData wave = WaveCodec.Decode(bytes);
                playbackClip = AudioClip.Create("Boris response", wave.Samples.Length / wave.Channels, wave.Channels, wave.Frequency, false);
                playbackClip.SetData(wave.Samples, 0);
                speaker.clip = playbackClip; speaker.Play();
                Status = "Борис отвечает. Новая запись остановит воспроизведение.";
            }
            catch (OperationCanceledException) { }
            catch (Exception) { if (version == revision) Status = "Озвучка недоступна. Проверь сервер и VOICE_MODE=ai; ответ остаётся в тексте."; }
            finally
            {
                source.Dispose();
                if (version == revision) { Working = false; pending = null; }
            }
        }

        private static async Task<byte[]> Post(string path, byte[] body, string contentType, int maxBytes, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (var web = new UnityWebRequest("http://127.0.0.1:8787/" + path, "POST"))
            {
                web.uploadHandler = new UploadHandlerRaw(body);
                web.downloadHandler = new DownloadHandlerBuffer();
                web.SetRequestHeader("Content-Type", contentType); web.timeout = 30;
                var operation = web.SendWebRequest();
                while (!operation.isDone)
                {
                    if (token.IsCancellationRequested) { web.Abort(); token.ThrowIfCancellationRequested(); }
                    if (web.downloadedBytes > (ulong)maxBytes) { web.Abort(); throw new InvalidOperationException("Audio response too large"); }
                    await Task.Yield();
                }
                token.ThrowIfCancellationRequested();
                if (web.result != UnityWebRequest.Result.Success || web.downloadHandler.data.Length > maxBytes)
                    throw new InvalidOperationException("Voice gateway unavailable");
                return web.downloadHandler.data;
            }
        }
    }
}
