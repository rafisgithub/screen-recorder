using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Enums;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Infrastructure.Encoding;

/// <summary>
/// Picks the best encoder for the requested settings and instantiates the
/// FFmpeg-backed encoders.
/// </summary>
/// <remarks>
/// <see cref="SelectVideoEncoder"/> is pure decision logic with no native
/// dependencies, so it is fully unit-testable. The <c>Create*</c> methods build
/// the concrete FFmpeg encoders (implemented in the encoding milestone).
/// </remarks>
public sealed class EncoderFactory : IEncoderFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public EncoderFactory(ILoggerFactory loggerFactory) => _loggerFactory = loggerFactory;

    public EncoderDescriptor SelectVideoEncoder(VideoSettings settings, HardwareCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(capabilities);

        var codec = settings.Codec;

        if (settings.PreferHardwareEncoding)
        {
            // 1) Honor an explicit vendor preference if that exact encoder exists.
            if (settings.PreferredVendor != HardwareVendor.None)
            {
                var preferred = capabilities.VideoEncoders.FirstOrDefault(
                    e => e.IsHardware && e.Codec == codec && e.Vendor == settings.PreferredVendor);
                if (preferred is not null)
                {
                    return preferred;
                }
            }

            // 2) Otherwise the best available hardware encoder for this codec.
            var hardware = capabilities.ForCodec(codec)
                .Where(e => e.IsHardware)
                .OrderBy(e => VendorPriority(e.Vendor))
                .FirstOrDefault();
            if (hardware is not null)
            {
                return hardware;
            }
        }

        // 3) Software encoder for this codec.
        var software = capabilities.ForCodec(codec).FirstOrDefault(e => !e.IsHardware);
        if (software is not null)
        {
            return software;
        }

        // 4) Absolute fallback — always available.
        return codec == VideoCodec.Hevc ? EncoderDescriptor.Libx265 : EncoderDescriptor.Libx264;
    }

    public IVideoEncoder CreateVideoEncoder(EncoderDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return new FFmpegVideoEncoder(descriptor, _loggerFactory.CreateLogger<FFmpegVideoEncoder>());
    }

    public IAudioEncoder CreateAudioEncoder(AudioSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new FFmpegAudioEncoder(_loggerFactory.CreateLogger<FFmpegAudioEncoder>());
    }

    private static int VendorPriority(HardwareVendor vendor) => vendor switch
    {
        HardwareVendor.Nvidia => 0,
        HardwareVendor.Intel => 1,
        HardwareVendor.Amd => 2,
        _ => 3,
    };
}
