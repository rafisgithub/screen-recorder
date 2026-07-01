using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Enums;
using ScreenRecorder.Core.Events;
using ScreenRecorder.Core.Models;
using ScreenRecorder.Infrastructure.Encoding;

namespace ScreenRecorder.Infrastructure.Recording;

/// <summary>
/// Coordinates the capture → encode → mux pipeline and owns the recording
/// lifecycle. Video frames are encoded on the capture callback thread; audio is
/// mixed (system + microphone) and pumped on a dedicated thread, both stamped
/// against a single <see cref="RecordingClock"/> for A/V sync.
/// </summary>
/// <remarks>MILESTONE 6 — orchestration, A/V sync, lifecycle, live statistics.</remarks>
public sealed class RecordingOrchestrator : IRecordingOrchestrator
{
    private const int AudioSampleRate = 48_000;
    private const int AudioChannels = 2;
    private const int MaxAudioChunkFrames = AudioSampleRate / 5; // 200 ms per pump iteration.

    private readonly ICaptureService _capture;
    private readonly ISystemAudioCaptureService _systemAudio;
    private readonly IMicrophoneCaptureService _microphone;
    private readonly IEncoderFactory _encoderFactory;
    private readonly IHardwareCapabilityService _capabilities;
    private readonly IMediaWriter _mediaWriter;
    private readonly IOutputPathService _outputPath;
    private readonly ISystemClock _clock;
    private readonly ILogger<RecordingOrchestrator> _logger;

    private readonly object _lifecycle = new();
    private readonly RecordingClock _recordingClock;

    private IVideoEncoder? _videoEncoder;
    private IAudioEncoder? _audioEncoder;
    private MixingSampleProvider? _mixer;
    private BufferedWaveProvider? _systemBuffer;
    private BufferedWaveProvider? _microphoneBuffer;
    private Thread? _audioPump;
    private Timer? _statsTimer;
    private float[] _mixScratch = new float[MaxAudioChunkFrames * AudioChannels];

    private volatile bool _running;
    private bool _audioEnabled;
    private long _emittedAudioFrames;
    private long _capturedFrames;
    private long _encodedFrames;
    private long _droppedFrames;
    private long _outputBytes;
    private long _lastStatsFrames;
    private TimeSpan _lastStatsElapsed;
    private string _outputFile = string.Empty;
    private string _encoderName = string.Empty;

    public RecordingOrchestrator(
        ICaptureService capture,
        ISystemAudioCaptureService systemAudio,
        IMicrophoneCaptureService microphone,
        IEncoderFactory encoderFactory,
        IHardwareCapabilityService capabilities,
        IMediaWriter mediaWriter,
        IOutputPathService outputPath,
        ISystemClock clock,
        ILogger<RecordingOrchestrator> logger)
    {
        _capture = capture;
        _systemAudio = systemAudio;
        _microphone = microphone;
        _encoderFactory = encoderFactory;
        _capabilities = capabilities;
        _mediaWriter = mediaWriter;
        _outputPath = outputPath;
        _clock = clock;
        _logger = logger;
        _recordingClock = new RecordingClock(clock);
    }

    public RecordingState State { get; private set; } = RecordingState.Idle;

    public RecordingStatistics Statistics { get; private set; } = RecordingStatistics.Empty;

    public event EventHandler<RecordingStateChangedEventArgs>? StateChanged;

    public event EventHandler<RecordingStatistics>? StatisticsUpdated;

    public async Task StartAsync(RecordingSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Target is null)
        {
            throw new InvalidOperationException("Select a capture target before recording.");
        }

        lock (_lifecycle)
        {
            if (State is RecordingState.Recording or RecordingState.Paused or RecordingState.Starting)
            {
                throw new InvalidOperationException("A recording is already in progress.");
            }

            SetState(RecordingState.Starting);
        }

        try
        {
            var capabilities = _capabilities.Detect();
            var descriptor = _encoderFactory.SelectVideoEncoder(settings.Video, capabilities);
            _outputFile = _outputPath.BuildOutputPath(settings);
            _encoderName = descriptor.DisplayName;

            var sourceSize = settings.Target.Bounds.Size;
            if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
            {
                sourceSize = settings.Video.Resolution;
            }

            _logger.LogInformation(
                "Starting recording: target='{Target}', encoder='{Encoder}', output='{Output}'.",
                settings.Target.DisplayName, descriptor.DisplayName, Path.GetFileName(_outputFile));

            // 1. Video encoder.
            _videoEncoder = _encoderFactory.CreateVideoEncoder(descriptor);
            await _videoEncoder.InitializeAsync(settings.Video, sourceSize, cancellationToken).ConfigureAwait(false);

            // 2. Audio sources, mixer, and encoder (when any source is enabled).
            _audioEnabled = settings.Audio.HasAnySource;
            if (_audioEnabled)
            {
                await SetUpAudioAsync(settings.Audio, cancellationToken).ConfigureAwait(false);
            }

            // 3. Muxer — bind opened encoder contexts so the MP4 carries codec extradata.
            BindMuxer();
            await _mediaWriter.OpenAsync(
                _outputFile,
                settings,
                descriptor,
                _audioEnabled ? AudioFormat.Float32Stereo48k : null,
                cancellationToken).ConfigureAwait(false);

            // 4. Reset counters and start the shared clock before any frames flow.
            ResetCounters();
            _recordingClock.Start();
            _running = true;

            // 5. Wire capture and begin.
            _capture.FrameArrived += OnVideoFrameArrived;
            _capture.CaptureFailed += OnCaptureFailed;
            await _capture.StartAsync(settings.Target, settings, cancellationToken).ConfigureAwait(false);

            if (_audioEnabled)
            {
                StartAudioPump();
            }

            StartStatsTimer();
            SetState(RecordingState.Recording);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording.");
            await SafeTeardownAsync().ConfigureAwait(false);
            SetState(RecordingState.Error, ex);
            throw;
        }
    }

    public async Task PauseAsync()
    {
        if (State != RecordingState.Recording)
        {
            return;
        }

        _recordingClock.Pause();
        SetState(RecordingState.Paused);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task ResumeAsync()
    {
        if (State != RecordingState.Paused)
        {
            return;
        }

        _recordingClock.Resume();
        SetState(RecordingState.Recording);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task<string?> StopAsync()
    {
        lock (_lifecycle)
        {
            if (State is RecordingState.Idle or RecordingState.Stopping)
            {
                return null;
            }

            SetState(RecordingState.Stopping);
        }

        try
        {
            await SafeTeardownAsync().ConfigureAwait(false);
            UpdateStatistics();
            SetState(RecordingState.Idle);
            _logger.LogInformation("Recording saved to {Output}.", _outputFile);
            return string.IsNullOrEmpty(_outputFile) ? null : _outputFile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finalizing recording.");
            SetState(RecordingState.Error, ex);
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (State is RecordingState.Recording or RecordingState.Paused)
        {
            await StopAsync().ConfigureAwait(false);
        }
        else
        {
            await SafeTeardownAsync().ConfigureAwait(false);
        }

        await _capture.DisposeAsync().ConfigureAwait(false);
        await _systemAudio.DisposeAsync().ConfigureAwait(false);
        await _microphone.DisposeAsync().ConfigureAwait(false);
        await _mediaWriter.DisposeAsync().ConfigureAwait(false);
    }

    private async Task SetUpAudioAsync(AudioSettings audio, CancellationToken cancellationToken)
    {
        _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(AudioSampleRate, AudioChannels))
        {
            ReadFully = true, // emit silence when a source has no data, keeping audio continuous.
        };

        if (audio.CaptureSystemAudio)
        {
            await _systemAudio.StartAsync(audio.SystemAudioDeviceId, cancellationToken).ConfigureAwait(false);
            _systemBuffer = CreateBuffer(_systemAudio.Format);
            _mixer.AddMixerInput(BuildSourceChain(_systemBuffer, audio.SystemGain));
            _systemAudio.DataAvailable += OnSystemAudio;
            _systemAudio.CaptureFailed += OnCaptureFailed;
        }

        if (audio.CaptureMicrophone)
        {
            await _microphone.StartAsync(audio.MicrophoneDeviceId, cancellationToken).ConfigureAwait(false);
            _microphoneBuffer = CreateBuffer(_microphone.Format);
            _mixer.AddMixerInput(BuildSourceChain(_microphoneBuffer, audio.MicrophoneGain));
            _microphone.DataAvailable += OnMicrophoneAudio;
            _microphone.CaptureFailed += OnCaptureFailed;
        }

        var encoderSettings = audio.Clone();
        encoderSettings.SampleRate = AudioSampleRate;
        encoderSettings.Channels = AudioChannels;

        _audioEncoder = _encoderFactory.CreateAudioEncoder(encoderSettings);
        await _audioEncoder.InitializeAsync(encoderSettings, AudioFormat.Float32Stereo48k, cancellationToken).ConfigureAwait(false);
    }

    private static BufferedWaveProvider CreateBuffer(AudioFormat format)
    {
        var waveFormat = format.IsFloat
            ? WaveFormat.CreateIeeeFloatWaveFormat(format.SampleRate, format.Channels)
            : new WaveFormat(format.SampleRate, format.BitsPerSample, format.Channels);

        return new BufferedWaveProvider(waveFormat)
        {
            BufferDuration = TimeSpan.FromSeconds(2),
            DiscardOnBufferOverflow = true,
            ReadFully = true,
        };
    }

    private ISampleProvider BuildSourceChain(BufferedWaveProvider buffer, double gain)
    {
        ISampleProvider provider = buffer.ToSampleProvider();

        if (provider.WaveFormat.SampleRate != AudioSampleRate)
        {
            provider = new WdlResamplingSampleProvider(provider, AudioSampleRate);
        }

        if (provider.WaveFormat.Channels != AudioChannels)
        {
            // Maps mono→stereo (duplicate) or N→2 (first two channels).
            provider = new MultiplexingSampleProvider(new[] { provider }, AudioChannels);
        }

        return new VolumeSampleProvider(provider) { Volume = (float)Math.Clamp(gain, 0d, 4d) };
    }

    private void BindMuxer()
    {
        if (_mediaWriter is FFmpegMuxer muxer && _videoEncoder is IFFmpegEncoderContext videoCtx)
        {
            muxer.AttachEncoders(videoCtx, _audioEncoder as IFFmpegEncoderContext);
        }
        else
        {
            _logger.LogWarning("Media writer is not an FFmpeg muxer; stream parameters may be incomplete.");
        }
    }

    private void OnVideoFrameArrived(object? sender, VideoFrame frame)
    {
        if (!_running || _recordingClock.IsPaused)
        {
            return;
        }

        try
        {
            Interlocked.Increment(ref _capturedFrames);

            var stamped = new VideoFrame
            {
                Size = frame.Size,
                Stride = frame.Stride,
                DataPointer = frame.DataPointer,
                DataLength = frame.DataLength,
                GpuTexture = frame.GpuTexture,
                Timestamp = _recordingClock.Elapsed,
            };

            var packets = _videoEncoder!.Encode(stamped);
            Interlocked.Increment(ref _encodedFrames);
            WriteVideoPackets(packets);
        }
        catch (Exception ex)
        {
            FailRecording(ex, "video encoding");
        }
    }

    private void OnCaptureFailed(object? sender, Exception error) => FailRecording(error, "capture source");

    private void OnSystemAudio(object? sender, AudioFrame frame) => SubmitAudio(_systemBuffer, frame);

    private void OnMicrophoneAudio(object? sender, AudioFrame frame) => SubmitAudio(_microphoneBuffer, frame);

    private void SubmitAudio(BufferedWaveProvider? buffer, AudioFrame frame)
    {
        if (!_running || _recordingClock.IsPaused || buffer is null || frame.Buffer.IsEmpty)
        {
            return;
        }

        var (array, offset, length) = AsArray(frame.Buffer);
        buffer.AddSamples(array, offset, length);
    }

    private void AudioPumpLoop()
    {
        while (_running)
        {
            try
            {
                if (_recordingClock.IsPaused)
                {
                    Thread.Sleep(10);
                    continue;
                }

                long target = _recordingClock.SamplesFor(AudioSampleRate);
                long pending = target - _emittedAudioFrames;
                if (pending <= 0)
                {
                    Thread.Sleep(5);
                    continue;
                }

                int frames = (int)Math.Min(pending, MaxAudioChunkFrames);
                int floats = frames * AudioChannels;
                if (_mixScratch.Length < floats)
                {
                    _mixScratch = new float[floats];
                }

                int read = _mixer!.Read(_mixScratch, 0, floats);
                if (read <= 0)
                {
                    Thread.Sleep(5);
                    continue;
                }

                int framesRead = read / AudioChannels;
                var bytes = new byte[read * sizeof(float)];
                Buffer.BlockCopy(_mixScratch, 0, bytes, 0, bytes.Length);

                var frame = new AudioFrame
                {
                    Buffer = bytes,
                    Format = AudioFormat.Float32Stereo48k,
                    Timestamp = TimeSpan.FromSeconds((double)_emittedAudioFrames / AudioSampleRate),
                };

                WriteAudioPackets(_audioEncoder!.Encode(frame));
                _emittedAudioFrames += framesRead;
            }
            catch (Exception ex)
            {
                FailRecording(ex, "audio encoding");
                return;
            }
        }
    }

    private void WriteVideoPackets(IReadOnlyList<EncodedVideoPacket> packets)
    {
        foreach (var packet in packets)
        {
            _mediaWriter.WriteVideoPacket(packet);
            Interlocked.Add(ref _outputBytes, packet.Data.Length);
        }
    }

    private void WriteAudioPackets(IReadOnlyList<EncodedAudioPacket> packets)
    {
        foreach (var packet in packets)
        {
            _mediaWriter.WriteAudioPacket(packet);
            Interlocked.Add(ref _outputBytes, packet.Data.Length);
        }
    }

    private void StartAudioPump()
    {
        _audioPump = new Thread(AudioPumpLoop)
        {
            IsBackground = true,
            Name = "AudioPump",
        };
        _audioPump.Start();
    }

    private void StartStatsTimer()
    {
        _lastStatsFrames = 0;
        _lastStatsElapsed = TimeSpan.Zero;
        _statsTimer = new Timer(_ => UpdateStatistics(), null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
    }

    private void UpdateStatistics()
    {
        var elapsed = _recordingClock.Elapsed;
        long encoded = Interlocked.Read(ref _encodedFrames);
        long bytes = Interlocked.Read(ref _outputBytes);

        double deltaSeconds = (elapsed - _lastStatsElapsed).TotalSeconds;
        double fps = deltaSeconds > 0 ? (encoded - _lastStatsFrames) / deltaSeconds : 0;
        _lastStatsFrames = encoded;
        _lastStatsElapsed = elapsed;

        double bitrate = elapsed.TotalSeconds > 0 ? bytes * 8d / elapsed.TotalSeconds / 1000d : 0;

        Statistics = new RecordingStatistics
        {
            Elapsed = elapsed,
            CapturedFrames = Interlocked.Read(ref _capturedFrames),
            EncodedFrames = encoded,
            DroppedFrames = Interlocked.Read(ref _droppedFrames),
            CurrentFps = fps,
            OutputBytes = bytes,
            OutputBitrateKbps = bitrate,
            EncoderName = _encoderName,
            OutputPath = _outputFile,
        };

        StatisticsUpdated?.Invoke(this, Statistics);
    }

    private async Task SafeTeardownAsync()
    {
        _running = false;
        _recordingClock.Stop();

        if (_statsTimer is not null)
        {
            await _statsTimer.DisposeAsync().ConfigureAwait(false);
            _statsTimer = null;
        }

        // Stop the audio pump before flushing so no writes race the trailer.
        if (_audioPump is not null)
        {
            _audioPump.Join(TimeSpan.FromSeconds(2));
            _audioPump = null;
        }

        _capture.FrameArrived -= OnVideoFrameArrived;
        _capture.CaptureFailed -= OnCaptureFailed;
        await SafeStopAsync(_capture.StopAsync, "video capture").ConfigureAwait(false);

        if (_audioEnabled)
        {
            _systemAudio.DataAvailable -= OnSystemAudio;
            _systemAudio.CaptureFailed -= OnCaptureFailed;
            _microphone.DataAvailable -= OnMicrophoneAudio;
            _microphone.CaptureFailed -= OnCaptureFailed;
            await SafeStopAsync(_systemAudio.StopAsync, "system audio").ConfigureAwait(false);
            await SafeStopAsync(_microphone.StopAsync, "microphone").ConfigureAwait(false);
        }

        // Flush encoders (no more live frames at this point) and finalize the file.
        TryFlushVideo();
        TryFlushAudio();
        await SafeCloseAsync().ConfigureAwait(false);

        await DisposeEncodersAsync().ConfigureAwait(false);
        _mixer = null;
        _systemBuffer = null;
        _microphoneBuffer = null;
        _audioEnabled = false;
    }

    private void TryFlushVideo()
    {
        try
        {
            if (_videoEncoder is not null)
            {
                WriteVideoPackets(_videoEncoder.Flush());
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Flushing the video encoder failed.");
        }
    }

    private void TryFlushAudio()
    {
        try
        {
            if (_audioEncoder is not null)
            {
                WriteAudioPackets(_audioEncoder.Flush());
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Flushing the audio encoder failed.");
        }
    }

    private async Task SafeCloseAsync()
    {
        try
        {
            await _mediaWriter.CloseAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Closing the media writer failed.");
        }
    }

    private async Task DisposeEncodersAsync()
    {
        if (_videoEncoder is not null)
        {
            await _videoEncoder.DisposeAsync().ConfigureAwait(false);
            _videoEncoder = null;
        }

        if (_audioEncoder is not null)
        {
            await _audioEncoder.DisposeAsync().ConfigureAwait(false);
            _audioEncoder = null;
        }
    }

    private async Task SafeStopAsync(Func<Task> stop, string what)
    {
        try
        {
            await stop().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stopping {What} failed.", what);
        }
    }

    private void FailRecording(Exception ex, string stage)
    {
        if (!_running)
        {
            return;
        }

        _running = false;
        _logger.LogError(ex, "Recording failed during {Stage}.", stage);
        _ = Task.Run(async () =>
        {
            try
            {
                await SafeTeardownAsync().ConfigureAwait(false);
            }
            finally
            {
                SetState(RecordingState.Error, ex);
            }
        });
    }

    private void ResetCounters()
    {
        _emittedAudioFrames = 0;
        _capturedFrames = 0;
        _encodedFrames = 0;
        _droppedFrames = 0;
        _outputBytes = 0;
        _lastStatsFrames = 0;
        _lastStatsElapsed = TimeSpan.Zero;
    }

    private void SetState(RecordingState next, Exception? error = null)
    {
        RecordingState previous;
        lock (_lifecycle)
        {
            previous = State;
            State = next;
        }

        StateChanged?.Invoke(this, new RecordingStateChangedEventArgs(previous, next, error));
    }

    private static (byte[] Array, int Offset, int Length) AsArray(ReadOnlyMemory<byte> memory)
    {
        if (MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> segment) && segment.Array is not null)
        {
            return (segment.Array, segment.Offset, segment.Count);
        }

        var copy = memory.ToArray();
        return (copy, 0, copy.Length);
    }
}
