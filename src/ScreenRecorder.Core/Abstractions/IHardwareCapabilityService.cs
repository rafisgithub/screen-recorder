using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Core.Abstractions;

/// <summary>
/// Probes the machine for available hardware/software encoders (NVENC, Quick
/// Sync, AMF, x264/x265). Results are cached after the first probe.
/// </summary>
public interface IHardwareCapabilityService
{
    /// <summary>Returns cached capabilities, probing synchronously on first use.</summary>
    HardwareCapabilities Detect();

    Task<HardwareCapabilities> DetectAsync(CancellationToken cancellationToken = default);
}
