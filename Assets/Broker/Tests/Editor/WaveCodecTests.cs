using System;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class WaveCodecTests
    {
        [Test] public void Pcm16RoundTripPreservesSamples()
        {
            var input = new float[1600];
            input[0] = -1; input[1] = 0.5f; input[2] = 1;
            var wave = WaveCodec.Decode(WaveCodec.Encode(input, 1, 16000));
            Assert.That(wave.Channels, Is.EqualTo(1));
            Assert.That(wave.Frequency, Is.EqualTo(16000));
            Assert.That(wave.Samples.Length, Is.EqualTo(input.Length));
            for (int i = 0; i < input.Length; i++) Assert.That(wave.Samples[i], Is.EqualTo(input[i]).Within(0.0001));
        }
        [Test] public void ClipsAndSanitizesNonFiniteSamples()
        {
            var input = new float[1600]; input[0] = 2; input[1] = float.NaN; input[2] = float.PositiveInfinity;
            var wave = WaveCodec.Decode(WaveCodec.Encode(input, 1, 16000));
            Assert.That(wave.Samples[0], Is.GreaterThan(0.99f));
            Assert.That(wave.Samples[1], Is.Zero); Assert.That(wave.Samples[2], Is.Zero);
        }
        [Test] public void TruncatedWavRejected()
        {
            var bytes = WaveCodec.Encode(new float[1600], 1, 16000);
            Array.Resize(ref bytes, bytes.Length - 1);
            Assert.Throws<ArgumentException>(() => WaveCodec.Decode(bytes));
        }
        [Test] public void NonPcmFormatRejected()
        {
            var bytes = WaveCodec.Encode(new float[1600], 1, 16000); bytes[20] = 3;
            Assert.Throws<ArgumentException>(() => WaveCodec.Decode(bytes));
        }
        [Test] public void OverlongMicrophoneCaptureRejected()
        {
            Assert.Throws<ArgumentException>(() => WaveCodec.Encode(new float[16000 * 21], 1, 16000));
        }
        [Test] public void InvalidChannelsRejected()
        {
            Assert.Throws<ArgumentException>(() => WaveCodec.Encode(new float[1600], 3, 16000));
        }
    }
}
