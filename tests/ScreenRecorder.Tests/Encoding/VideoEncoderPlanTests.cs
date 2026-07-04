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
    public void MediaFoundation_uses_nv12_and_quality_vbr_in_quality_mode()
    {
        var plan = VideoEncoderPlan.Build(EncoderDescriptor.H264Mf, Settings(RateControlMode.ConstantQuality, quality: 20));

        plan.UseNv12.Should().BeTrue();
        Value(plan, "rate_control").Should().Be("quality");
        // 20 on the 0..51 scale maps to Media Foundation quality 61 (higher = better).
        Value(plan, "quality").Should().Be("61");
        // A target bitrate is retained as a safety net if the MFT ignores quality mode.
        plan.BitRate.Should().Be(12_000_000);
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
        var plan = VideoEncoderPlan.Build(EncoderDescriptor.H264Mf, Settings(RateControlMode.VariableBitrate, bitrate: 12_000, maxBitrate: 20_000));

        plan.BitRate.Should().Be(12_000_000);
        plan.MaxBitRate.Should().Be(20_000_000);
    }

    [Fact]
    public void Quality_level_is_clamped_to_valid_range()
    {
        // 999 clamps to 51 on the FFmpeg scale, which maps to Media Foundation quality 1 (worst).
        var plan = VideoEncoderPlan.Build(EncoderDescriptor.H264Mf, Settings(RateControlMode.ConstantQuality, quality: 999));
        Value(plan, "quality").Should().Be("1");
    }
}
