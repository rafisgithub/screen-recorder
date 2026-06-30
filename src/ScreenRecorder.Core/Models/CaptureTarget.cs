using ScreenRecorder.Core.Enums;

namespace ScreenRecorder.Core.Models;

/// <summary>
/// Identifies what the video pipeline captures — a monitor, a window, or a
/// region. Carries the native handle the Graphics Capture layer needs.
/// </summary>
public sealed class CaptureTarget
{
    public CaptureTargetKind Kind { get; init; }

    /// <summary>Stable identifier (monitor device id or window handle string).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Human-friendly label shown in the UI.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>HWND for a window, or HMONITOR for a monitor/region.</summary>
    public nint Handle { get; init; }

    /// <summary>Pixel bounds (full monitor/window, or the selected region).</summary>
    public PixelRect Bounds { get; init; }

    public static CaptureTarget ForMonitor(string id, string name, nint hMonitor, PixelRect bounds) => new()
    {
        Kind = CaptureTargetKind.Monitor,
        Id = id,
        DisplayName = name,
        Handle = hMonitor,
        Bounds = bounds,
    };

    public static CaptureTarget ForWindow(string id, string title, nint hWnd, PixelRect bounds) => new()
    {
        Kind = CaptureTargetKind.Window,
        Id = id,
        DisplayName = title,
        Handle = hWnd,
        Bounds = bounds,
    };

    public static CaptureTarget ForRegion(string id, string name, nint hMonitor, PixelRect region) => new()
    {
        Kind = CaptureTargetKind.Region,
        Id = id,
        DisplayName = name,
        Handle = hMonitor,
        Bounds = region,
    };

    public override string ToString() => DisplayName;
}
