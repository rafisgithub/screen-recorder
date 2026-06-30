using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Enums;
using ScreenRecorder.Core.Events;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Infrastructure.Recording;

/// <summary>
/// Coordinates the capture → encode → mux pipeline and owns the recording
/// lifecycle. The view models depend only on this service.
/// </summary>
/// <remarks>
/// MILESTONE 6 — orchestration. The dependencies are fully wired by DI and the
/// pre-flight steps (encoder selection, output path) run today; assembling and
/// pumping the live pipeline is completed in Milestone 6.
/// </remarks>
public sealed class RecordingOrchestrator : IRecordingOrchestrator
{
    private readonly ICaptureService _capture;
    private readonly ISystemAudioCaptureService _systemAudio;
    private readonly IMicrophoneCaptureService _microphone;
    private readonly IEncoderFactory _encoderFactory;
    private readonly IHardwareCapabilityService _capabilities;
    private readonly IMediaWriter _mediaWriter;
    private readonly IOutputPathService _outputPath;
    private readonly ISystemClock _clock;
    private readonly ILogger<RecordingOrchestrator> _logger;

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
    }

    public RecordingState State { get; private set; } = RecordingState.Idle;

    public RecordingStatistics Statistics { get; private set; } = RecordingStatistics.Empty;

    public event EventHandler<RecordingStateChangedEventArgs>? StateChanged;

#pragma warning disable CS0067 // Pushed on a timer in Milestone 6.
    public event EventHandler<RecordingStatistics>? StatisticsUpdated;
#pragma warning restore CS0067

    public Task StartAsync(RecordingSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.Target is null)
        {
            throw new InvalidOperationException("Select a capture target before recording.");
        }

        // Pre-flight: real, working decision logic that exercises the wiring.
        var capabilities = _capabilities.Detect();
        var encoder = _encoderFactory.SelectVideoEncoder(settings.Video, capabilities);
        var outputPath = _outputPath.BuildOutputPath(settings);

        _logger.LogInformation(
            "Pre-flight at {Time:T}: target='{Target}', encoder='{Encoder}', output='{Output}'",
            _clock.LocalNow, settings.Target.DisplayName, encoder.DisplayName, Path.GetFileName(outputPath));

        Statistics = Statistics with { EncoderName = encoder.DisplayName, OutputPath = outputPath };

        // MILESTONE 6: build the pipeline (capture + audio sources → encoders →
        // muxer), start the sources, pump frames, and transition to Recording.
        throw new NotImplementedException(
            $"Recording pipeline assembly is implemented in Milestone 6. " +
            $"(Would encode with '{encoder.DisplayName}' to '{Path.GetFileName(outputPath)}'.)");
    }

    public Task PauseAsync() =>
        throw new NotImplementedException("Pause/resume is implemented in Milestone 6.");

    public Task ResumeAsync() =>
        throw new NotImplementedException("Pause/resume is implemented in Milestone 6.");

    public Task<string?> StopAsync() => Task.FromResult<string?>(null);

    public async ValueTask DisposeAsync()
    {
        await _capture.DisposeAsync().ConfigureAwait(false);
        await _systemAudio.DisposeAsync().ConfigureAwait(false);
        await _microphone.DisposeAsync().ConfigureAwait(false);
        await _mediaWriter.DisposeAsync().ConfigureAwait(false);
    }

    // Reserved for Milestone 6: centralizes state transitions + notifications.
    private void SetState(RecordingState next, Exception? error = null)
    {
        var previous = State;
        State = next;
        StateChanged?.Invoke(this, new RecordingStateChangedEventArgs(previous, next, error));
    }
}
