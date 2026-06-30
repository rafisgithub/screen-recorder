namespace ScreenRecorder.Core.Models;

/// <summary>
/// User-configurable audio capture and encoding parameters.
/// </summary>
public sealed class AudioSettings
{
    /// <summary>Capture desktop/system audio via WASAPI loopback.</summary>
    public bool CaptureSystemAudio { get; set; } = true;

    /// <summary>Capture the microphone via WASAPI.</summary>
    public bool CaptureMicrophone { get; set; }

    /// <summary>
    /// Render endpoint id to loop back; <c>null</c> = default playback device.
    /// </summary>
    public string? SystemAudioDeviceId { get; set; }

    /// <summary>Capture endpoint id; <c>null</c> = default recording device.</summary>
    public string? MicrophoneDeviceId { get; set; }

    /// <summary>AAC bitrate in kbps. 192–256 is high quality for stereo.</summary>
    public int BitrateKbps { get; set; } = 192;

    /// <summary>Output sample rate in Hz. 48 kHz is the YouTube standard.</summary>
    public int SampleRate { get; set; } = 48_000;

    /// <summary>Output channel count (2 = stereo).</summary>
    public int Channels { get; set; } = 2;

    /// <summary>Linear gain applied to the system mix (1.0 = unity).</summary>
    public double SystemGain { get; set; } = 1.0;

    /// <summary>Linear gain applied to the microphone (1.0 = unity).</summary>
    public double MicrophoneGain { get; set; } = 1.0;

    /// <summary>True when at least one audio source is enabled.</summary>
    public bool HasAnySource => CaptureSystemAudio || CaptureMicrophone;

    public AudioSettings Clone() => (AudioSettings)MemberwiseClone();
}
