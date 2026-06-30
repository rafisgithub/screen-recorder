using ScreenRecorder.Core.Enums;
using ScreenRecorder.Core.Events;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Core.Abstractions;

/// <summary>
/// The top-level recording engine. Wires capture + audio + encoding into a
/// pipeline, owns the lifecycle and A/V synchronization, and surfaces state and
/// live statistics to the UI. This is the single service the view models talk to.
/// </summary>
public interface IRecordingOrchestrator : IAsyncDisposable
{
    RecordingState State { get; }

    RecordingStatistics Statistics { get; }

    event EventHandler<RecordingStateChangedEventArgs>? StateChanged;

    event EventHandler<RecordingStatistics>? StatisticsUpdated;

    Task StartAsync(RecordingSettings settings, CancellationToken cancellationToken = default);

    Task PauseAsync();

    Task ResumeAsync();

    /// <summary>Stops, finalizes the file, and returns its path (or <c>null</c> on failure).</summary>
    Task<string?> StopAsync();
}
