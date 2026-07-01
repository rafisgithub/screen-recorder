using System.IO;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;

namespace ScreenRecorder.Infrastructure.Encoding;

/// <summary>
/// One-time native FFmpeg setup and small helpers shared by the encoders and the
/// muxer. Resolves the FFmpeg 7.x shared libraries, verifies they load, and
/// exposes encoder-availability probes.
/// </summary>
/// <remarks>
/// The managed <c>FFmpeg.AutoGen</c> binding compiles without the native DLLs;
/// they are only required at runtime. When they are missing, <see cref="IsAvailable"/>
/// is <c>false</c> and callers degrade gracefully (e.g. capability detection
/// reports software-only and recording surfaces a clear error).
/// </remarks>
internal static unsafe class FFmpegInterop
{
    private static readonly object Gate = new();
    private static bool _initialized;

    /// <summary>True once the native libraries have been located and loaded.</summary>
    public static bool IsAvailable { get; private set; }

    /// <summary>FFmpeg version string, or <c>null</c> when unavailable.</summary>
    public static string? VersionInfo { get; private set; }

    /// <summary>
    /// Locates and loads the native FFmpeg libraries (idempotent, thread-safe).
    /// Returns <see cref="IsAvailable"/>.
    /// </summary>
    public static bool TryInitialize(ILogger logger)
    {
        lock (Gate)
        {
            if (_initialized)
            {
                return IsAvailable;
            }

            _initialized = true;

            try
            {
                var root = ResolveRootPath();
                if (root is not null)
                {
                    ffmpeg.RootPath = root;
                }

                // FFmpeg.AutoGen 7 uses dynamic P/Invoke bindings: must call
                // Initialize() to wire up all function pointers before any native call.
                DynamicallyLoadedBindings.Initialize();

                // First native call: verifies the DLLs actually loaded.
                VersionInfo = ffmpeg.av_version_info();
                uint codecVersion = ffmpeg.avcodec_version();
                int major = (int)(codecVersion >> 16);

                ConfigureLogging();

                IsAvailable = true;
                logger.LogInformation(
                    "FFmpeg loaded: {Version} (avcodec major {Major}) from '{Root}'.",
                    VersionInfo, major, string.IsNullOrEmpty(ffmpeg.RootPath) ? "PATH" : ffmpeg.RootPath);

                if (major < 59)
                {
                    logger.LogWarning(
                        "FFmpeg avcodec major {Major} is older than the expected 61 (FFmpeg 7.x); behavior may differ.", major);
                }
            }
            catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
            {
                IsAvailable = false;
                logger.LogWarning(
                    "FFmpeg native libraries not found. Recording/encoding is unavailable until the FFmpeg 7.x " +
                    "shared DLLs (avcodec-61, avformat-61, avutil-59, swscale-8, swresample-5) are on the PATH or " +
                    "in an 'ffmpeg' folder beside the executable. ({Message})", ex.Message);
            }
            catch (Exception ex)
            {
                IsAvailable = false;
                logger.LogError(ex, "Unexpected error initializing FFmpeg.");
            }

            return IsAvailable;
        }
    }

    /// <summary>True when the named encoder is compiled into the loaded FFmpeg build.</summary>
    public static bool EncoderCompiledIn(string name)
    {
        AVCodec* codec = ffmpeg.avcodec_find_encoder_by_name(name);
        return codec != null;
    }

    /// <summary>
    /// True when the named encoder both exists and successfully opens on this
    /// machine — the real test for hardware encoders (driver/GPU present).
    /// </summary>
    public static bool EncoderUsable(string name, int width = 1280, int height = 720)
    {
        AVCodec* codec = ffmpeg.avcodec_find_encoder_by_name(name);
        if (codec == null)
        {
            return false;
        }

        AVCodecContext* ctx = ffmpeg.avcodec_alloc_context3(codec);
        if (ctx == null)
        {
            return false;
        }

        try
        {
            ctx->width = width;
            ctx->height = height;
            ctx->time_base = new AVRational { num = 1, den = 30 };
            ctx->framerate = new AVRational { num = 30, den = 1 };
            ctx->pix_fmt = AVPixelFormat.AV_PIX_FMT_NV12;
            ctx->bit_rate = 4_000_000;
            return ffmpeg.avcodec_open2(ctx, codec, null) == 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            ffmpeg.avcodec_free_context(&ctx);
        }
    }

    /// <summary>Translates an FFmpeg negative error code into a readable message.</summary>
    public static string ErrorToString(int code)
    {
        const int bufferSize = 1024;
        byte* buffer = stackalloc byte[bufferSize];
        ffmpeg.av_strerror(code, buffer, bufferSize);
        return Marshal.PtrToStringAnsi((IntPtr)buffer) ?? $"FFmpeg error {code}";
    }

    /// <summary>Throws when <paramref name="code"/> is a negative FFmpeg error.</summary>
    public static void ThrowIfError(int code, string what)
    {
        if (code < 0)
        {
            throw new InvalidOperationException($"{what} failed: {ErrorToString(code)} ({code}).");
        }
    }

    private static string? ResolveRootPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var arch = Environment.Is64BitProcess ? "x64" : "x86";

        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("FFMPEG_ROOT"),
            Path.Combine(baseDir, "ffmpeg"),
            Path.Combine(baseDir, "ffmpeg", "bin"),
            Path.Combine(baseDir, "FFmpeg", "bin", arch),
            Path.Combine(baseDir, "runtimes", $"win-{arch}", "native"),
            baseDir,
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) &&
                Directory.Exists(candidate) &&
                Directory.EnumerateFiles(candidate, "avcodec-*.dll").Any())
            {
                return candidate;
            }
        }

        // Fall back to the OS search path (PATH / next to the exe).
        return null;
    }

    private static av_log_set_callback_callback? _logCallback;

    private static void ConfigureLogging()
    {
        ffmpeg.av_log_set_level(ffmpeg.AV_LOG_ERROR);
        // Keep the delegate rooted so the GC cannot collect it while native code holds it.
        _logCallback = LogCallback;
        ffmpeg.av_log_set_callback(_logCallback);
    }

    private static void LogCallback(void* avcl, int level, string fmt, byte* vl)
    {
        // Suppressed by level; native formatting is intentionally skipped to avoid
        // varargs marshaling. Errors surface through return codes instead.
    }
}
