namespace ScreenRecorder.Core.Abstractions;

/// <summary>
/// Captures system/desktop audio via WASAPI <b>loopback</b> on a render endpoint.
/// Marker specialization of <see cref="IAudioCaptureService"/> for DI resolution.
/// </summary>
public interface ISystemAudioCaptureService : IAudioCaptureService
{
}
