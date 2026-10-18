using System;
using System.Threading;
using System.Threading.Tasks;
using Broker.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Broker.Runtime.Voice
{
    public sealed partial class VoiceSession
    {
        [Serializable] private sealed class GatewayHealth { public string status; public string mode; public string voice; }
        private sealed class GatewayException : Exception { public GatewayException(string message) : base(message) { } }
        private bool previewRecording, healthChecking;
        private double nextLevelUpdate;
        public string[] Devices { get; private set; } = new string[0];
        public string SelectedDevice { get; private set; }
        public float InputLevel { get; private set; }
        public bool HasLocalPreview { get; private set; }
        public string LocalSignalHint { get; private set; } = "";
        public string GatewayStatus { get; private set; } = "Подключение ещё не проверялось.";

        public void RefreshDevices()
        {
            Devices = Microphone.devices;
            if (Array.IndexOf(Devices, SelectedDevice) < 0)
                SelectedDevice = Devices.Length == 0 ? null : Devices[0];
        }

        public void SelectNextDevice()
        {
            if (Recording || Working) return;
            Cancel(); RefreshDevices();
            if (Devices.Length > 0) SelectedDevice = Devices[(Array.IndexOf(Devices, SelectedDevice) + 1) % Devices.Length];
            Status = "Микрофон выбран. Выбор действует до закрытия игры.";
        }

        private void UpdateInputLevel()
        {
            if (!Recording || microphoneClip == null || Time.realtimeSinceStartupAsDouble < nextLevelUpdate) return;
            nextLevelUpdate = Time.realtimeSinceStartupAsDouble + 0.1;
            try
            {
                int frames = Microphone.GetPosition(device);
                if (frames <= 0) { InputLevel = 0f; return; }
                int count = Math.Min(1024, frames);
                var samples = new float[count * microphoneClip.channels];
                InputLevel = microphoneClip.GetData(samples, frames - count) ? VoiceDiagnostics.Rms(samples) : 0f;
            }
            catch (Exception) { InputLevel = 0f; }
        }

        public void PlayLocalPreview()
        {
            if (!HasLocalPreview || playbackClip == null || Recording || Working) return;
            speaker.Stop(); speaker.clip = playbackClip; speaker.Play();
            Status = "Прослушивание локальной записи. " + LocalSignalHint;
        }

        // GET /health is local-only. Never calls the audio provider or validates an API key.
        public async void CheckGateway()
        {
            Cancel();
            var source = new CancellationTokenSource(); pending = source;
            int version = ++revision; Working = true; healthChecking = true;
            GatewayStatus = "Проверяю http://127.0.0.1:8787/health…";
            try
            {
                using (var web = UnityWebRequest.Get("http://127.0.0.1:8787/health"))
                {
                    web.timeout = 5;
                    var operation = web.SendWebRequest();
                    while (!operation.isDone)
                    {
                        if (source.IsCancellationRequested) { web.Abort(); source.Token.ThrowIfCancellationRequested(); }
                        if (web.downloadedBytes > 4096) { web.Abort(); throw new GatewayException("Ответ /health слишком большой. Проверь порт 8787."); }
                        await Task.Yield();
                    }
                    source.Token.ThrowIfCancellationRequested();
                    if (version != revision) return;
                    if (web.result != UnityWebRequest.Result.Success)
                        throw new GatewayException(VoiceDiagnostics.RequestFailure(web.responseCode));
                    if (web.downloadHandler.data == null || web.downloadHandler.data.Length > 4096)
                        throw new GatewayException("Некорректный ответ /health. Проверь версию сервера.");
                    var health = JsonUtility.FromJson<GatewayHealth>(web.downloadHandler.text);
                    GatewayStatus = health == null ? VoiceDiagnostics.HealthHint(null, null, null)
                        : VoiceDiagnostics.HealthHint(health.status, health.mode, health.voice);
                }
            }
            catch (OperationCanceledException) { }
            catch (GatewayException error) { if (version == revision) GatewayStatus = error.Message; }
            catch (Exception) { if (version == revision) GatewayStatus = "Не удалось проверить шлюз: неверный ответ или ошибка соединения. Проверь сервер и порт 8787."; }
            finally
            {
                source.Dispose();
                if (version == revision) { pending = null; Working = false; healthChecking = false; }
            }
        }
    }
}
