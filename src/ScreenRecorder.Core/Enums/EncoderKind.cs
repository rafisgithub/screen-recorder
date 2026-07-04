namespace ScreenRecorder.Core.Enums;

/// <summary>Whether an encoder runs on dedicated silicon or the CPU.</summary>
public enum EncoderKind
{
    /// <summary>CPU/OS software encoder (e.g. Media Foundation h264_mf/hevc_mf).</summary>
    Software = 0,

    /// <summary>GPU/fixed-function hardware encoder (NVENC, QSV, AMF).</summary>
    Hardware,
}
