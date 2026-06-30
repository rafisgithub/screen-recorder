namespace ScreenRecorder.Core.Enums;

/// <summary>Direction of an audio endpoint.</summary>
public enum AudioDeviceKind
{
    /// <summary>Playback endpoint (speakers/headphones) — captured via loopback.</summary>
    Render = 0,

    /// <summary>Recording endpoint (microphone/line-in).</summary>
    Capture,
}
