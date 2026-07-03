using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;
using Windows.Graphics.Capture;

namespace ScreenRecorder.Infrastructure.Display;

/// <summary>
/// Enumerates monitors and windows via Win32, and shows the system
/// <see cref="GraphicsCapturePicker"/> for interactive selection.
/// </summary>
/// <remarks>MILESTONE 1 / 3 — target enumeration and picker.</remarks>
public sealed class CaptureTargetProvider : ICaptureTargetProvider
{
    private readonly ILogger<CaptureTargetProvider> _logger;

    public CaptureTargetProvider(ILogger<CaptureTargetProvider> logger) => _logger = logger;

    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();
        int index = 0;

        bool Callback(IntPtr hMonitor, IntPtr hdc, ref Win32Display.Rect rect, IntPtr data)
        {
            var info = new Win32Display.MonitorInfoEx { Size = Marshal.SizeOf<Win32Display.MonitorInfoEx>() };
            if (!Win32Display.GetMonitorInfo(hMonitor, ref info))
            {
                return true;
            }

            index++;
            bool isPrimary = (info.Flags & Win32Display.MonitorInfoPrimary) != 0;
            var bounds = new PixelRect(
                info.Monitor.Left, info.Monitor.Top, info.Monitor.Width, info.Monitor.Height);

            monitors.Add(new MonitorInfo
            {
                DeviceId = string.IsNullOrEmpty(info.Device) ? $"monitor-{index}" : info.Device,
                DisplayName = FormatMonitorName(index, bounds, isPrimary),
                Handle = hMonitor,
                Bounds = bounds,
                IsPrimary = isPrimary,
                ScaleFactor = GetScaleFactor(hMonitor),
            });

            return true;
        }

        try
        {
            Win32Display.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Monitor enumeration failed.");
        }

        return monitors;
    }

    public IReadOnlyList<CaptureTarget> GetWindows()
    {
        var windows = new List<CaptureTarget>();
        IntPtr self;
        using (var current = Process.GetCurrentProcess())
        {
            self = current.MainWindowHandle;
        }

        bool Callback(IntPtr hWnd, IntPtr param)
        {
            if (hWnd == self)
            {
                return true;
            }

            int titleLength = Win32Display.GetWindowTextLength(hWnd);
            long exStyle = Win32Display.GetWindowLongPtr(hWnd, Win32Display.GwlExStyle).ToInt64();
            bool cloaked = Win32Display.IsCloaked(hWnd);
            Win32Display.GetWindowRect(hWnd, out var rect);

            if (!WindowEligibility.IsEligible(
                    Win32Display.IsWindowVisible(hWnd), titleLength, exStyle, cloaked, rect.Width, rect.Height))
            {
                return true;
            }

            var title = new StringBuilder(titleLength + 1);
            Win32Display.GetWindowText(hWnd, title, title.Capacity);

            windows.Add(CaptureTarget.ForWindow(
                $"hwnd-{hWnd.ToInt64():X}",
                title.ToString(),
                hWnd,
                new PixelRect(rect.Left, rect.Top, rect.Width, rect.Height)));

            return true;
        }

        try
        {
            Win32Display.EnumWindows(Callback, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Window enumeration failed.");
        }

        return windows;
    }

    public MonitorInfo? GetPrimaryMonitor() => GetMonitors().FirstOrDefault(m => m.IsPrimary);

    public async Task<CaptureTarget?> PickTargetAsync(CancellationToken cancellationToken = default)
    {
        if (!GraphicsCaptureSession.IsSupported())
        {
            throw new NotSupportedException("The Graphics Capture picker is not available on this system.");
        }

        var picker = new GraphicsCapturePicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, GetOwnerWindow());

        var item = await picker.PickSingleItemAsync().AsTask(cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return null;
        }

        // The picker does not expose the native handle, so re-resolve it against
        // the enumerated targets to keep the capture flow handle-based.
        var resolved = ResolveByName(item.DisplayName);
        if (resolved is null)
        {
            _logger.LogWarning("Picked target '{Name}' could not be matched to a window or monitor.", item.DisplayName);
        }

        return resolved;
    }

    private CaptureTarget? ResolveByName(string displayName)
    {
        var window = GetWindows().FirstOrDefault(w =>
            string.Equals(w.DisplayName, displayName, StringComparison.Ordinal));
        if (window is not null)
        {
            return window;
        }

        var monitor = GetMonitors().FirstOrDefault(m =>
            m.DisplayName.Contains(displayName, StringComparison.OrdinalIgnoreCase));
        return monitor?.ToCaptureTarget();
    }

    private static IntPtr GetOwnerWindow()
    {
        IntPtr main;
        using (var current = Process.GetCurrentProcess())
        {
            main = current.MainWindowHandle;
        }

        return main != IntPtr.Zero ? main : Win32Display.GetForegroundWindow();
    }

    private static string FormatMonitorName(int index, PixelRect bounds, bool isPrimary)
    {
        string suffix = isPrimary ? " (Primary)" : string.Empty;
        return $"Display {index} — {bounds.Width}×{bounds.Height}{suffix}";
    }

    private double GetScaleFactor(IntPtr hMonitor)
    {
        try
        {
            if (Win32Display.GetDpiForMonitor(hMonitor, Win32Display.MdtEffectiveDpi, out uint dpiX, out _) == 0 && dpiX > 0)
            {
                return dpiX / 96.0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "DPI query failed for monitor; assuming 96 DPI.");
        }

        return 1.0;
    }
}
