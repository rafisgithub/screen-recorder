namespace ScreenRecorder.Core.Models;

/// <summary>A connected display, as enumerated by <c>ICaptureTargetProvider</c>.</summary>
public sealed class MonitorInfo
{
    public string DeviceId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Native HMONITOR handle.</summary>
    public nint Handle { get; init; }

    /// <summary>Virtual-desktop pixel bounds.</summary>
    public PixelRect Bounds { get; init; }

    public bool IsPrimary { get; init; }

    /// <summary>DPI scale factor (1.0 == 96 DPI).</summary>
    public double ScaleFactor { get; init; } = 1.0;

    public CaptureTarget ToCaptureTarget() =>
        CaptureTarget.ForMonitor(DeviceId, DisplayName, Handle, Bounds);

    public override string ToString() => DisplayName;
}
