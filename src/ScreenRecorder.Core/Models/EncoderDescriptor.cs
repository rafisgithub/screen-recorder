using ScreenRecorder.Core.Enums;

namespace ScreenRecorder.Core.Models;

/// <summary>
/// Describes a concrete, selectable encoder — the bridge between the user's
/// codec choice and the FFmpeg encoder that will be instantiated.
/// </summary>
public sealed record EncoderDescriptor(
    VideoCodec Codec,
    EncoderKind Kind,
    HardwareVendor Vendor,
    string FFmpegEncoderName,
    string DisplayName)
{
    public bool IsHardware => Kind == EncoderKind.Hardware;

    public override string ToString() => DisplayName;

    // ---- Well-known descriptors (used by EncoderFactory and tests) ----

    public static EncoderDescriptor Libx264 { get; } =
        new(VideoCodec.H264, EncoderKind.Software, HardwareVendor.None, "libx264", "x264 — Software (CPU)");

    public static EncoderDescriptor Libx265 { get; } =
        new(VideoCodec.Hevc, EncoderKind.Software, HardwareVendor.None, "libx265", "x265 — Software (CPU)");

    public static EncoderDescriptor H264Nvenc { get; } =
        new(VideoCodec.H264, EncoderKind.Hardware, HardwareVendor.Nvidia, "h264_nvenc", "H.264 — NVIDIA NVENC");

    public static EncoderDescriptor HevcNvenc { get; } =
        new(VideoCodec.Hevc, EncoderKind.Hardware, HardwareVendor.Nvidia, "hevc_nvenc", "HEVC — NVIDIA NVENC");

    public static EncoderDescriptor H264Qsv { get; } =
        new(VideoCodec.H264, EncoderKind.Hardware, HardwareVendor.Intel, "h264_qsv", "H.264 — Intel Quick Sync");

    public static EncoderDescriptor HevcQsv { get; } =
        new(VideoCodec.Hevc, EncoderKind.Hardware, HardwareVendor.Intel, "hevc_qsv", "HEVC — Intel Quick Sync");

    public static EncoderDescriptor H264Amf { get; } =
        new(VideoCodec.H264, EncoderKind.Hardware, HardwareVendor.Amd, "h264_amf", "H.264 — AMD AMF");

    public static EncoderDescriptor HevcAmf { get; } =
        new(VideoCodec.Hevc, EncoderKind.Hardware, HardwareVendor.Amd, "hevc_amf", "HEVC — AMD AMF");
}
