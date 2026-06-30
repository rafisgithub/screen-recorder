using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Core.Abstractions;

/// <summary>Encodes raw <see cref="AudioFrame"/>s into compressed AAC packets.</summary>
public interface IAudioEncoder : IAsyncDisposable
{
    Task InitializeAsync(AudioSettings settings, AudioFormat sourceFormat, CancellationToken cancellationToken = default);

    /// <summary>Encodes a buffer of PCM audio; may return zero or several packets.</summary>
    IReadOnlyList<EncodedAudioPacket> Encode(AudioFrame frame);

    /// <summary>Drains buffered packets at end-of-stream.</summary>
    IReadOnlyList<EncodedAudioPacket> Flush();
}
