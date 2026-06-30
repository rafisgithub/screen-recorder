using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Core.Abstractions;

/// <summary>
/// Captures video frames from a <see cref="CaptureTarget"/> using the Windows
/// Graphics Capture API and raises <see cref="FrameArrived"/> for each frame.
/// </summary>
public interface ICaptureService : IAsyncDisposable
{
    bool IsCapturing { get; }

    /// <summary>Native size of the capture source, once capture has started.</summary>
    Resolution? SourceSize { get; }

    /// <summary>
    /// Raised for every captured frame. Handlers run on a capture-pool thread and
    /// must not block; the <see cref="VideoFrame"/> is only valid for the call.
    /// </summary>
    event EventHandler<VideoFrame>? FrameArrived;

    /// <summary>Raised when capture terminates abnormally.</summary>
    event EventHandler<Exception>? CaptureFailed;

    Task StartAsync(CaptureTarget target, RecordingSettings settings, CancellationToken cancellationToken = default);

    Task StopAsync();
}
