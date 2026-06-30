using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Core.Abstractions;

/// <summary>Enumerates capturable monitors and windows.</summary>
public interface ICaptureTargetProvider
{
    IReadOnlyList<MonitorInfo> GetMonitors();

    /// <summary>Top-level, capturable application windows.</summary>
    IReadOnlyList<CaptureTarget> GetWindows();

    MonitorInfo? GetPrimaryMonitor();

    /// <summary>
    /// Shows the built-in Windows <c>GraphicsCapturePicker</c> so the user can
    /// pick a window/display interactively. Returns <c>null</c> if cancelled.
    /// </summary>
    Task<CaptureTarget?> PickTargetAsync(CancellationToken cancellationToken = default);
}
