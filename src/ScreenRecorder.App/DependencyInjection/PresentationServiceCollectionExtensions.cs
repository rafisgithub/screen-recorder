using ScreenRecorder.App.Services;
using ScreenRecorder.App.ViewModels;
using ScreenRecorder.App.Views;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers presentation-layer services, view models, and windows.</summary>
public static class PresentationServiceCollectionExtensions
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddSingleton<IUiDispatcher, WpfDispatcher>();
        services.AddSingleton<IDialogService, DialogService>();

        services.AddSingleton<RecordingViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();

        services.AddSingleton<MainWindow>();

        return services;
    }
}
