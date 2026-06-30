namespace ScreenRecorder.Core.Models;

/// <summary>
/// A compressed (AAC) audio packet emitted by <c>IAudioEncoder</c> and consumed
/// by <c>IMediaWriter</c>.
/// </summary>
public sealed class EncodedAudioPacket
{
    /// <summary>Compressed bitstream for this packet.</summary>
    public ReadOnlyMemory<byte> Data { get; init; }

    /// <summary>Presentation timestamp.</summary>
    public TimeSpan PresentationTimestamp { get; init; }
}
