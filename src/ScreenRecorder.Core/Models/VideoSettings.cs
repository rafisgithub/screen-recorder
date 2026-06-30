using ScreenRecorder.Core.Enums;

namespace ScreenRecorder.Core.Models;

/// <summary>
/// User-configurable video encoding parameters. Plain mutable POCO so it can be
/// edited by the settings view model and serialized by the settings service.
/// </summary>
public sealed class VideoSettings
{
    /// <summary>Output resolution. Defaults to 1080p.</summary>
    public Resolution Resolution { get; set; } = Resolution.FullHd1080;

    /// <summary>Target frame rate in frames per second. Defaults to 60.</summary>
    public int FrameRate { get; set; } = 60;

    /// <summary>Video codec for the MP4 stream.</summary>
    public VideoCodec Codec { get; set; } = VideoCodec.H264;

    /// <summary>How the encoder distributes bits.</summary>
    public RateControlMode RateControl { get; set; } = RateControlMode.ConstantQuality;

    /// <summary>
    /// Quality level for <see cref="RateControlMode.ConstantQuality"/>
    /// (lower = better; ~18–23 is the visually-lossless range). Default 21.
    /// </summary>
    public int QualityLevel { get; set; } = 21;

    /// <summary>Target bitrate (kbps) for CBR/VBR. Default 12 Mbps (1080p60).</summary>
    public int BitrateKbps { get; set; } = 12_000;

    /// <summary>Ceiling bitrate (kbps) for VBR.</summary>
    public int MaxBitrateKbps { get; set; } = 16_000;

    /// <summary>Seconds between keyframes (GOP length). YouTube prefers ~2s.</summary>
    public double KeyframeIntervalSeconds { get; set; } = 2.0;

    /// <summary>Prefer a GPU encoder (NVENC/QSV/AMF) when available.</summary>
    public bool PreferHardwareEncoding { get; set; } = true;

    /// <summary>
    /// Force a specific GPU vendor's encoder; <see cref="HardwareVendor.None"/>
    /// lets <c>IEncoderFactory</c> auto-select the best available.
    /// </summary>
    public HardwareVendor PreferredVendor { get; set; } = HardwareVendor.None;

    public VideoSettings Clone() => (VideoSettings)MemberwiseClone();
}
