using System.Reflection;
using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;

namespace ScreenRecorder.App.ViewModels;

/// <summary>Root view model; composes the recording and settings view models.</summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly ILogger<MainViewModel> _logger;
    private bool _isInitialized;

    public MainViewModel(
        RecordingViewModel recording,
        SettingsViewModel settings,
        ISettingsService settingsService,
        ILogger<MainViewModel> logger)
    {
        Recording = recording;
        Settings = settings;
        _settings = settingsService;
        _logger = logger;
    }

    public RecordingViewModel Recording { get; }

    public SettingsViewModel Settings { get; }

    public string Title => "Screen Recorder";

    public string VersionLabel { get; } = BuildVersionLabel();

    public bool IsInitialized
    {
        get => _isInitialized;
        private set => SetProperty(ref _isInitialized, value);
    }

    /// <summary>Loads persisted settings and populates the settings view model.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            await _settings.LoadAsync();
            Settings.Initialize();
            IsInitialized = true;
            _logger.LogInformation("Application initialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialization failed");
        }
    }

    private static string BuildVersionLabel()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "1.0.0";

        // Drop any source-revision metadata (e.g. "1.1.0+abc1234").
        int plus = version.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
        {
            version = version[..plus];
        }

        return "v" + version;
    }
}
