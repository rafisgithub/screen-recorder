using ScreenRecorder.Core.Enums;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Infrastructure.Encoding;

/// <summary>
/// Pure translation of an <see cref="EncoderDescriptor"/> + <see cref="VideoSettings"/>
/// into the concrete knobs an FFmpeg codec context needs (pixel format, bit-rate,
/// and private <c>av_opt_set</c> options). Free of any FFmpeg/native types so the
/// rate-control mapping is fully unit-testable.
/// </summary>
public readonly record struct VideoEncoderPlan(
    bool UseNv12,
    long BitRate,
    long MaxBitRate,
    int? GlobalQuality,
    IReadOnlyList<KeyValuePair<string, string>> PrivateOptions)
{
    /// <summary>Encoder "family" derived from the ffmpeg encoder name.</summary>
    private enum Family
    {
        Software,
        Nvenc,
        Qsv,
        Amf,
        MediaFoundation,
    }

    public static VideoEncoderPlan Build(EncoderDescriptor descriptor, VideoSettings settings)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(settings);

        var family = Classify(descriptor.FFmpegEncoderName);
        // Hardware encoders and Media Foundation take NV12; a generic software
        // encoder would take YUV420P.
        bool useNv12 = family != Family.Software;
        var options = new List<KeyValuePair<string, string>>();

        long bitRate = 0;
        long maxBitRate = 0;
        int? globalQuality = null;

        int quality = Math.Clamp(settings.QualityLevel, 0, 51);
        long targetBps = Math.Max(1, settings.BitrateKbps) * 1000L;
        long ceilingBps = Math.Max(settings.BitrateKbps, settings.MaxBitrateKbps) * 1000L;

        switch (settings.RateControl)
        {
            case RateControlMode.ConstantQuality:
                switch (family)
                {
                    case Family.Nvenc:
                        Add(options, "preset", "p5");
                        Add(options, "tune", "hq");
                        Add(options, "rc", "vbr");
                        Add(options, "cq", quality.ToString());
                        break;
                    case Family.Qsv:
                        Add(options, "preset", "medium");
                        globalQuality = quality; // ICQ
                        break;
                    case Family.Amf:
                        Add(options, "quality", "balanced");
                        Add(options, "rc", "cqp");
                        Add(options, "qp_i", quality.ToString());
                        Add(options, "qp_p", quality.ToString());
                        Add(options, "qp_b", quality.ToString());
                        break;
                    case Family.MediaFoundation:
                        // MF has no CRF; use quality-based VBR (higher = better),
                        // keeping a target bitrate as a safety net if the MFT
                        // ignores the quality knob.
                        Add(options, "rate_control", "quality");
                        Add(options, "quality", MediaFoundationQuality(quality).ToString());
                        bitRate = targetBps;
                        break;
                    default:
                        Add(options, "preset", "medium");
                        Add(options, "crf", quality.ToString());
                        break;
                }

                break;

            case RateControlMode.ConstantBitrate:
                bitRate = targetBps;
                maxBitRate = targetBps;
                switch (family)
                {
                    case Family.Nvenc:
                        Add(options, "preset", "p5");
                        Add(options, "rc", "cbr");
                        break;
                    case Family.Qsv:
                        Add(options, "preset", "medium");
                        break;
                    case Family.Amf:
                        Add(options, "quality", "balanced");
                        Add(options, "rc", "cbr");
                        break;
                    case Family.MediaFoundation:
                        Add(options, "rate_control", "cbr");
                        break;
                    default:
                        Add(options, "preset", "medium");
                        Add(options, "nal-hrd", "cbr");
                        break;
                }

                break;

            case RateControlMode.VariableBitrate:
            default:
                bitRate = targetBps;
                maxBitRate = ceilingBps;
                switch (family)
                {
                    case Family.Nvenc:
                        Add(options, "preset", "p5");
                        Add(options, "rc", "vbr");
                        break;
                    case Family.Qsv:
                        Add(options, "preset", "medium");
                        break;
                    case Family.Amf:
                        Add(options, "quality", "balanced");
                        Add(options, "rc", "vbr_peak");
                        break;
                    case Family.MediaFoundation:
                        // Peak-constrained VBR uses rc_max_rate (set from maxBitRate).
                        Add(options, "rate_control", "pc_vbr");
                        break;
                    default:
                        Add(options, "preset", "medium");
                        break;
                }

                break;
        }

        return new VideoEncoderPlan(useNv12, bitRate, maxBitRate, globalQuality, options);
    }

    private static Family Classify(string encoderName)
    {
        if (encoderName.Contains("nvenc", StringComparison.OrdinalIgnoreCase))
        {
            return Family.Nvenc;
        }

        if (encoderName.Contains("qsv", StringComparison.OrdinalIgnoreCase))
        {
            return Family.Qsv;
        }

        if (encoderName.Contains("amf", StringComparison.OrdinalIgnoreCase))
        {
            return Family.Amf;
        }

        if (encoderName.Contains("_mf", StringComparison.OrdinalIgnoreCase))
        {
            return Family.MediaFoundation;
        }

        return Family.Software;
    }

    private static void Add(List<KeyValuePair<string, string>> options, string key, string value)
        => options.Add(new KeyValuePair<string, string>(key, value));

    // FFmpeg 0..51 quality (lower = better) -> Media Foundation 0..100 (higher = better).
    private static int MediaFoundationQuality(int quality)
        => Math.Clamp(100 - (int)Math.Round(quality / 51.0 * 100.0), 1, 100);
}
