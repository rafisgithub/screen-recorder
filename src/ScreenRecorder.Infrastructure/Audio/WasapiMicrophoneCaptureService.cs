using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using ScreenRecorder.Core.Abstractions;

namespace ScreenRecorder.Infrastructure.Audio;

/// <summary>
/// Captures microphone audio with WASAPI (NAudio's <see cref="WasapiCapture"/>)
/// on a capture endpoint.
/// </summary>
/// <remarks>MILESTONE 4 — audio pipeline.</remarks>
public sealed class WasapiMicrophoneCaptureService : WasapiCaptureServiceBase, IMicrophoneCaptureService
{
    private const int BufferMilliseconds = 50;

    private readonly ILogger<WasapiMicrophoneCaptureService> _logger;

    public WasapiMicrophoneCaptureService(ILogger<WasapiMicrophoneCaptureService> logger)
        : base(logger) => _logger = logger;

    protected override string SourceName => "Microphone";

    protected override IWaveIn CreateCapture(string? deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            return new WasapiCapture(WasapiCapture.GetDefaultCaptureDevice(), useEventSync: true, BufferMilliseconds);
        }

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDevice(deviceId);
            return new WasapiCapture(device, useEventSync: true, BufferMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Capture endpoint '{DeviceId}' unavailable; using the default microphone.", deviceId);
            return new WasapiCapture(WasapiCapture.GetDefaultCaptureDevice(), useEventSync: true, BufferMilliseconds);
        }
    }
}
