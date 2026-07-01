using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Infrastructure.Encoding;

/// <summary>
/// Encodes captured PCM audio to AAC with FFmpeg. Incoming buffers are converted
/// to interleaved float, accumulated, and emitted to the encoder in fixed
/// frame-size chunks as planar float (FLTP). Presentation timestamps are
/// sample-accurate: seeded from the first frame's master-clock timestamp, then
/// advanced by the AAC frame size.
/// </summary>
/// <remarks>
/// MILESTONE 5 — encoding. The orchestrator's mixer delivers audio already at the
/// encoder's sample rate and channel count, so no native resampling is performed.
/// </remarks>
public sealed unsafe class FFmpegAudioEncoder : IAudioEncoder, IFFmpegEncoderContext
{
    private readonly ILogger<FFmpegAudioEncoder> _logger;
    private readonly object _sync = new();
    private readonly Queue<float> _pending = new();

    private AVCodecContext* _ctx;
    private AVFrame* _frame;
    private AVPacket* _pkt;

    private int _channels;
    private int _sampleRate;
    private int _frameSize;
    private long _nextPts;
    private bool _ptsSeeded;
    private bool _initialized;
    private bool _disposed;

    public FFmpegAudioEncoder(ILogger<FFmpegAudioEncoder> logger) => _logger = logger;

    nint IFFmpegEncoderContext.CodecContext => (nint)_ctx;

    public Task InitializeAsync(AudioSettings settings, AudioFormat sourceFormat, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_initialized)
            {
                throw new InvalidOperationException("Audio encoder is already initialized.");
            }

            if (!FFmpegInterop.TryInitialize(_logger))
            {
                throw new InvalidOperationException(
                    "FFmpeg native libraries are not available; cannot encode audio.");
            }

            AVCodec* codec = ffmpeg.avcodec_find_encoder_by_name("aac");
            if (codec == null)
            {
                throw new InvalidOperationException("The AAC encoder is not available in this FFmpeg build.");
            }

            _channels = settings.Channels > 0 ? settings.Channels : 2;
            _sampleRate = settings.SampleRate > 0 ? settings.SampleRate : 48_000;

            _ctx = ffmpeg.avcodec_alloc_context3(codec);
            if (_ctx == null)
            {
                throw new InvalidOperationException("avcodec_alloc_context3 failed for the AAC encoder.");
            }

            _ctx->sample_fmt = AVSampleFormat.AV_SAMPLE_FMT_FLTP;
            _ctx->sample_rate = _sampleRate;
            _ctx->bit_rate = Math.Max(64, settings.BitrateKbps) * 1000L;

            AVChannelLayout layout = default;
            ffmpeg.av_channel_layout_default(&layout, _channels);
            ffmpeg.av_channel_layout_copy(&_ctx->ch_layout, &layout);
            ffmpeg.av_channel_layout_uninit(&layout);

            _ctx->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;

            FFmpegInterop.ThrowIfError(ffmpeg.avcodec_open2(_ctx, codec, null), "Opening the AAC encoder");

            _frameSize = _ctx->frame_size > 0 ? _ctx->frame_size : 1024;

            _frame = ffmpeg.av_frame_alloc();
            _frame->format = (int)AVSampleFormat.AV_SAMPLE_FMT_FLTP;
            _frame->sample_rate = _sampleRate;
            _frame->nb_samples = _frameSize;
            ffmpeg.av_channel_layout_copy(&_frame->ch_layout, &_ctx->ch_layout);
            FFmpegInterop.ThrowIfError(ffmpeg.av_frame_get_buffer(_frame, 0), "Allocating the audio frame buffer");

            _pkt = ffmpeg.av_packet_alloc();

            _initialized = true;
            _logger.LogInformation(
                "Audio encoder ready: AAC {Rate} Hz, {Channels} ch, {Kbps} kbps, frame {FrameSize}.",
                _sampleRate, _channels, settings.BitrateKbps, _frameSize);
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<EncodedAudioPacket> Encode(AudioFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        lock (_sync)
        {
            if (!_initialized || _disposed || frame.Buffer.IsEmpty)
            {
                return Array.Empty<EncodedAudioPacket>();
            }

            if (!_ptsSeeded)
            {
                _nextPts = (long)Math.Round(frame.Timestamp.TotalSeconds * _sampleRate);
                _ptsSeeded = true;
            }

            var interleaved = AudioSampleConverter.ToInterleavedFloat(frame.Buffer.Span, frame.Format, _channels);
            foreach (var sample in interleaved)
            {
                _pending.Enqueue(sample);
            }

            return DrainFullFrames();
        }
    }

    public IReadOnlyList<EncodedAudioPacket> Flush()
    {
        lock (_sync)
        {
            if (!_initialized || _disposed)
            {
                return Array.Empty<EncodedAudioPacket>();
            }

            var packets = new List<EncodedAudioPacket>(DrainFullFrames());

            // Emit a final partial frame (zero-padded) so no captured audio is lost.
            int remaining = _pending.Count / _channels;
            if (remaining > 0)
            {
                packets.AddRange(EncodeFrame(remaining, padToFrameSize: true));
            }

            packets.AddRange(SendReceive(null));
            return packets;
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _disposed = true;
            _pending.Clear();

            if (_pkt != null)
            {
                AVPacket* pkt = _pkt;
                ffmpeg.av_packet_free(&pkt);
                _pkt = null;
            }

            if (_frame != null)
            {
                AVFrame* frame = _frame;
                ffmpeg.av_frame_free(&frame);
                _frame = null;
            }

            if (_ctx != null)
            {
                AVCodecContext* ctx = _ctx;
                ffmpeg.avcodec_free_context(&ctx);
                _ctx = null;
            }
        }

        return ValueTask.CompletedTask;
    }

    private List<EncodedAudioPacket> DrainFullFrames()
    {
        var packets = new List<EncodedAudioPacket>();
        while (_pending.Count >= _frameSize * _channels)
        {
            packets.AddRange(EncodeFrame(_frameSize, padToFrameSize: false));
        }

        return packets;
    }

    private List<EncodedAudioPacket> EncodeFrame(int sampleCount, bool padToFrameSize)
    {
        FFmpegInterop.ThrowIfError(ffmpeg.av_frame_make_writable(_frame), "av_frame_make_writable (audio)");

        // Pull the next chunk out of the interleaved queue, then deinterleave into
        // the planar float frame buffers.
        int consume = sampleCount * _channels;
        var chunk = new float[consume];
        for (int i = 0; i < consume; i++)
        {
            chunk[i] = _pending.Dequeue();
        }

        var plane = new float[_frameSize];
        for (int c = 0; c < _channels; c++)
        {
            Array.Clear(plane);
            for (int i = 0; i < sampleCount; i++)
            {
                plane[i] = chunk[(i * _channels) + c];
            }

            Marshal.Copy(plane, 0, (IntPtr)_frame->data[(uint)c], _frameSize);
        }

        _frame->nb_samples = padToFrameSize ? _frameSize : sampleCount;
        _frame->pts = _nextPts;
        _nextPts += _frame->nb_samples;

        return SendReceive(_frame);
    }

    private List<EncodedAudioPacket> SendReceive(AVFrame* frame)
    {
        var packets = new List<EncodedAudioPacket>();

        int send = ffmpeg.avcodec_send_frame(_ctx, frame);
        if (send < 0 && send != ffmpeg.AVERROR_EOF)
        {
            FFmpegInterop.ThrowIfError(send, "avcodec_send_frame (audio)");
        }

        while (true)
        {
            int recv = ffmpeg.avcodec_receive_packet(_ctx, _pkt);
            if (recv == ffmpeg.AVERROR(ffmpeg.EAGAIN) || recv == ffmpeg.AVERROR_EOF)
            {
                break;
            }

            FFmpegInterop.ThrowIfError(recv, "avcodec_receive_packet (audio)");

            var data = new byte[_pkt->size];
            Marshal.Copy((IntPtr)_pkt->data, data, 0, _pkt->size);

            long pts = _pkt->pts == ffmpeg.AV_NOPTS_VALUE ? 0 : _pkt->pts;
            packets.Add(new EncodedAudioPacket
            {
                Data = data,
                PresentationTimestamp = TimeSpan.FromSeconds((double)pts / _sampleRate),
            });

            ffmpeg.av_packet_unref(_pkt);
        }

        return packets;
    }
}
