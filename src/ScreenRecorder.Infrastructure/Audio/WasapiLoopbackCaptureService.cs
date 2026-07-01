using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using ScreenRecorder.Core.Abstractions;

namespace ScreenRecorder.Infrastructure.Audio;

/// <summary>
/// Captures system/desktop audio with WASAPI loopback (NAudio's
/// <see cref="WasapiLoopbackCapture"/>) on a render endpoint.
/// </summary>
/// <remarks>MILESTONE 4 — audio pipeline.</remarks>
public sealed class WasapiLoopbackCaptureService : WasapiCaptureServiceBase, ISystemAudioCaptureService
{
    private readonly ILogger<WasapiLoopbackCaptureService> _logger;

    public WasapiLoopbackCaptureService(ILogger<WasapiLoopbackCaptureService> logger)
        : base(logger) => _logger = logger;

    protected override string SourceName => "System audio";

    protected override IWaveIn CreateCapture(string? deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            return new WasapiLoopbackCapture();
        }

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDevice(deviceId);
            return new WasapiLoopbackCapture(device);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Render endpoint '{DeviceId}' unavailable; using the default playback device.", deviceId);
            return new WasapiLoopbackCapture();
        }
    }
}
