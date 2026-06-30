using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Infrastructure.Encoding;

/// <summary>
/// Probes the machine for available encoders and caches the result.
/// </summary>
/// <remarks>
/// MILESTONE 2 — real detection. Today it returns the guaranteed software
/// encoders only. The full implementation will query the GPU/FFmpeg for
/// NVENC / Quick Sync / AMF availability and prepend the hardware encoders.
/// </remarks>
public sealed class HardwareCapabilityService : IHardwareCapabilityService
{
    private readonly ILogger<HardwareCapabilityService> _logger;
    private HardwareCapabilities? _cached;

    public HardwareCapabilityService(ILogger<HardwareCapabilityService> logger) => _logger = logger;

    public HardwareCapabilities Detect()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        // TODO (Milestone 2): probe NVENC/QSV/AMF and merge with software encoders.
        _logger.LogInformation("Hardware encoder probe not yet implemented; reporting software encoders only.");
        _cached = HardwareCapabilities.SoftwareOnly;
        return _cached;
    }

    public Task<HardwareCapabilities> DetectAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Detect());
}
