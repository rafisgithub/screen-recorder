using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Core.Abstractions;

/// <summary>Enumerates WASAPI audio endpoints.</summary>
public interface IAudioDeviceProvider
{
    /// <summary>Render endpoints (speakers/headphones) available for loopback.</summary>
    IReadOnlyList<AudioDeviceInfo> GetRenderDevices();

    /// <summary>Capture endpoints (microphones/line-in).</summary>
    IReadOnlyList<AudioDeviceInfo> GetCaptureDevices();

    AudioDeviceInfo? GetDefaultRenderDevice();

    AudioDeviceInfo? GetDefaultCaptureDevice();
}
