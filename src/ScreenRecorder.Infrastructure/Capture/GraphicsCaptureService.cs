using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Enums;
using ScreenRecorder.Core.Models;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace ScreenRecorder.Infrastructure.Capture;

/// <summary>
/// Captures video via the Windows Graphics Capture API
/// (<c>Windows.Graphics.Capture</c>) and a Direct3D11 device. Each frame is
/// copied to a CPU-readable staging texture, mapped to a BGRA8 buffer, and
/// surfaced through <see cref="FrameArrived"/> for the duration of the callback.
/// </summary>
/// <remarks>
/// MILESTONE 3 — capture pipeline. The CPU-mapped transport is implemented; a
/// zero-copy GPU hand-off to a hardware encoder is a later optimization.
/// </remarks>
public sealed class GraphicsCaptureService : ICaptureService
{
    private const DirectXPixelFormat CaptureFormat = DirectXPixelFormat.B8G8R8A8UIntNormalized;
    private const int BufferCount = 2;

    private readonly ILogger<GraphicsCaptureService> _logger;
    private readonly object _sync = new();

    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDirect3DDevice? _winrtDevice;
    private GraphicsCaptureItem? _item;
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureSession? _session;
    private ID3D11Texture2D? _staging;
    private SizeInt32 _stagingSize;
    private TimeSpan? _firstFrameTime;
    private bool _disposed;

    public GraphicsCaptureService(ILogger<GraphicsCaptureService> logger) => _logger = logger;

    public bool IsCapturing { get; private set; }

    public Resolution? SourceSize { get; private set; }

    public event EventHandler<VideoFrame>? FrameArrived;

    public event EventHandler<Exception>? CaptureFailed;

    public Task StartAsync(CaptureTarget target, RecordingSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);

        if (!GraphicsCaptureSession.IsSupported())
        {
            throw new NotSupportedException(
                "Windows Graphics Capture is not available on this system (requires Windows 10 build 1903+).");
        }

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsCapturing)
            {
                throw new InvalidOperationException("Capture is already running.");
            }

            try
            {
                CreateDevice();
                _item = CreateItem(target);
                _stagingSize = _item.Size;
                CreateStaging(_stagingSize);
                SourceSize = new Resolution(_stagingSize.Width, _stagingSize.Height);

                _winrtDevice = CaptureInterop.CreateDirect3DDevice(_device!);
                _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                    _winrtDevice, CaptureFormat, BufferCount, _stagingSize);
                _framePool.FrameArrived += OnFrameArrived;

                _session = _framePool.CreateCaptureSession(_item);
                ApplySessionOptions(_session, settings);

                _item.Closed += OnItemClosed;

                IsCapturing = true;
                _session.StartCapture();

                _logger.LogInformation(
                    "Graphics capture started for '{Target}' at {Width}x{Height}.",
                    target.DisplayName, _stagingSize.Width, _stagingSize.Height);
            }
            catch
            {
                StopInternal();
                throw;
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        lock (_sync)
        {
            StopInternal();
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            StopInternal();
            _disposed = true;
        }

        return ValueTask.CompletedTask;
    }

    private void CreateDevice()
    {
        const DeviceCreationFlags flags = DeviceCreationFlags.BgraSupport;
        var featureLevels = new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0 };

        var result = D3D11.D3D11CreateDevice(null, DriverType.Hardware, flags, featureLevels, out ID3D11Device device);
        if (result.Failure)
        {
            // No hardware device (e.g. headless / RDP) — fall back to the software rasterizer.
            _logger.LogWarning("Hardware D3D11 device unavailable ({Code}); falling back to WARP.", result.Code);
            D3D11.D3D11CreateDevice(null, DriverType.Warp, flags, featureLevels, out device).CheckError();
        }

        _device = device;
        _context = device.ImmediateContext;
    }

    private static GraphicsCaptureItem CreateItem(CaptureTarget target) => target.Kind switch
    {
        CaptureTargetKind.Monitor => CaptureInterop.CreateItemForMonitor(target.Handle),
        CaptureTargetKind.Window => CaptureInterop.CreateItemForWindow(target.Handle),
        CaptureTargetKind.Region => throw new NotSupportedException(
            "Region capture is not implemented yet; capture a monitor or window."),
        _ => throw new NotSupportedException($"Unsupported capture target kind: {target.Kind}."),
    };

    private void CreateStaging(SizeInt32 size)
    {
        _staging?.Dispose();
        var description = new Texture2DDescription(
            Format.B8G8R8A8_UNorm,
            (uint)size.Width,
            (uint)size.Height,
            arraySize: 1,
            mipLevels: 1,
            bindFlags: BindFlags.None,
            usage: ResourceUsage.Staging,
            cpuAccessFlags: CpuAccessFlags.Read);
        _staging = _device!.CreateTexture2D(description);
        _stagingSize = size;
    }

    private void ApplySessionOptions(GraphicsCaptureSession session, RecordingSettings settings)
    {
        // Both properties are version-gated; tolerate older OSes gracefully.
        try
        {
            session.IsCursorCaptureEnabled = settings.CaptureCursor;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "IsCursorCaptureEnabled is not supported on this OS build.");
        }

        // IsBorderRequired (suppress the yellow capture border) is a Windows 11
        // API not present in the 19041 projection; left for a later SDK bump.
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        try
        {
            lock (_sync)
            {
                if (_disposed || !IsCapturing || _framePool is null)
                {
                    return;
                }

                using var frame = sender.TryGetNextFrame();
                if (frame is null)
                {
                    return;
                }

                var size = frame.ContentSize;
                if (size.Width <= 0 || size.Height <= 0)
                {
                    return;
                }

                if (size.Width != _stagingSize.Width || size.Height != _stagingSize.Height)
                {
                    CreateStaging(size);
                    SourceSize = new Resolution(size.Width, size.Height);
                    _framePool.Recreate(_winrtDevice, CaptureFormat, BufferCount, size);
                }

                using var texture = CaptureInterop.GetTexture(frame.Surface);
                _context!.CopyResource(_staging!, texture);

                var mapped = _context.Map(_staging!, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                try
                {
                    _firstFrameTime ??= frame.SystemRelativeTime;
                    var videoFrame = new VideoFrame
                    {
                        Size = new Resolution(size.Width, size.Height),
                        Stride = (int)mapped.RowPitch,
                        DataPointer = mapped.DataPointer,
                        DataLength = (int)mapped.RowPitch * size.Height,
                        Timestamp = frame.SystemRelativeTime - _firstFrameTime.Value,
                    };

                    FrameArrived?.Invoke(this, videoFrame);
                }
                finally
                {
                    _context.Unmap(_staging!, 0);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Graphics capture frame processing failed.");
            bool wasCapturing;
            lock (_sync)
            {
                wasCapturing = IsCapturing;
                IsCapturing = false;
            }

            if (wasCapturing)
            {
                CaptureFailed?.Invoke(this, ex);
            }
        }
    }

    private void OnItemClosed(GraphicsCaptureItem sender, object args)
    {
        _logger.LogWarning("Capture target was closed; stopping capture.");
        bool wasCapturing;
        lock (_sync)
        {
            wasCapturing = IsCapturing;
            StopInternal();
        }

        if (wasCapturing)
        {
            CaptureFailed?.Invoke(this, new InvalidOperationException("The capture target was closed."));
        }
    }

    private void StopInternal()
    {
        IsCapturing = false;

        if (_item is not null)
        {
            _item.Closed -= OnItemClosed;
        }

        if (_framePool is not null)
        {
            _framePool.FrameArrived -= OnFrameArrived;
        }

        _session?.Dispose();
        _session = null;
        _framePool?.Dispose();
        _framePool = null;
        _item = null;

        _staging?.Dispose();
        _staging = null;
        _winrtDevice?.Dispose();
        _winrtDevice = null;
        _context?.Dispose();
        _context = null;
        _device?.Dispose();
        _device = null;

        _firstFrameTime = null;
        _stagingSize = default;
        SourceSize = null;
    }
}
