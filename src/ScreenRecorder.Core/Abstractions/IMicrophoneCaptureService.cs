namespace ScreenRecorder.Core.Abstractions;

/// <summary>
/// Captures microphone/line-in audio via WASAPI on a capture endpoint.
/// Marker specialization of <see cref="IAudioCaptureService"/> for DI resolution.
/// </summary>
public interface IMicrophoneCaptureService : IAudioCaptureService
{
}
