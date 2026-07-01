namespace ScreenRecorder.Infrastructure.Encoding;

/// <summary>
/// Infrastructure-internal bridge that lets the <see cref="FFmpegMuxer"/> read an
/// opened encoder's <c>AVCodecContext*</c> (for <c>codecpar</c>/extradata and the
/// codec time base) without leaking native types through the public Core contracts.
/// </summary>
internal interface IFFmpegEncoderContext
{
    /// <summary>The opened <c>AVCodecContext*</c> as a native handle, or zero if not initialized.</summary>
    nint CodecContext { get; }
}
