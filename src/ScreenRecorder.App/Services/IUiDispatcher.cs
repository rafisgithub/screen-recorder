namespace ScreenRecorder.App.Services;

/// <summary>
/// Marshals work onto the UI thread. Lets view models stay testable — a test
/// supplies a synchronous fake instead of the WPF dispatcher.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>Queues <paramref name="action"/> to run on the UI thread (fire-and-forget).</summary>
    void Post(Action action);

    /// <summary>Runs <paramref name="action"/> on the UI thread and awaits completion.</summary>
    Task InvokeAsync(Action action);

    /// <summary>True when the caller is already on the UI thread.</summary>
    bool HasAccess { get; }
}
