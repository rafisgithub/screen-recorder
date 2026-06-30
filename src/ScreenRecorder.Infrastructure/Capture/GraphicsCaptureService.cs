using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Infrastructure.Capture;

/// <summary>
/// Captures video via the Windows Graphics Capture API
/// (<c>Windows.Graphics.Capture</c> + Direct3D11 interop).
/// </summary>
/// <remarks>MILESTONE 3 — capture pipeline. Currently a scaffold stub.</remarks>
public sealed class GraphicsCaptureService : ICaptureService
{
    private readonly ILogger<GraphicsCaptureService> _logger;

    public GraphicsCaptureService(ILogger<GraphicsCaptureService> logger) => _logger = logger;

    public bool IsCapturing { get; private set; }

    public Resolution? SourceSize { get; private set; }

#pragma warning disable CS0067 // Wired up in Milestone 3.
    public event EventHandler<VideoFrame>? FrameArrived;
    public event EventHandler<Exception>? CaptureFailed;
#pragma warning restore CS0067

    public Task StartAsync(CaptureTarget target, RecordingSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        _logger.LogWarning("GraphicsCaptureService.StartAsync invoked before Milestone 3 is implemented.");
        throw new NotImplementedException("Windows Graphics Capture is implemented in Milestone 3.");
    }

    public Task StopAsync()
    {
        IsCapturing = false;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
