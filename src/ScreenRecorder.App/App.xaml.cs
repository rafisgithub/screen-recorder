using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using ScreenRecorder.App.ViewModels;
using ScreenRecorder.App.Views;

namespace ScreenRecorder.App;

/// <summary>
/// Composition root: builds the generic host (DI + Serilog), resolves the main
/// window, and manages application lifetime.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        MigrateLegacyAppData();

        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScreenRecorder",
            "logs");
        Directory.CreateDirectory(logDirectory);

        try
        {
            _host = Host.CreateDefaultBuilder()
                .UseSerilog((_, configuration) => configuration
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .WriteTo.Console()
                    .WriteTo.File(
                        Path.Combine(logDirectory, "log-.txt"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7))
                .ConfigureServices(services =>
                {
                    services.AddInfrastructure();
                    services.AddPresentation();
                })
                .Build();

            await _host.StartAsync();

            DispatcherUnhandledException += OnDispatcherUnhandledException;

            var window = _host.Services.GetRequiredService<MainWindow>();
            var viewModel = _host.Services.GetRequiredService<MainViewModel>();
            window.DataContext = viewModel;
            MainWindow = window;
            window.Show();

            await viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fatal error during startup");
            MessageBox.Show(ex.Message, "Startup failed", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled UI exception");
        MessageBox.Show(e.Exception.Message, "Unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    /// <summary>
    /// Versions before 1.1.0 stored settings and logs under "YouTubeScreenRecorder";
    /// move them to the new "ScreenRecorder" folders once so upgrades keep user data.
    /// </summary>
    private static void MigrateLegacyAppData()
    {
        foreach (var root in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                 })
        {
            try
            {
                var legacy = Path.Combine(root, "YouTubeScreenRecorder");
                var current = Path.Combine(root, "ScreenRecorder");
                if (Directory.Exists(legacy) && !Directory.Exists(current))
                {
                    Directory.Move(legacy, current);
                }
            }
            catch (Exception)
            {
                // Migration is best-effort; the app falls back to defaults.
            }
        }
    }
}
