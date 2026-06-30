using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Infrastructure.Audio;

/// <summary>
/// Enumerates WASAPI endpoints via NAudio's <c>MMDeviceEnumerator</c>.
/// </summary>
/// <remarks>
/// MILESTONE 1 — device enumeration. Returns empty lists for now; the UI always
/// offers a "system default" option independent of enumeration.
/// </remarks>
public sealed class AudioDeviceProvider : IAudioDeviceProvider
{
    private readonly ILogger<AudioDeviceProvider> _logger;

    public AudioDeviceProvider(ILogger<AudioDeviceProvider> logger) => _logger = logger;

    public IReadOnlyList<AudioDeviceInfo> GetRenderDevices() => Array.Empty<AudioDeviceInfo>();

    public IReadOnlyList<AudioDeviceInfo> GetCaptureDevices() => Array.Empty<AudioDeviceInfo>();

    public AudioDeviceInfo? GetDefaultRenderDevice() => null;

    public AudioDeviceInfo? GetDefaultCaptureDevice() => null;
}
