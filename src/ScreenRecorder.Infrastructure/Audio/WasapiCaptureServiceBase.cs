using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Infrastructure.Audio;

/// <summary>
/// Shared WASAPI capture lifecycle for the system-audio (loopback) and microphone
/// services. Wraps an NAudio <see cref="IWaveIn"/>, copies each delivered buffer
/// (NAudio reuses its buffer), stamps it with a capture-relative timestamp, and
/// raises <see cref="DataAvailable"/>. Authoritative A/V presentation timestamps
/// are assigned downstream by the orchestrator's shared clock.
/// </summary>
public abstract class WasapiCaptureServiceBase : IAudioCaptureService
{
    private readonly ILogger _logger;
    private readonly object _sync = new();
    private readonly Stopwatch _elapsed = new();

    private IWaveIn? _capture;
    private TaskCompletionSource<object?>? _stopSignal;
    private bool _disposed;

    protected WasapiCaptureServiceBase(ILogger logger) => _logger = logger;

    public bool IsCapturing { get; private set; }

    public AudioFormat Format { get; private set; } = AudioFormat.Float32Stereo48k;

    public event EventHandler<AudioFrame>? DataAvailable;

    public event EventHandler<Exception>? CaptureFailed;

    /// <summary>Friendly name used in log messages (e.g. "system audio").</summary>
    protected abstract string SourceName { get; }

    /// <summary>Creates the concrete NAudio capture for the requested endpoint.</summary>
    protected abstract IWaveIn CreateCapture(string? deviceId);

    public Task StartAsync(string? deviceId, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsCapturing)
            {
                throw new InvalidOperationException($"{SourceName} capture is already running.");
            }

            var capture = CreateCapture(deviceId);
            var waveFormat = capture.WaveFormat;
            Format = new AudioFormat(
                waveFormat.SampleRate,
                waveFormat.Channels,
                waveFormat.BitsPerSample,
                waveFormat.Encoding == WaveFormatEncoding.IeeeFloat);

            capture.DataAvailable += OnDataAvailable;
            capture.RecordingStopped += OnRecordingStopped;

            _capture = capture;
            _stopSignal = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _elapsed.Restart();
            capture.StartRecording();
            IsCapturing = true;

            _logger.LogInformation(
                "{Source} capture started: {Rate} Hz, {Channels} ch, {Bits}-bit {Encoding}.",
                SourceName, Format.SampleRate, Format.Channels, Format.BitsPerSample,
                Format.IsFloat ? "float" : "PCM");
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        IWaveIn? capture;
        Task? stopped;
        lock (_sync)
        {
            if (!IsCapturing || _capture is null)
            {
                return;
            }

            IsCapturing = false;
            capture = _capture;
            stopped = _stopSignal?.Task;
        }

        capture.StopRecording();

        if (stopped is not null)
        {
            // RecordingStopped fires on the capture thread; bound the wait so a stuck
            // driver can't hang shutdown.
            await Task.WhenAny(stopped, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);

        lock (_sync)
        {
            _disposed = true;
            if (_capture is not null)
            {
                _capture.DataAvailable -= OnDataAvailable;
                _capture.RecordingStopped -= OnRecordingStopped;
                _capture.Dispose();
                _capture = null;
            }
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0)
        {
            return;
        }

        // NAudio reuses e.Buffer between callbacks — copy what we keep.
        var buffer = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, buffer, 0, e.BytesRecorded);

        var frame = new AudioFrame
        {
            Buffer = buffer,
            Format = Format,
            Timestamp = _elapsed.Elapsed,
        };

        DataAvailable?.Invoke(this, frame);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        IsCapturing = false;
        _stopSignal?.TrySetResult(null);

        if (e.Exception is not null)
        {
            _logger.LogError(e.Exception, "{Source} capture stopped unexpectedly.", SourceName);
            CaptureFailed?.Invoke(this, e.Exception);
        }
    }
}
