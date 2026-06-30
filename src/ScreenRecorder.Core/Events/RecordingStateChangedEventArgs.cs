using ScreenRecorder.Core.Enums;

namespace ScreenRecorder.Core.Events;

/// <summary>Raised by <c>IRecordingOrchestrator</c> on every state transition.</summary>
public sealed class RecordingStateChangedEventArgs : EventArgs
{
    public RecordingStateChangedEventArgs(RecordingState oldState, RecordingState newState, Exception? error = null)
    {
        OldState = oldState;
        NewState = newState;
        Error = error;
    }

    public RecordingState OldState { get; }
    public RecordingState NewState { get; }

    /// <summary>The fault that caused a transition to <see cref="RecordingState.Error"/>, if any.</summary>
    public Exception? Error { get; }
}
