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
    //
    // The software fallbacks use Windows Media Foundation (h264_mf / hevc_mf),
    // not libx264/libx265. The bundled FFmpeg is an LGPL build (Store-safe;
    // libx264/libx265 are GPL and are not present). Media Foundation ships with
    // Windows and provides an OS-level H.264/HEVC encoder, so machines with no
    // NVENC/QSV/AMF hardware encoder can still record H.264/HEVC.

    public static EncoderDescriptor H264Mf { get; } =
        new(VideoCodec.H264, EncoderKind.Software, HardwareVendor.None, "h264_mf", "H.264 — Media Foundation");

    public static EncoderDescriptor HevcMf { get; } =
        new(VideoCodec.Hevc, EncoderKind.Software, HardwareVendor.None, "hevc_mf", "HEVC — Media Foundation");

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
