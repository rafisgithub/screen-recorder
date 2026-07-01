using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Enums;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Infrastructure.Audio;

/// <summary>
/// Enumerates WASAPI render/capture endpoints via NAudio's
/// <see cref="MMDeviceEnumerator"/>.
/// </summary>
/// <remarks>MILESTONE 1 / 4 — device enumeration.</remarks>
public sealed class AudioDeviceProvider : IAudioDeviceProvider
{
    private readonly ILogger<AudioDeviceProvider> _logger;

    public AudioDeviceProvider(ILogger<AudioDeviceProvider> logger) => _logger = logger;

    public IReadOnlyList<AudioDeviceInfo> GetRenderDevices() => Enumerate(DataFlow.Render, AudioDeviceKind.Render);

    public IReadOnlyList<AudioDeviceInfo> GetCaptureDevices() => Enumerate(DataFlow.Capture, AudioDeviceKind.Capture);

    public AudioDeviceInfo? GetDefaultRenderDevice() => GetDefault(DataFlow.Render, Role.Multimedia, AudioDeviceKind.Render);

    public AudioDeviceInfo? GetDefaultCaptureDevice() => GetDefault(DataFlow.Capture, Role.Console, AudioDeviceKind.Capture);

    private IReadOnlyList<AudioDeviceInfo> Enumerate(DataFlow flow, AudioDeviceKind kind)
    {
        var devices = new List<AudioDeviceInfo>();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            string? defaultId = TryGetDefaultId(enumerator, flow, kind == AudioDeviceKind.Capture ? Role.Console : Role.Multimedia);

            foreach (var device in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
            {
                using (device)
                {
                    devices.Add(new AudioDeviceInfo
                    {
                        Id = device.ID,
                        DisplayName = device.FriendlyName,
                        Kind = kind,
                        IsDefault = device.ID == defaultId,
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Enumerating {Flow} audio endpoints failed.", flow);
        }

        return devices;
    }

    private AudioDeviceInfo? GetDefault(DataFlow flow, Role role, AudioDeviceKind kind)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            if (!enumerator.HasDefaultAudioEndpoint(flow, role))
            {
                return null;
            }

            using var device = enumerator.GetDefaultAudioEndpoint(flow, role);
            return new AudioDeviceInfo
            {
                Id = device.ID,
                DisplayName = device.FriendlyName,
                Kind = kind,
                IsDefault = true,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Resolving default {Flow} endpoint failed.", flow);
            return null;
        }
    }

    private string? TryGetDefaultId(MMDeviceEnumerator enumerator, DataFlow flow, Role role)
    {
        try
        {
            if (!enumerator.HasDefaultAudioEndpoint(flow, role))
            {
                return null;
            }

            using var device = enumerator.GetDefaultAudioEndpoint(flow, role);
            return device.ID;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "No default {Flow} endpoint.", flow);
            return null;
        }
    }
}
