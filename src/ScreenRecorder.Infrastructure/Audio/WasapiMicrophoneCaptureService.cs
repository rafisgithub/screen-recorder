using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Infrastructure.Audio;

/// <summary>
/// Captures microphone audio with WASAPI (via NAudio's <c>WasapiCapture</c>).
/// </summary>
/// <remarks>MILESTONE 4 — audio pipeline. Currently a scaffold stub.</remarks>
public sealed class WasapiMicrophoneCaptureService : IMicrophoneCaptureService
{
    private readonly ILogger<WasapiMicrophoneCaptureService> _logger;

    public WasapiMicrophoneCaptureService(ILogger<WasapiMicrophoneCaptureService> logger) => _logger = logger;

    public bool IsCapturing { get; private set; }

    public AudioFormat Format { get; private set; } = AudioFormat.Float32Stereo48k;

#pragma warning disable CS0067 // Wired up in Milestone 4.
    public event EventHandler<AudioFrame>? DataAvailable;
    public event EventHandler<Exception>? CaptureFailed;
#pragma warning restore CS0067

    public Task StartAsync(string? deviceId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("WasapiMicrophoneCaptureService.StartAsync invoked before Milestone 4 is implemented.");
        throw new NotImplementedException("WASAPI microphone capture is implemented in Milestone 4.");
    }

    public Task StopAsync()
    {
        IsCapturing = false;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
