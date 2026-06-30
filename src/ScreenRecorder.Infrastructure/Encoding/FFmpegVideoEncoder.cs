using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Infrastructure.Encoding;

/// <summary>
/// Encodes frames with FFmpeg (FFmpeg.AutoGen), preferring a GPU-backed input
/// surface for a zero-copy hand-off to NVENC/QSV/AMF.
/// </summary>
/// <remarks>MILESTONE 5 — encoding. Currently a scaffold stub.</remarks>
public sealed class FFmpegVideoEncoder : IVideoEncoder
{
    private readonly ILogger<FFmpegVideoEncoder> _logger;

    public FFmpegVideoEncoder(EncoderDescriptor descriptor, ILogger<FFmpegVideoEncoder> logger)
    {
        Descriptor = descriptor;
        _logger = logger;
    }

    public EncoderDescriptor Descriptor { get; }

    public Task InitializeAsync(VideoSettings settings, Resolution sourceSize, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("FFmpegVideoEncoder.InitializeAsync invoked before Milestone 5 is implemented.");
        throw new NotImplementedException("FFmpeg video encoding is implemented in Milestone 5.");
    }

    public IReadOnlyList<EncodedVideoPacket> Encode(VideoFrame frame) =>
        throw new NotImplementedException("FFmpeg video encoding is implemented in Milestone 5.");

    public IReadOnlyList<EncodedVideoPacket> Flush() =>
        throw new NotImplementedException("FFmpeg video encoding is implemented in Milestone 5.");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
