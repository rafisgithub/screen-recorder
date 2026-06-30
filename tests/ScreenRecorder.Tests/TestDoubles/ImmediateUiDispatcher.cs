using ScreenRecorder.App.Services;

namespace ScreenRecorder.Tests.TestDoubles;

/// <summary>Runs dispatched work synchronously so view-model tests are deterministic.</summary>
public sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public bool HasAccess => true;

    public void Post(Action action) => action();

    public Task InvokeAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
