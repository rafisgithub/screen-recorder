namespace ScreenRecorder.Core.Models;

/// <summary>
/// A single captured video frame handed from the capture service to the encoder.
/// </summary>
/// <remarks>
/// Frames are <b>pool-owned</b>: the buffer/texture is valid only for the
/// duration of the <c>FrameArrived</c> callback (or the synchronous encode call
/// it triggers). Consumers must copy anything they need to retain.
/// <para>
/// Two transports are supported. The fast path leaves the frame on the GPU
/// (<see cref="GpuTexture"/> set) for a zero-copy hand-off to a hardware encoder;
/// the fallback path exposes a CPU-mapped BGRA8 buffer (<see cref="DataPointer"/>).
/// </para>
/// </remarks>
public sealed class VideoFrame
{
    /// <summary>Presentation timestamp relative to recording start.</summary>
    public TimeSpan Timestamp { get; init; }

    public Resolution Size { get; init; }

    /// <summary>Row stride in bytes for the CPU buffer (BGRA8, top-down).</summary>
    public int Stride { get; init; }

    /// <summary>Pointer to CPU pixel data, or <see cref="nint.Zero"/> when GPU-backed.</summary>
    public nint DataPointer { get; init; }

    /// <summary>Length in bytes of the CPU buffer.</summary>
    public int DataLength { get; init; }

    /// <summary>Native ID3D11Texture2D*, or <see cref="nint.Zero"/> when CPU-backed.</summary>
    public nint GpuTexture { get; init; }

    public bool IsGpuBacked => GpuTexture != nint.Zero;
}
