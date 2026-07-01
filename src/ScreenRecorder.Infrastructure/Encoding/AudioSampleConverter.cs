using System.Runtime.InteropServices;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Infrastructure.Encoding;

/// <summary>
/// Pure conversion of a captured interleaved PCM buffer (32-bit float or 16-bit
/// signed) into interleaved 32-bit float at a target channel count. Channel count
/// is adapted by duplicating mono to all channels, averaging down to mono, or
/// copying/zero-padding otherwise. No resampling — callers ensure the sample rate
/// already matches the encoder. Extracted for unit testing.
/// </summary>
internal static class AudioSampleConverter
{
    public static float[] ToInterleavedFloat(ReadOnlySpan<byte> buffer, AudioFormat format, int targetChannels)
    {
        if (targetChannels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetChannels));
        }

        int sourceChannels = Math.Max(1, format.Channels);
        float[] mono;
        int frames;

        if (format.IsFloat && format.BitsPerSample == 32)
        {
            var samples = MemoryMarshal.Cast<byte, float>(buffer);
            frames = samples.Length / sourceChannels;
            return Remap(samples, sourceChannels, frames, targetChannels, static (s, i) => s[i]);
        }

        if (!format.IsFloat && format.BitsPerSample == 16)
        {
            var samples = MemoryMarshal.Cast<byte, short>(buffer);
            frames = samples.Length / sourceChannels;
            return Remap(samples, sourceChannels, frames, targetChannels, static (s, i) => s[i] / 32768f);
        }

        // Unsupported source layout — return silence sized to the input so timing holds.
        int bytesPerSample = Math.Max(1, format.BitsPerSample / 8);
        frames = buffer.Length / (bytesPerSample * sourceChannels);
        mono = new float[Math.Max(0, frames) * targetChannels];
        return mono;
    }

    private delegate float SampleReader<T>(ReadOnlySpan<T> source, int index);

    private static float[] Remap<T>(
        ReadOnlySpan<T> source, int sourceChannels, int frames, int targetChannels, SampleReader<T> read)
    {
        var output = new float[frames * targetChannels];

        for (int f = 0; f < frames; f++)
        {
            int srcBase = f * sourceChannels;
            int dstBase = f * targetChannels;

            if (sourceChannels == targetChannels)
            {
                for (int c = 0; c < targetChannels; c++)
                {
                    output[dstBase + c] = read(source, srcBase + c);
                }
            }
            else if (sourceChannels == 1)
            {
                float value = read(source, srcBase);
                for (int c = 0; c < targetChannels; c++)
                {
                    output[dstBase + c] = value;
                }
            }
            else if (targetChannels == 1)
            {
                float sum = 0f;
                for (int c = 0; c < sourceChannels; c++)
                {
                    sum += read(source, srcBase + c);
                }

                output[dstBase] = sum / sourceChannels;
            }
            else
            {
                int shared = Math.Min(sourceChannels, targetChannels);
                for (int c = 0; c < shared; c++)
                {
                    output[dstBase + c] = read(source, srcBase + c);
                }
            }
        }

        return output;
    }
}
