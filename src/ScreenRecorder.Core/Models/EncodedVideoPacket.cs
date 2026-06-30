namespace ScreenRecorder.Core.Models;

/// <summary>
/// A compressed video packet emitted by <c>IVideoEncoder</c> and consumed by
/// <c>IMediaWriter</c>. Kept codec-agnostic (raw bytes + timing) so the muxing
/// contract does not leak FFmpeg's <c>AVPacket</c> into Core.
/// </summary>
public sealed class EncodedVideoPacket
{
    /// <summary>Compressed bitstream for this packet.</summary>
    public ReadOnlyMemory<byte> Data { get; init; }

    /// <summary>Presentation timestamp.</summary>
    public TimeSpan PresentationTimestamp { get; init; }

    /// <summary>Decode timestamp (differs from PTS when B-frames are used).</summary>
    public TimeSpan DecodeTimestamp { get; init; }

    /// <summary>True for IDR/key frames.</summary>
    public bool IsKeyframe { get; init; }
}
