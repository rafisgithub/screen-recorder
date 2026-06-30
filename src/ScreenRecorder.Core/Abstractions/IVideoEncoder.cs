using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Core.Abstractions;

/// <summary>
/// Encodes raw <see cref="VideoFrame"/>s into compressed packets. Implementations
/// wrap a specific FFmpeg encoder (e.g. h264_nvenc, libx264).
/// </summary>
public interface IVideoEncoder : IAsyncDisposable
{
    /// <summary>The encoder this instance represents.</summary>
    EncoderDescriptor Descriptor { get; }

    Task InitializeAsync(VideoSettings settings, Resolution sourceSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Encodes one frame. May return zero packets (the codec is buffering, e.g.
    /// B-frame reordering) or several.
    /// </summary>
    IReadOnlyList<EncodedVideoPacket> Encode(VideoFrame frame);

    /// <summary>Drains buffered packets at end-of-stream.</summary>
    IReadOnlyList<EncodedVideoPacket> Flush();
}
