namespace ScreenRecorder.Core.Models;

/// <summary>
/// A buffer of captured PCM audio handed from a capture service to the mixer/encoder.
/// </summary>
/// <remarks>
/// As with <see cref="VideoFrame"/>, the underlying memory may be pool-owned and
/// valid only for the duration of the callback; copy to retain.
/// </remarks>
public sealed class AudioFrame
{
    /// <summary>Presentation timestamp relative to recording start.</summary>
    public TimeSpan Timestamp { get; init; }

    /// <summary>Raw interleaved PCM samples in <see cref="Format"/>.</summary>
    public ReadOnlyMemory<byte> Buffer { get; init; }

    public AudioFormat Format { get; init; }

    /// <summary>Number of sample frames in <see cref="Buffer"/>.</summary>
    public int FrameCount => Format.BlockAlign == 0 ? 0 : Buffer.Length / Format.BlockAlign;
}
