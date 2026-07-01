using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Infrastructure.Encoding;

/// <summary>
/// Encodes captured BGRA frames to H.264/HEVC with FFmpeg, scaling to the target
/// resolution and converting to the encoder's pixel format (NV12 for hardware,
/// YUV420P for software). Hardware paths (NVENC/QSV/AMF) are selected by the
/// <see cref="EncoderDescriptor"/>; rate control comes from <see cref="VideoEncoderPlan"/>.
/// </summary>
/// <remarks>MILESTONE 5 — encoding (CPU-mapped frame path).</remarks>
public sealed unsafe class FFmpegVideoEncoder : IVideoEncoder, IFFmpegEncoderContext
{
    private const int CodecTimeBaseDen = 1000; // PTS expressed in milliseconds.

    private readonly ILogger<FFmpegVideoEncoder> _logger;
    private readonly object _sync = new();

    private AVCodecContext* _ctx;
    private AVFrame* _frame;
    private AVPacket* _pkt;
    private SwsContext* _sws;

    private int _outputWidth;
    private int _outputHeight;
    private int _srcWidth;
    private int _srcHeight;
    private AVPixelFormat _pixelFormat;
    private long _lastPts = -1;
    private bool _initialized;
    private bool _disposed;

    public FFmpegVideoEncoder(EncoderDescriptor descriptor, ILogger<FFmpegVideoEncoder> logger)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _logger = logger;
    }

    public EncoderDescriptor Descriptor { get; }

    nint IFFmpegEncoderContext.CodecContext => (nint)_ctx;

    public Task InitializeAsync(VideoSettings settings, Resolution sourceSize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_initialized)
            {
                throw new InvalidOperationException("Video encoder is already initialized.");
            }

            if (!FFmpegInterop.TryInitialize(_logger))
            {
                throw new InvalidOperationException(
                    "FFmpeg native libraries are not available; cannot encode video.");
            }

            var plan = VideoEncoderPlan.Build(Descriptor, settings);

            AVCodec* codec = ffmpeg.avcodec_find_encoder_by_name(Descriptor.FFmpegEncoderName);
            if (codec == null)
            {
                throw new InvalidOperationException(
                    $"Video encoder '{Descriptor.FFmpegEncoderName}' is not available in this FFmpeg build.");
            }

            _outputWidth = AlignEven(settings.Resolution.Width);
            _outputHeight = AlignEven(settings.Resolution.Height);
            _srcWidth = Math.Max(2, sourceSize.Width);
            _srcHeight = Math.Max(2, sourceSize.Height);
            _pixelFormat = plan.UseNv12 ? AVPixelFormat.AV_PIX_FMT_NV12 : AVPixelFormat.AV_PIX_FMT_YUV420P;

            _ctx = ffmpeg.avcodec_alloc_context3(codec);
            if (_ctx == null)
            {
                throw new InvalidOperationException("avcodec_alloc_context3 failed for the video encoder.");
            }

            int fps = Math.Clamp(settings.FrameRate, 1, 240);
            _ctx->width = _outputWidth;
            _ctx->height = _outputHeight;
            _ctx->time_base = new AVRational { num = 1, den = CodecTimeBaseDen };
            _ctx->framerate = new AVRational { num = fps, den = 1 };
            _ctx->pix_fmt = _pixelFormat;
            _ctx->gop_size = Math.Max(1, (int)Math.Round(fps * Math.Max(0.1, settings.KeyframeIntervalSeconds)));
            _ctx->max_b_frames = 0; // low latency; keeps DTS == PTS for screen capture.

            if (plan.BitRate > 0)
            {
                _ctx->bit_rate = plan.BitRate;
            }

            if (plan.MaxBitRate > 0)
            {
                _ctx->rc_max_rate = plan.MaxBitRate;
                _ctx->rc_buffer_size = (int)Math.Min(plan.MaxBitRate, int.MaxValue);
            }

            if (plan.GlobalQuality.HasValue)
            {
                _ctx->global_quality = plan.GlobalQuality.Value;
                _ctx->flags |= ffmpeg.AV_CODEC_FLAG_QSCALE;
            }

            // MP4 always wants the codec's parameter sets in the container header.
            _ctx->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;

            foreach (var (key, value) in plan.PrivateOptions)
            {
                int set = ffmpeg.av_opt_set(_ctx->priv_data, key, value, 0);
                if (set < 0)
                {
                    _logger.LogDebug("Encoder '{Encoder}' rejected option {Key}={Value}.", Descriptor.FFmpegEncoderName, key, value);
                }
            }

            FFmpegInterop.ThrowIfError(ffmpeg.avcodec_open2(_ctx, codec, null), $"Opening encoder '{Descriptor.FFmpegEncoderName}'");

            _frame = ffmpeg.av_frame_alloc();
            _frame->format = (int)_pixelFormat;
            _frame->width = _outputWidth;
            _frame->height = _outputHeight;
            FFmpegInterop.ThrowIfError(ffmpeg.av_frame_get_buffer(_frame, 0), "Allocating the encoder frame buffer");

            _pkt = ffmpeg.av_packet_alloc();
            CreateScaler();

            _initialized = true;
            _logger.LogInformation(
                "Video encoder ready: {Encoder} {Width}x{Height} @ {Fps}fps ({Format}).",
                Descriptor.DisplayName, _outputWidth, _outputHeight, fps, _pixelFormat);
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<EncodedVideoPacket> Encode(VideoFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        lock (_sync)
        {
            if (!_initialized || _disposed)
            {
                return Array.Empty<EncodedVideoPacket>();
            }

            if (frame.DataPointer == nint.Zero)
            {
                // GPU-only frame: the CPU path needs a mapped buffer.
                return Array.Empty<EncodedVideoPacket>();
            }

            EnsureScaler(frame.Size.Width, frame.Size.Height);
            FFmpegInterop.ThrowIfError(ffmpeg.av_frame_make_writable(_frame), "av_frame_make_writable");

            var srcData = default(byte_ptrArray4);
            var srcLine = default(int_array4);
            srcData[0] = (byte*)frame.DataPointer;
            srcLine[0] = frame.Stride;

            var dstData = default(byte_ptrArray4);
            var dstLine = default(int_array4);
            for (uint i = 0; i < 4; i++)
            {
                dstData[i] = _frame->data[i];
                dstLine[i] = _frame->linesize[i];
            }

            ffmpeg.sws_scale(_sws, srcData, srcLine, 0, _srcHeight, dstData, dstLine);

            long pts = (long)Math.Round(frame.Timestamp.TotalMilliseconds);
            if (pts <= _lastPts)
            {
                pts = _lastPts + 1;
            }

            _lastPts = pts;
            _frame->pts = pts;

            return Drain(_frame);
        }
    }

    public IReadOnlyList<EncodedVideoPacket> Flush()
    {
        lock (_sync)
        {
            if (!_initialized || _disposed)
            {
                return Array.Empty<EncodedVideoPacket>();
            }

            return Drain(null);
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

            if (_sws != null)
            {
                ffmpeg.sws_freeContext(_sws);
                _sws = null;
            }

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

    private List<EncodedVideoPacket> Drain(AVFrame* frame)
    {
        var packets = new List<EncodedVideoPacket>();

        int send = ffmpeg.avcodec_send_frame(_ctx, frame);
        if (send < 0 && send != ffmpeg.AVERROR_EOF)
        {
            FFmpegInterop.ThrowIfError(send, "avcodec_send_frame (video)");
        }

        while (true)
        {
            int recv = ffmpeg.avcodec_receive_packet(_ctx, _pkt);
            if (recv == ffmpeg.AVERROR(ffmpeg.EAGAIN) || recv == ffmpeg.AVERROR_EOF)
            {
                break;
            }

            FFmpegInterop.ThrowIfError(recv, "avcodec_receive_packet (video)");

            var data = new byte[_pkt->size];
            Marshal.Copy((IntPtr)_pkt->data, data, 0, _pkt->size);

            packets.Add(new EncodedVideoPacket
            {
                Data = data,
                PresentationTimestamp = TimeStamp(_pkt->pts),
                DecodeTimestamp = TimeStamp(_pkt->dts),
                IsKeyframe = (_pkt->flags & ffmpeg.AV_PKT_FLAG_KEY) != 0,
            });

            ffmpeg.av_packet_unref(_pkt);
        }

        return packets;
    }

    private void EnsureScaler(int srcWidth, int srcHeight)
    {
        if (srcWidth == _srcWidth && srcHeight == _srcHeight && _sws != null)
        {
            return;
        }

        _srcWidth = Math.Max(2, srcWidth);
        _srcHeight = Math.Max(2, srcHeight);
        if (_sws != null)
        {
            ffmpeg.sws_freeContext(_sws);
            _sws = null;
        }

        CreateScaler();
    }

    private void CreateScaler()
    {
        _sws = ffmpeg.sws_getContext(
            _srcWidth, _srcHeight, AVPixelFormat.AV_PIX_FMT_BGRA,
            _outputWidth, _outputHeight, _pixelFormat,
            ffmpeg.SWS_BILINEAR, null, null, null);
        if (_sws == null)
        {
            throw new InvalidOperationException("sws_getContext failed for the video scaler.");
        }
    }

    private static TimeSpan TimeStamp(long codecTimeBaseValue)
    {
        if (codecTimeBaseValue == ffmpeg.AV_NOPTS_VALUE)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromMilliseconds(codecTimeBaseValue);
    }

    private static int AlignEven(int value) => value % 2 == 0 ? value : value + 1;
}
