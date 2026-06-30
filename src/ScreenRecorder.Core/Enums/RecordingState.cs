namespace ScreenRecorder.Core.Enums;

/// <summary>
/// Lifecycle states of the recording engine. Exposed by
/// <see cref="Abstractions.IRecordingOrchestrator"/> and bound by the UI to
/// drive command availability (start/stop/pause).
/// </summary>
public enum RecordingState
{
    /// <summary>No recording in progress; ready to start.</summary>
    Idle = 0,

    /// <summary>Capture/encode pipeline is spinning up.</summary>
    Starting,

    /// <summary>Actively capturing and encoding.</summary>
    Recording,

    /// <summary>Capture is suspended but the session is still open.</summary>
    Paused,

    /// <summary>Pipeline is flushing and finalizing the output file.</summary>
    Stopping,

    /// <summary>A fault terminated the session; see logs for details.</summary>
    Error,
}
