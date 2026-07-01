using FluentAssertions;
using ScreenRecorder.Core.Enums;
using ScreenRecorder.Core.Models;
using ScreenRecorder.Infrastructure.Encoding;
using Xunit;

namespace ScreenRecorder.Tests.Encoding;

public class VideoEncoderPlanTests
{
    private static VideoSettings Settings(RateControlMode rc, int quality = 21, int bitrate = 12_000, int maxBitrate = 16_000)
        => new() { RateControl = rc, QualityLevel = quality, BitrateKbps = bitrate, MaxBitrateKbps = maxBitrate };

    private static string? Value(VideoEncoderPlan plan, string key)
        => plan.PrivateOptions.FirstOrDefault(o => o.Key == key).Value;

    [Fact]
    public void Software_uses_yuv420p_and_crf_in_quality_mode()
    {
        var plan = VideoEncoderPlan.Build(EncoderDescriptor.Libx264, Settings(RateControlMode.ConstantQuality, quality: 20));

        plan.UseNv12.Should().BeFalse();
        plan.BitRate.Should().Be(0);
        Value(plan, "crf").Should().Be("20");
        Value(plan, "preset").Should().Be("medium");
    }

    [Fact]
    public void Nvenc_uses_nv12_and_cq_in_quality_mode()
    {
        var plan = VideoEncoderPlan.Build(EncoderDescriptor.H264Nvenc, Settings(RateControlMode.ConstantQuality, quality: 19));

        plan.UseNv12.Should().BeTrue();
        plan.BitRate.Should().Be(0);
        Value(plan, "rc").Should().Be("vbr");
        Value(plan, "cq").Should().Be("19");
    }

    [Fact]
    public void Qsv_quality_mode_sets_global_quality()
    {
        var plan = VideoEncoderPlan.Build(EncoderDescriptor.H264Qsv, Settings(RateControlMode.ConstantQuality, quality: 23));

        plan.GlobalQuality.Should().Be(23);
        plan.UseNv12.Should().BeTrue();
    }

    [Fact]
    public void Constant_bitrate_sets_bitrate_and_caps_it()
    {
        var plan = VideoEncoderPlan.Build(EncoderDescriptor.H264Nvenc, Settings(RateControlMode.ConstantBitrate, bitrate: 10_000));

        plan.BitRate.Should().Be(10_000_000);
        plan.MaxBitRate.Should().Be(10_000_000);
        Value(plan, "rc").Should().Be("cbr");
    }

    [Fact]
    public void Variable_bitrate_sets_bitrate_and_ceiling()
    {
        var plan = VideoEncoderPlan.Build(EncoderDescriptor.Libx264, Settings(RateControlMode.VariableBitrate, bitrate: 12_000, maxBitrate: 20_000));

        plan.BitRate.Should().Be(12_000_000);
        plan.MaxBitRate.Should().Be(20_000_000);
    }

    [Fact]
    public void Quality_level_is_clamped_to_valid_range()
    {
        var plan = VideoEncoderPlan.Build(EncoderDescriptor.Libx264, Settings(RateControlMode.ConstantQuality, quality: 999));
        Value(plan, "crf").Should().Be("51");
    }
}
