namespace ScreenRecorder.Core.Enums;

/// <summary>Whether an encoder runs on dedicated silicon or the CPU.</summary>
public enum EncoderKind
{
    /// <summary>CPU-based software encoder (e.g. libx264, libx265).</summary>
    Software = 0,

    /// <summary>GPU/fixed-function hardware encoder (NVENC, QSV, AMF).</summary>
    Hardware,
}
