using ScreenRecorder.Core.Enums;

namespace ScreenRecorder.Core.Models;

/// <summary>An audio endpoint, as enumerated by <c>IAudioDeviceProvider</c>.</summary>
public sealed class AudioDeviceInfo
{
    /// <summary>Endpoint id (WASAPI device id), stable across sessions.</summary>
    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public AudioDeviceKind Kind { get; init; }

    /// <summary>True if this is the system default endpoint for its direction.</summary>
    public bool IsDefault { get; init; }

    public override string ToString() => IsDefault ? $"{DisplayName} (default)" : DisplayName;
}
