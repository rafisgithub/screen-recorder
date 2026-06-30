using System.Windows;
using System.Windows.Threading;

namespace ScreenRecorder.App.Services;

/// <summary>WPF-backed <see cref="IUiDispatcher"/> over the application dispatcher.</summary>
public sealed class WpfDispatcher : IUiDispatcher
{
    private readonly Dispatcher _dispatcher =
        Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

    public bool HasAccess => _dispatcher.CheckAccess();

    public void Post(Action action) => _dispatcher.BeginInvoke(action);

    public Task InvokeAsync(Action action) => _dispatcher.InvokeAsync(action).Task;
}
