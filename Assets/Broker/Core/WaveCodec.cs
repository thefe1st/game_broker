using System;
using System.IO;
using System.Text;

namespace Broker.Core
{
    public sealed class WaveData
    {
        public readonly float[] Samples;
        public readonly int Channels;
        public readonly int Frequency;
        public WaveData(float[] samples, int channels, int frequency)
        { Samples = samples; Channels = channels; Frequency = frequency; }
    }

    // Only little-endian PCM16 WAV. No Unity dependency, no file storage.
    public static class WaveCodec
    {
        public static byte[] Encode(float[] samples, int channels, int frequency)
        {
            if (samples == null || samples.Length == 0 || channels < 1 || channels > 2
                || frequency < 8000 || frequency > 48000 || samples.Length % channels != 0
                || samples.Length / channels > frequency * 20)
                throw new ArgumentException("Invalid microphone samples");
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.ASCII))
            {
                int size = samples.Length * 2;
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + size);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)channels); writer.Write(frequency);
                writer.Write(frequency * channels * 2); writer.Write((short)(channels * 2)); writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(size);
                foreach (float sample in samples)
                {
                    float value = float.IsNaN(sample) || float.IsInfinity(sample) ? 0f : Math.Max(-1f, Math.Min(1f, sample));
                    writer.Write((short)Math.Round(value * 32767f));
                }
                return stream.ToArray();
            }
        }

        public static WaveData Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 44 || bytes.Length > 2000000)
                throw new ArgumentException("Invalid WAV size");
            using (var reader = new BinaryReader(new MemoryStream(bytes), Encoding.ASCII))
            {
                if (Tag(reader) != "RIFF" || reader.ReadUInt32() != bytes.Length - 8 || Tag(reader) != "WAVE")
                    throw new ArgumentException("Invalid WAV header");
                bool hasFormat = false;
                int channels = 0, frequency = 0, align = 0, dataOffset = -1, dataSize = 0;
                while (reader.BaseStream.Position + 8 <= bytes.Length)
                {
                    string tag = Tag(reader); uint length = reader.ReadUInt32();
                    long start = reader.BaseStream.Position;
                    if (length > bytes.Length - start) throw new ArgumentException("Truncated WAV");
                    if (tag == "fmt ")
                    {
                        if (hasFormat || length < 16 || reader.ReadUInt16() != 1) throw new ArgumentException("Expected PCM WAV");
                        hasFormat = true;
                        channels = reader.ReadUInt16(); frequency = reader.ReadInt32();
                        int byteRate = reader.ReadInt32(); align = reader.ReadUInt16();
                        int bits = reader.ReadUInt16();
                        if (channels < 1 || channels > 2 || frequency < 8000 || frequency > 48000
                            || bits != 16 || align != channels * 2 || byteRate != frequency * align)
                            throw new ArgumentException("Unsupported WAV format");
                    }
                    else if (tag == "data")
                    {
                        if (dataOffset >= 0) throw new ArgumentException("Duplicate WAV data");
                        dataOffset = (int)start; dataSize = (int)length;
                    }
                    reader.BaseStream.Position = start + length + (length % 2);
                }
                if (!hasFormat || dataOffset < 0 || dataSize <= 0 || dataSize % align != 0
                    || dataSize / align > frequency * 30 || dataSize / align < frequency / 10)
                    throw new ArgumentException("Invalid WAV duration");
                var samples = new float[dataSize / 2];
                reader.BaseStream.Position = dataOffset;
                for (int i = 0; i < samples.Length; i++) samples[i] = reader.ReadInt16() / 32768f;
                return new WaveData(samples, channels, frequency);
            }
        }

        private static string Tag(BinaryReader reader) { return Encoding.ASCII.GetString(reader.ReadBytes(4)); }
    }
}
