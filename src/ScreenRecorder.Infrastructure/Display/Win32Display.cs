using System.Runtime.InteropServices;
using System.Text;

namespace ScreenRecorder.Infrastructure.Display;

/// <summary>
/// Native Win32 entry points used to enumerate monitors and top-level windows.
/// Kept internal so the platform surface stays inside the Infrastructure layer.
/// </summary>
internal static class Win32Display
{
    public const uint MonitorInfoPrimary = 0x1;
    public const int GwlExStyle = -20;
    public const int WsExToolWindow = 0x00000080;
    public const int DwmwaCloaked = 14;
    public const int MdtEffectiveDpi = 0;

    public delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref Rect rect, IntPtr data);

    public delegate bool EnumWindowsProc(IntPtr window, IntPtr param);

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly int Width => Right - Left;

        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MonitorInfoEx
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string Device;
    }

    [DllImport("user32.dll")]
    public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr param);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowTextLength(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);

    [DllImport("shcore.dll")]
    public static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

    public static bool IsCloaked(IntPtr window)
    {
        // Filters out the hidden UWP/"ghost" windows that EnumWindows still reports.
        if (DwmGetWindowAttribute(window, DwmwaCloaked, out int cloaked, sizeof(int)) == 0)
        {
            return cloaked != 0;
        }

        return false;
    }
}

/// <summary>
/// Pure decision logic for whether a top-level window is a sensible capture
/// target. Extracted from <see cref="CaptureTargetProvider"/> so it is unit
/// testable without a live desktop.
/// </summary>
internal static class WindowEligibility
{
    /// <summary>
    /// A window is offered as a capture target when it is visible, titled, not a
    /// tool window, not DWM-cloaked, and has a non-empty client area.
    /// </summary>
    public static bool IsEligible(bool isVisible, int titleLength, long exStyle, bool isCloaked, int width, int height)
        => isVisible
           && !isCloaked
           && titleLength > 0
           && (exStyle & Win32Display.WsExToolWindow) == 0
           && width > 0
           && height > 0;
}
