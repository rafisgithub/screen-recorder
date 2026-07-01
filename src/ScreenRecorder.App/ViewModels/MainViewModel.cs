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

    public string Title => "YouTube Screen Recorder";

    public string VersionLabel => "v1.0.0";

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
}
