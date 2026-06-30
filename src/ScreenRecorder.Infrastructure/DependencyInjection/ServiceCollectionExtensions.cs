using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Infrastructure.Audio;
using ScreenRecorder.Infrastructure.Capture;
using ScreenRecorder.Infrastructure.Display;
using ScreenRecorder.Infrastructure.Encoding;
using ScreenRecorder.Infrastructure.IO;
using ScreenRecorder.Infrastructure.Recording;
using ScreenRecorder.Infrastructure.Settings;
using ScreenRecorder.Infrastructure.Time;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers all <c>ScreenRecorder.Infrastructure</c> services. Called from the
/// App composition root.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // --- Time -------------------------------------------------------------
        services.AddSingleton<ISystemClock, SystemClock>();

        // --- Settings & output paths -----------------------------------------
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IOutputPathService, OutputPathService>();

        // --- Capability detection & encoder selection ------------------------
        services.AddSingleton<IHardwareCapabilityService, HardwareCapabilityService>();
        services.AddSingleton<IEncoderFactory, EncoderFactory>();

        // --- Device / target enumeration -------------------------------------
        services.AddSingleton<ICaptureTargetProvider, CaptureTargetProvider>();
        services.AddSingleton<IAudioDeviceProvider, AudioDeviceProvider>();

        // --- Capture & audio sources (reusable, start/stop lifecycle) --------
        services.AddSingleton<ICaptureService, GraphicsCaptureService>();
        services.AddSingleton<ISystemAudioCaptureService, WasapiLoopbackCaptureService>();
        services.AddSingleton<IMicrophoneCaptureService, WasapiMicrophoneCaptureService>();

        // --- Muxer ------------------------------------------------------------
        services.AddSingleton<IMediaWriter, FFmpegMuxer>();

        // --- Orchestration (the engine the UI talks to) ----------------------
        services.AddSingleton<IRecordingOrchestrator, RecordingOrchestrator>();

        return services;
    }
}
