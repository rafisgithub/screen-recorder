using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Infrastructure.Audio;

/// <summary>
/// Captures system/desktop audio with WASAPI loopback (via NAudio's
/// <c>WasapiLoopbackCapture</c>).
/// </summary>
/// <remarks>MILESTONE 4 — audio pipeline. Currently a scaffold stub.</remarks>
public sealed class WasapiLoopbackCaptureService : ISystemAudioCaptureService
{
    private readonly ILogger<WasapiLoopbackCaptureService> _logger;

    public WasapiLoopbackCaptureService(ILogger<WasapiLoopbackCaptureService> logger) => _logger = logger;

    public bool IsCapturing { get; private set; }

    public AudioFormat Format { get; private set; } = AudioFormat.Float32Stereo48k;

#pragma warning disable CS0067 // Wired up in Milestone 4.
    public event EventHandler<AudioFrame>? DataAvailable;
    public event EventHandler<Exception>? CaptureFailed;
#pragma warning restore CS0067

    public Task StartAsync(string? deviceId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("WasapiLoopbackCaptureService.StartAsync invoked before Milestone 4 is implemented.");
        throw new NotImplementedException("WASAPI loopback capture is implemented in Milestone 4.");
    }

    public Task StopAsync()
    {
        IsCapturing = false;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
