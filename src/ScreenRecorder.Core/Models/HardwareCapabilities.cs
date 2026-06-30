using ScreenRecorder.Core.Enums;

namespace ScreenRecorder.Core.Models;

/// <summary>
/// The set of encoders available on this machine, as probed by
/// <c>IHardwareCapabilityService</c>. Always includes the software encoders as
/// a guaranteed fallback.
/// </summary>
public sealed class HardwareCapabilities
{
    /// <summary>All usable video encoders, best-first.</summary>
    public IReadOnlyList<EncoderDescriptor> VideoEncoders { get; init; } = Array.Empty<EncoderDescriptor>();

    public bool HasHardwareEncoder => VideoEncoders.Any(e => e.IsHardware);

    public bool Supports(VideoCodec codec, HardwareVendor vendor) =>
        VideoEncoders.Any(e => e.Codec == codec && e.Vendor == vendor);

    public IEnumerable<EncoderDescriptor> ForCodec(VideoCodec codec) =>
        VideoEncoders.Where(e => e.Codec == codec);

    /// <summary>Software-only capabilities — the universal fallback.</summary>
    public static HardwareCapabilities SoftwareOnly { get; } = new()
    {
        VideoEncoders = new[] { EncoderDescriptor.Libx264, EncoderDescriptor.Libx265 },
    };
}
