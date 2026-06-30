using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Infrastructure.Encoding;

/// <summary>
/// Encodes PCM audio to AAC with FFmpeg (FFmpeg.AutoGen).
/// </summary>
/// <remarks>MILESTONE 5 — encoding. Currently a scaffold stub.</remarks>
public sealed class FFmpegAudioEncoder : IAudioEncoder
{
    private readonly ILogger<FFmpegAudioEncoder> _logger;

    public FFmpegAudioEncoder(ILogger<FFmpegAudioEncoder> logger) => _logger = logger;

    public Task InitializeAsync(AudioSettings settings, AudioFormat sourceFormat, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("FFmpegAudioEncoder.InitializeAsync invoked before Milestone 5 is implemented.");
        throw new NotImplementedException("FFmpeg AAC encoding is implemented in Milestone 5.");
    }

    public IReadOnlyList<EncodedAudioPacket> Encode(AudioFrame frame) =>
        throw new NotImplementedException("FFmpeg AAC encoding is implemented in Milestone 5.");

    public IReadOnlyList<EncodedAudioPacket> Flush() =>
        throw new NotImplementedException("FFmpeg AAC encoding is implemented in Milestone 5.");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
