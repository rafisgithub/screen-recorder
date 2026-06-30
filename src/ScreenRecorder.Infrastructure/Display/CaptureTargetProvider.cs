using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Infrastructure.Display;

/// <summary>
/// Enumerates monitors and windows, and shows the system Graphics Capture picker.
/// </summary>
/// <remarks>
/// MILESTONE 1 / 3 — target enumeration and picker. Currently a scaffold stub.
/// </remarks>
public sealed class CaptureTargetProvider : ICaptureTargetProvider
{
    private readonly ILogger<CaptureTargetProvider> _logger;

    public CaptureTargetProvider(ILogger<CaptureTargetProvider> logger) => _logger = logger;

    public IReadOnlyList<MonitorInfo> GetMonitors() => Array.Empty<MonitorInfo>();

    public IReadOnlyList<CaptureTarget> GetWindows() => Array.Empty<CaptureTarget>();

    public MonitorInfo? GetPrimaryMonitor() => null;

    public Task<CaptureTarget?> PickTargetAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("CaptureTargetProvider.PickTargetAsync invoked before the picker is implemented.");
        throw new NotImplementedException("The Graphics Capture picker is implemented in Milestone 1/3.");
    }
}
