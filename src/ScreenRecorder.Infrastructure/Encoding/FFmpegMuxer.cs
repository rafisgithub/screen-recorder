using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Infrastructure.Encoding;

/// <summary>
/// Muxes encoded H.264/HEVC + AAC packets into a faststart MP4 with FFmpeg.
/// Stream parameters (including the codec extradata needed for a playable MP4) are
/// copied from the encoders' opened codec contexts, which the orchestrator binds
/// via <see cref="AttachEncoders"/> before <see cref="OpenAsync"/>.
/// </summary>
/// <remarks>MILESTONE 5 — muxing.</remarks>
public sealed unsafe class FFmpegMuxer : IMediaWriter
{
    private static readonly AVRational VideoSourceTimeBase = new() { num = 1, den = 1000 };

    private readonly ILogger<FFmpegMuxer> _logger;
    private readonly object _sync = new();

    private AVCodecContext* _videoCtx;
    private AVCodecContext* _audioCtx;
    private AVFormatContext* _oc;
    private AVStream* _videoStream;
    private AVStream* _audioStream;
    private AVPacket* _packet;
    private AVRational _audioSourceTimeBase;
    private int _audioSampleRate;
    private bool _headerWritten;
    private bool _faulted;
    private bool _disposed;

    public FFmpegMuxer(ILogger<FFmpegMuxer> logger) => _logger = logger;

    public bool HasAudio { get; private set; }

    /// <summary>
    /// True when a packet write or the trailer failed since the last <see cref="OpenAsync"/>.
    /// The output MP4 is then incomplete/unplayable, so the orchestrator reports the
    /// recording as failed rather than saved.
    /// </summary>
    public bool Faulted
    {
        get { lock (_sync) { return _faulted; } }
    }

    /// <summary>
    /// Binds the opened encoder codec contexts so their parameters (incl. extradata)
    /// can be copied into the output streams. Called before <see cref="OpenAsync"/>.
    /// </summary>
    internal void AttachEncoders(IFFmpegEncoderContext video, IFFmpegEncoderContext? audio)
    {
        ArgumentNullException.ThrowIfNull(video);
        _videoCtx = (AVCodecContext*)video.CodecContext;
        _audioCtx = audio is null ? null : (AVCodecContext*)audio.CodecContext;
    }

    public Task OpenAsync(
        string outputPath,
        RecordingSettings settings,
        EncoderDescriptor videoEncoder,
        AudioFormat? audioFormat,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_oc != null)
            {
                throw new InvalidOperationException("The muxer is already open.");
            }

            _faulted = false;

            if (!FFmpegInterop.TryInitialize(_logger))
            {
                throw new InvalidOperationException("FFmpeg native libraries are not available; cannot write MP4.");
            }

            if (_videoCtx == null)
            {
                throw new InvalidOperationException("No video encoder bound; call AttachEncoders before OpenAsync.");
            }

            AVFormatContext* oc = null;
            FFmpegInterop.ThrowIfError(
                ffmpeg.avformat_alloc_output_context2(&oc, null, "mp4", outputPath),
                "avformat_alloc_output_context2");
            _oc = oc;

            _videoStream = ffmpeg.avformat_new_stream(_oc, null);
            if (_videoStream == null)
            {
                throw new InvalidOperationException("Failed to create the MP4 video stream.");
            }

            FFmpegInterop.ThrowIfError(
                ffmpeg.avcodec_parameters_from_context(_videoStream->codecpar, _videoCtx),
                "avcodec_parameters_from_context (video)");
            _videoStream->time_base = _videoCtx->time_base;

            if (audioFormat is not null && _audioCtx != null)
            {
                _audioStream = ffmpeg.avformat_new_stream(_oc, null);
                if (_audioStream == null)
                {
                    throw new InvalidOperationException("Failed to create the MP4 audio stream.");
                }

                FFmpegInterop.ThrowIfError(
                    ffmpeg.avcodec_parameters_from_context(_audioStream->codecpar, _audioCtx),
                    "avcodec_parameters_from_context (audio)");
                _audioSampleRate = _audioCtx->sample_rate;
                _audioSourceTimeBase = new AVRational { num = 1, den = _audioSampleRate };
                _audioStream->time_base = _audioSourceTimeBase;
                HasAudio = true;
            }

            if ((_oc->oformat->flags & ffmpeg.AVFMT_NOFILE) == 0)
            {
                FFmpegInterop.ThrowIfError(
                    ffmpeg.avio_open(&_oc->pb, outputPath, ffmpeg.AVIO_FLAG_WRITE),
                    "avio_open");
            }

            AVDictionary* options = null;
            ffmpeg.av_dict_set(&options, "movflags", "+faststart", 0);
            int header = ffmpeg.avformat_write_header(_oc, &options);
            ffmpeg.av_dict_free(&options);
            FFmpegInterop.ThrowIfError(header, "avformat_write_header");

            _packet = ffmpeg.av_packet_alloc();
            if (_packet == null)
            {
                throw new InvalidOperationException("av_packet_alloc failed for the muxer.");
            }

            _headerWritten = true;

            _logger.LogInformation(
                "MP4 muxer opened '{Path}' (video={Video}, audio={Audio}).",
                outputPath, videoEncoder.DisplayName, HasAudio ? "AAC" : "none");
        }

        return Task.CompletedTask;
    }

    public void WriteVideoPacket(EncodedVideoPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        long pts = (long)Math.Round(packet.PresentationTimestamp.TotalMilliseconds);
        long dts = (long)Math.Round(packet.DecodeTimestamp.TotalMilliseconds);
        WritePacket(packet.Data.Span, _videoStream, VideoSourceTimeBase, pts, dts, packet.IsKeyframe);
    }

    public void WriteAudioPacket(EncodedAudioPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (_audioStream == null)
        {
            return;
        }

        long pts = (long)Math.Round(packet.PresentationTimestamp.TotalSeconds * _audioSampleRate);
        WritePacket(packet.Data.Span, _audioStream, _audioSourceTimeBase, pts, pts, isKeyframe: true);
    }

    public Task CloseAsync()
    {
        lock (_sync)
        {
            CloseLocked();
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            // CloseLocked frees the packet and format context and resets state,
            // so there is nothing left to release here.
            CloseLocked();
            _disposed = true;
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Finalizes the current output and resets all per-recording state so the
    /// (singleton) muxer can be reopened for the next recording. Safe to call more
    /// than once — a no-op once the format context has been released.
    /// </summary>
    private void CloseLocked()
    {
        if (_oc == null)
        {
            return;
        }

        if (_headerWritten)
        {
            int trailer = ffmpeg.av_write_trailer(_oc);
            if (trailer < 0)
            {
                _faulted = true;
                _logger.LogWarning("av_write_trailer failed: {Error}", FFmpegInterop.ErrorToString(trailer));
            }
        }

        if ((_oc->oformat->flags & ffmpeg.AVFMT_NOFILE) == 0 && _oc->pb != null)
        {
            ffmpeg.avio_closep(&_oc->pb);
        }

        if (_packet != null)
        {
            AVPacket* pkt = _packet;
            ffmpeg.av_packet_free(&pkt);
            _packet = null;
        }

        ffmpeg.avformat_free_context(_oc);
        _oc = null;

        // The stream/codec-context pointers are owned elsewhere (streams by _oc,
        // just freed; codec contexts by the encoders). Drop the borrowed handles
        // so a subsequent OpenAsync starts clean.
        _videoStream = null;
        _audioStream = null;
        _videoCtx = null;
        _audioCtx = null;
        _headerWritten = false;
        HasAudio = false;
    }

    private void WritePacket(ReadOnlySpan<byte> data, AVStream* stream, AVRational sourceTimeBase, long pts, long dts, bool isKeyframe)
    {
        if (data.IsEmpty)
        {
            return;
        }

        lock (_sync)
        {
            if (!_headerWritten || _disposed || _oc == null || stream == null || _packet == null)
            {
                return;
            }

            FFmpegInterop.ThrowIfError(ffmpeg.av_new_packet(_packet, data.Length), "av_new_packet");
            try
            {
                data.CopyTo(new Span<byte>(_packet->data, data.Length));

                _packet->stream_index = stream->index;
                _packet->pts = ffmpeg.av_rescale_q(pts, sourceTimeBase, stream->time_base);
                _packet->dts = ffmpeg.av_rescale_q(dts, sourceTimeBase, stream->time_base);
                _packet->flags = isKeyframe ? ffmpeg.AV_PKT_FLAG_KEY : 0;

                int write = ffmpeg.av_interleaved_write_frame(_oc, _packet);
                if (write < 0)
                {
                    _faulted = true;
                    _logger.LogWarning("av_interleaved_write_frame failed: {Error}", FFmpegInterop.ErrorToString(write));
                }
            }
            finally
            {
                // av_interleaved_write_frame takes ownership and resets the packet;
                // unref is a safe no-op if it already did.
                ffmpeg.av_packet_unref(_packet);
            }
        }
    }
}
