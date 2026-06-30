using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Infrastructure.Encoding;

/// <summary>
/// Muxes encoded video/audio packets into a faststart MP4 with FFmpeg.
/// </summary>
/// <remarks>MILESTONE 5 — encoding/muxing. Currently a scaffold stub.</remarks>
public sealed class FFmpegMuxer : IMediaWriter
{
    private readonly ILogger<FFmpegMuxer> _logger;

    public FFmpegMuxer(ILogger<FFmpegMuxer> logger) => _logger = logger;

    public bool HasAudio { get; private set; }

    public Task OpenAsync(
        string outputPath,
        RecordingSettings settings,
        EncoderDescriptor videoEncoder,
        AudioFormat? audioFormat,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("FFmpegMuxer.OpenAsync invoked before Milestone 5 is implemented.");
        throw new NotImplementedException("FFmpeg MP4 muxing is implemented in Milestone 5.");
    }

    public void WriteVideoPacket(EncodedVideoPacket packet) =>
        throw new NotImplementedException("FFmpeg MP4 muxing is implemented in Milestone 5.");

    public void WriteAudioPacket(EncodedAudioPacket packet) =>
        throw new NotImplementedException("FFmpeg MP4 muxing is implemented in Milestone 5.");

    public Task CloseAsync() => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
