using System.Runtime.InteropServices;
using FluentAssertions;
using ScreenRecorder.Core.Models;
using ScreenRecorder.Infrastructure.Encoding;
using Xunit;

namespace ScreenRecorder.Tests.Encoding;

public class AudioSampleConverterTests
{
    private static byte[] FloatBytes(params float[] samples)
    {
        var bytes = new byte[samples.Length * 4];
        MemoryMarshal.Cast<float, byte>(samples).CopyTo(bytes);
        return bytes;
    }

    private static byte[] ShortBytes(params short[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        MemoryMarshal.Cast<short, byte>(samples).CopyTo(bytes);
        return bytes;
    }

    [Fact]
    public void Float_stereo_passthrough_preserves_values()
    {
        var format = new AudioFormat(48_000, 2, 32, IsFloat: true);
        var bytes = FloatBytes(0.25f, -0.5f, 1.0f, -1.0f);

        var result = AudioSampleConverter.ToInterleavedFloat(bytes, format, targetChannels: 2);

        result.Should().Equal(0.25f, -0.5f, 1.0f, -1.0f);
    }

    [Fact]
    public void Int16_is_normalized_to_unit_float()
    {
        var format = new AudioFormat(48_000, 1, 16, IsFloat: false);
        var bytes = ShortBytes(16384, -32768);

        var result = AudioSampleConverter.ToInterleavedFloat(bytes, format, targetChannels: 1);

        result.Should().HaveCount(2);
        result[0].Should().BeApproximately(0.5f, 1e-4f);
        result[1].Should().BeApproximately(-1.0f, 1e-4f);
    }

    [Fact]
    public void Mono_is_duplicated_to_stereo()
    {
        var format = new AudioFormat(48_000, 1, 32, IsFloat: true);
        var bytes = FloatBytes(0.3f, -0.7f);

        var result = AudioSampleConverter.ToInterleavedFloat(bytes, format, targetChannels: 2);

        result.Should().Equal(0.3f, 0.3f, -0.7f, -0.7f);
    }

    [Fact]
    public void Stereo_is_averaged_down_to_mono()
    {
        var format = new AudioFormat(48_000, 2, 32, IsFloat: true);
        var bytes = FloatBytes(0.4f, 0.6f, -0.2f, -0.4f);

        var result = AudioSampleConverter.ToInterleavedFloat(bytes, format, targetChannels: 1);

        result.Should().HaveCount(2);
        result[0].Should().BeApproximately(0.5f, 1e-4f);
        result[1].Should().BeApproximately(-0.3f, 1e-4f);
    }
}
