using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class VoiceDiagnosticsTests
    {
        [Test] public void SilenceHasZeroLevel()
        { Assert.That(VoiceDiagnostics.Rms(new float[1024]), Is.Zero); }
        [Test] public void MissingSamplesHaveZeroLevel()
        { Assert.That(VoiceDiagnostics.Rms(null), Is.Zero); Assert.That(VoiceDiagnostics.Rms(new float[0]), Is.Zero); }
        [Test] public void RmsUsesBothPolarities()
        { Assert.That(VoiceDiagnostics.Rms(new[] { 0.5f, -0.5f }), Is.EqualTo(0.5f).Within(0.0001)); }
        [Test] public void NonFiniteSamplesAreSilenceAndOutliersAreClamped()
        { Assert.That(VoiceDiagnostics.Rms(new[] { float.NaN, float.PositiveInfinity, 2f, -2f }), Is.EqualTo(0.7071067f).Within(0.0001)); }
        [Test] public void QuietSignalSuggestsDeviceCheck()
        { StringAssert.Contains("Почти тишина", VoiceDiagnostics.SignalHint(0.001f)); }
        [Test] public void LoudSignalSuggestsLowerGain()
        { StringAssert.Contains("уменьши", VoiceDiagnostics.SignalHint(0.8f)); }
        [Test] public void SignalIsNotProofOfSpeech()
        { StringAssert.Contains("не отличает речь от шума", VoiceDiagnostics.SignalHint(0.1f)); }
        [Test] public void HealthIsNotProofOfWorkingApi()
        { StringAssert.Contains("НЕ проверяются", VoiceDiagnostics.HealthHint("ok", "mock", "ai")); }
        [Test] public void DisabledVoiceExplainsConfiguration()
        { StringAssert.Contains("VOICE_MODE=ai", VoiceDiagnostics.HealthHint("ok", "ai", "disabled")); }
        [Test] public void UnknownHealthDoesNotEchoUntrustedContent()
        {
            string result = VoiceDiagnostics.HealthHint("secret", "secret", "secret");
            StringAssert.Contains("неизвестном формате", result); StringAssert.DoesNotContain("secret", result);
        }
        [TestCase(0, "npm.cmd start")]
        [TestCase(400, "400")]
        [TestCase(403, "403")]
        [TestCase(404, "404/405")]
        [TestCase(405, "404/405")]
        [TestCase(413, "короткую")]
        [TestCase(415, "PCM16 WAV")]
        [TestCase(429, "минуту")]
        [TestCase(502, "ключ, оплату")]
        [TestCase(503, "VOICE_MODE=ai")]
        [TestCase(504, "не дождался")]
        [TestCase(599, "Проверь сервер")]
        public void RequestFailuresGiveActionableHints(long code, string expected)
        { StringAssert.Contains(expected, VoiceDiagnostics.RequestFailure(code)); }
    }
}
