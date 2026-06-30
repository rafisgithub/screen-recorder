using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Core.Abstractions;

/// <summary>
/// Muxes encoded video and audio packets into the output container (faststart
/// MP4). Owns the FFmpeg format context and stream timing.
/// </summary>
public interface IMediaWriter : IAsyncDisposable
{
    /// <summary>Whether an audio stream has been declared on this container.</summary>
    bool HasAudio { get; }

    /// <summary>
    /// Opens the container and declares the streams. <paramref name="audioFormat"/>
    /// is <c>null</c> when recording video only.
    /// </summary>
    Task OpenAsync(
        string outputPath,
        RecordingSettings settings,
        EncoderDescriptor videoEncoder,
        AudioFormat? audioFormat,
        CancellationToken cancellationToken = default);

    void WriteVideoPacket(EncodedVideoPacket packet);

    void WriteAudioPacket(EncodedAudioPacket packet);

    /// <summary>Writes the trailer / relocates the moov atom and closes the file.</summary>
    Task CloseAsync();
}
