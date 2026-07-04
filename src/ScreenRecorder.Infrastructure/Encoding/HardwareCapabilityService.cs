using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Infrastructure.Encoding;

/// <summary>
/// Probes the machine for usable encoders via FFmpeg and caches the result.
/// Hardware encoders (NVENC/Quick Sync/AMF) are confirmed by actually opening a
/// small codec context — the only reliable test that the GPU/driver is present.
/// Falls back to the guaranteed software encoders when FFmpeg is unavailable.
/// </summary>
/// <remarks>MILESTONE 2 — hardware detection.</remarks>
public sealed class HardwareCapabilityService : IHardwareCapabilityService
{
    private static readonly EncoderDescriptor[] HardwareCandidates =
    {
        EncoderDescriptor.H264Nvenc,
        EncoderDescriptor.HevcNvenc,
        EncoderDescriptor.H264Qsv,
        EncoderDescriptor.HevcQsv,
        EncoderDescriptor.H264Amf,
        EncoderDescriptor.HevcAmf,
    };

    private readonly ILogger<HardwareCapabilityService> _logger;
    private readonly object _sync = new();
    private HardwareCapabilities? _cached;

    public HardwareCapabilityService(ILogger<HardwareCapabilityService> logger) => _logger = logger;

    public HardwareCapabilities Detect()
    {
        lock (_sync)
        {
            if (_cached is not null)
            {
                return _cached;
            }

            if (!FFmpegInterop.TryInitialize(_logger))
            {
                _logger.LogInformation("FFmpeg unavailable; reporting software encoders only.");
                _cached = HardwareCapabilities.SoftwareOnly;
                return _cached;
            }

            var encoders = new List<EncoderDescriptor>();
            foreach (var candidate in HardwareCandidates)
            {
                if (FFmpegInterop.EncoderUsable(candidate.FFmpegEncoderName))
                {
                    encoders.Add(candidate);
                    _logger.LogInformation("Hardware encoder available: {Encoder}.", candidate.DisplayName);
                }
            }

            // Media Foundation software encoders are the universal fallback so the
            // factory always has an option for each codec even with no GPU encoder.
            // (The LGPL FFmpeg build has no libx264/libx265; MF ships with Windows.)
            AddIfCompiledIn(encoders, EncoderDescriptor.H264Mf);
            AddIfCompiledIn(encoders, EncoderDescriptor.HevcMf);

            if (encoders.Count == 0)
            {
                _logger.LogWarning("No encoders detected in this FFmpeg build; using software defaults.");
                _cached = HardwareCapabilities.SoftwareOnly;
                return _cached;
            }

            _cached = new HardwareCapabilities { VideoEncoders = encoders };
            _logger.LogInformation(
                "Encoder probe complete: {Count} encoder(s); hardware acceleration {State}.",
                encoders.Count, _cached.HasHardwareEncoder ? "available" : "unavailable");
            return _cached;
        }
    }

    public Task<HardwareCapabilities> DetectAsync(CancellationToken cancellationToken = default) =>
        Task.Run(Detect, cancellationToken);

    private void AddIfCompiledIn(List<EncoderDescriptor> encoders, EncoderDescriptor descriptor)
    {
        if (FFmpegInterop.EncoderCompiledIn(descriptor.FFmpegEncoderName))
        {
            encoders.Add(descriptor);
        }
        else
        {
            _logger.LogDebug("Software encoder {Encoder} is not compiled into this FFmpeg build.", descriptor.DisplayName);
        }
    }
}
