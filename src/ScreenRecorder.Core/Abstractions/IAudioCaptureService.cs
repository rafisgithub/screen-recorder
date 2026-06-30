using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Core.Abstractions;

/// <summary>
/// Common contract for an audio source. Concrete sources are distinguished by
/// the marker interfaces <see cref="ISystemAudioCaptureService"/> and
/// <see cref="IMicrophoneCaptureService"/> so DI can resolve each independently.
/// </summary>
public interface IAudioCaptureService : IAsyncDisposable
{
    bool IsCapturing { get; }

    /// <summary>The PCM format this source emits once started.</summary>
    AudioFormat Format { get; }

    /// <summary>Raised for each buffer of captured PCM audio.</summary>
    event EventHandler<AudioFrame>? DataAvailable;

    /// <summary>Raised when capture terminates abnormally.</summary>
    event EventHandler<Exception>? CaptureFailed;

    /// <param name="deviceId">Endpoint id, or <c>null</c> for the system default.</param>
    Task StartAsync(string? deviceId, CancellationToken cancellationToken = default);

    Task StopAsync();
}
