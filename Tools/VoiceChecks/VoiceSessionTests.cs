using System;
using System.Reflection;
using System.Threading.Tasks;
using Broker.Runtime.Voice;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;

namespace Broker.VoiceChecks
{
    [NonParallelizable]
    public sealed class VoiceSessionTests
    {
        private VoiceSession voice;
        private int transcripts;
        private static void Invoke(VoiceSession instance, string method)
            => typeof(VoiceSession).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, null);
        [SetUp] public void SetUp()
        {
            Microphone.devices = new[] { "Mic A", "Mic B" }; Microphone.Position = 0; Microphone.Ends = 0;
            Application.Authorized = true; Time.realtimeSinceStartupAsDouble = 0;
            UnityWebRequest.Sent = 0; UnityWebRequest.Aborted = 0; UnityWebRequest.Hold = false;
            UnityWebRequest.NextCode = 200;
            UnityWebRequest.NextBody = "{\"status\":\"ok\",\"mode\":\"mock\",\"voice\":\"ai\"}";
            voice = new VoiceSession(); Invoke(voice, "Awake"); transcripts = 0;
            voice.OnTranscript = _ => transcripts++;
        }
        [TearDown] public void TearDown() { voice.Cancel(); }
        private void Capture(bool local = true)
        {
            voice.BeginRecording(local); Assert.That(voice.Recording, Is.True);
            Microphone.Position = 16000;
            var data = new float[16000]; Array.Fill(data, 0.1f);
            Microphone.LastClip.SetData(data, 0);
            voice.FinishRecording();
        }
        [Test] public void LocalCaptureNeverSendsNetworkOrTranscript()
        {
            Capture(); Assert.That(voice.HasLocalPreview, Is.True);
            Assert.That(UnityWebRequest.Sent, Is.Zero); Assert.That(transcripts, Is.Zero);
            Assert.That(voice.Recording, Is.False); Assert.That(Microphone.LastClip.Destroyed, Is.True);
        }
        [Test] public void PreviewCanReplayWithoutNetwork()
        {
            Capture(); voice.PlayLocalPreview(); Assert.That(voice.Playing, Is.True);
            voice.PlayLocalPreview(); Assert.That(UnityWebRequest.Sent, Is.Zero);
        }
        [Test] public void CancelDeletesPreviewAndStopsPlayback()
        {
            Capture(); voice.PlayLocalPreview(); voice.Cancel(); voice.PlayLocalPreview();
            Assert.That(voice.HasLocalPreview, Is.False); Assert.That(voice.Playing, Is.False);
        }
        [Test] public void CancellingCaptureEndsDeviceAndClearsClip()
        {
            voice.BeginRecording(true); voice.Cancel();
            Assert.That(voice.Recording, Is.False); Assert.That(Microphone.Ends, Is.EqualTo(1));
            Assert.That(Microphone.LastClip.Destroyed, Is.True); Assert.That(UnityWebRequest.Sent, Is.Zero);
        }
        [Test] public void DeviceSelectionIsUsedForRecording()
        {
            voice.RefreshDevices(); voice.SelectNextDevice(); voice.BeginRecording(true);
            Assert.That(Microphone.LastDevice, Is.EqualTo("Mic B"));
        }
        [Test] public void MissingDevicesGivesLocalFailure()
        {
            Microphone.devices = Array.Empty<string>(); voice.BeginRecording(true);
            Assert.That(voice.Recording, Is.False); Assert.That(voice.Working, Is.False);
            StringAssert.Contains("не найден", voice.Status); Assert.That(UnityWebRequest.Sent, Is.Zero);
        }
        [Test] public void DeniedPermissionNeverRecordsOrSends()
        {
            Application.Authorized = false; voice.BeginRecording(true);
            Assert.That(voice.Recording, Is.False); StringAssert.Contains("разрешения", voice.Status);
            Assert.That(UnityWebRequest.Sent, Is.Zero);
        }
        [Test] public void TooShortLocalRecordingDoesNotUpload()
        {
            voice.BeginRecording(true); Microphone.Position = 100; voice.FinishRecording();
            Assert.That(voice.HasLocalPreview, Is.False); StringAssert.Contains("короткая", voice.Status);
            Assert.That(UnityWebRequest.Sent, Is.Zero);
        }
        [Test] public void LocalCaptureAutoStopsAtTwentySecondsWithoutUpload()
        {
            voice.BeginRecording(true); Time.realtimeSinceStartupAsDouble = 20;
            Invoke(voice, "Update");
            Assert.That(voice.Recording, Is.False); Assert.That(voice.HasLocalPreview, Is.True);
            Assert.That(UnityWebRequest.Sent, Is.Zero);
        }
        [Test] public void InputLevelReadsRecentSamples()
        {
            voice.BeginRecording(true); Microphone.Position = 16000;
            var data = new float[16000]; Array.Fill(data, 0.25f); Microphone.LastClip.SetData(data, 0);
            Invoke(voice, "Update"); Assert.That(voice.InputLevel, Is.EqualTo(0.25f).Within(0.001));
        }
        [Test] public void HealthOnlyMakesLocalGetWithoutAudio()
        {
            voice.CheckGateway(); Assert.That(UnityWebRequest.Sent, Is.EqualTo(1));
            Assert.That(UnityWebRequest.Last.url, Is.EqualTo("http://127.0.0.1:8787/health"));
            Assert.That(UnityWebRequest.Last.method, Is.EqualTo("GET"));
            Assert.That(UnityWebRequest.Last.uploadHandler, Is.Null);
            StringAssert.Contains("НЕ проверяются", voice.GatewayStatus);
        }
        [Test] public void HealthRejectsUnexpectedAndOversizedResponses()
        {
            UnityWebRequest.NextBody = "{}"; voice.CheckGateway();
            StringAssert.Contains("неизвестном формате", voice.GatewayStatus);
            UnityWebRequest.NextBody = new string('x', 5000); voice.CheckGateway();
            StringAssert.Contains("Некорректный ответ", voice.GatewayStatus);
        }
        [Test] public void VoiceRequestUsesSpecificSafeFailureAndKeepsItAfterCancel()
        {
            UnityWebRequest.NextCode = 503; UnityWebRequest.NextBody = "SECRET_PROVIDER_MESSAGE";
            Capture(false); StringAssert.Contains("VOICE_MODE=ai", voice.Status);
            StringAssert.DoesNotContain("SECRET", voice.Status); voice.Cancel();
            StringAssert.Contains("VOICE_MODE=ai", voice.LastFailure);
        }
        [Test] public void ExternalCaptureStillDeliversTranscript()
        {
            UnityWebRequest.NextBody = "{\"text\":\"Какая у вас цель?\"}";
            Capture(false); Assert.That(transcripts, Is.EqualTo(1));
            Assert.That(voice.HasLocalPreview, Is.False);
            Assert.That(UnityWebRequest.Last.url, Does.EndWith("/transcribe"));
        }
        [Test] public async Task CancelledHealthCannotOverwriteNewCheck()
        {
            UnityWebRequest.Hold = true; voice.CheckGateway();
            var held = UnityWebRequest.LastOperation; voice.Cancel();
            UnityWebRequest.Hold = false; voice.CheckGateway();
            held.isDone = true; await Task.Delay(30);
            StringAssert.Contains("НЕ проверяются", voice.GatewayStatus);
            Assert.That(voice.Working, Is.False);
        }
        [Test] public async Task CancelledTranscriptionCannotDeliverText()
        {
            UnityWebRequest.Hold = true; UnityWebRequest.NextBody = "{\"text\":\"Late reply\"}";
            Capture(false); var held = UnityWebRequest.LastOperation;
            voice.Cancel(); held.isDone = true; await Task.Delay(30);
            Assert.That(transcripts, Is.Zero); Assert.That(voice.Working, Is.False);
        }
    }
}
