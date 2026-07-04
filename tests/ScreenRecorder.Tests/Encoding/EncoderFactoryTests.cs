using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ScreenRecorder.Core.Enums;
using ScreenRecorder.Core.Models;
using ScreenRecorder.Infrastructure.Encoding;
using Xunit;

namespace ScreenRecorder.Tests.Encoding;

public class EncoderFactoryTests
{
    private static EncoderFactory CreateFactory() => new(NullLoggerFactory.Instance);

    [Fact]
    public void Selects_software_when_no_hardware_available()
    {
        var factory = CreateFactory();
        var settings = new VideoSettings { Codec = VideoCodec.H264, PreferHardwareEncoding = true };

        var chosen = factory.SelectVideoEncoder(settings, HardwareCapabilities.SoftwareOnly);

        chosen.Should().Be(EncoderDescriptor.H264Mf);
    }

    [Fact]
    public void Prefers_hardware_when_available_and_enabled()
    {
        var factory = CreateFactory();
        var capabilities = new HardwareCapabilities
        {
            VideoEncoders = new[] { EncoderDescriptor.H264Nvenc, EncoderDescriptor.H264Mf },
        };
        var settings = new VideoSettings { Codec = VideoCodec.H264, PreferHardwareEncoding = true };

        factory.SelectVideoEncoder(settings, capabilities).Should().Be(EncoderDescriptor.H264Nvenc);
    }

    [Fact]
    public void Uses_software_when_hardware_preference_disabled()
    {
        var factory = CreateFactory();
        var capabilities = new HardwareCapabilities
        {
            VideoEncoders = new[] { EncoderDescriptor.H264Nvenc, EncoderDescriptor.H264Mf },
        };
        var settings = new VideoSettings { Codec = VideoCodec.H264, PreferHardwareEncoding = false };

        factory.SelectVideoEncoder(settings, capabilities).Should().Be(EncoderDescriptor.H264Mf);
    }

    [Fact]
    public void Honors_explicit_preferred_vendor()
    {
        var factory = CreateFactory();
        var capabilities = new HardwareCapabilities
        {
            VideoEncoders = new[]
            {
                EncoderDescriptor.H264Nvenc,
                EncoderDescriptor.H264Qsv,
                EncoderDescriptor.H264Amf,
                EncoderDescriptor.H264Mf,
            },
        };
        var settings = new VideoSettings
        {
            Codec = VideoCodec.H264,
            PreferHardwareEncoding = true,
            PreferredVendor = HardwareVendor.Amd,
        };

        factory.SelectVideoEncoder(settings, capabilities).Should().Be(EncoderDescriptor.H264Amf);
    }

    [Fact]
    public void Falls_back_to_other_hardware_when_preferred_vendor_unavailable()
    {
        var factory = CreateFactory();
        var capabilities = new HardwareCapabilities
        {
            VideoEncoders = new[] { EncoderDescriptor.H264Qsv, EncoderDescriptor.H264Mf },
        };
        var settings = new VideoSettings
        {
            Codec = VideoCodec.H264,
            PreferHardwareEncoding = true,
            PreferredVendor = HardwareVendor.Nvidia,
        };

        factory.SelectVideoEncoder(settings, capabilities).Should().Be(EncoderDescriptor.H264Qsv);
    }

    [Fact]
    public void Selects_hevc_software_for_hevc_codec()
    {
        var factory = CreateFactory();
        var settings = new VideoSettings { Codec = VideoCodec.Hevc, PreferHardwareEncoding = true };

        factory.SelectVideoEncoder(settings, HardwareCapabilities.SoftwareOnly).Should().Be(EncoderDescriptor.HevcMf);
    }
}
