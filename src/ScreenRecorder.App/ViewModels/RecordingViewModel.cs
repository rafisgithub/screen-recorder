using Microsoft.Extensions.Logging;
using ScreenRecorder.App.Commands;
using ScreenRecorder.App.Services;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Enums;
using ScreenRecorder.Core.Events;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.App.ViewModels;

/// <summary>
/// Drives the record / pause / stop controls and surfaces live state and
/// statistics. Talks only to <see cref="IRecordingOrchestrator"/>.
/// </summary>
public sealed class RecordingViewModel : ViewModelBase
{
    private readonly IRecordingOrchestrator _orchestrator;
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialog;
    private readonly IUiDispatcher _ui;
    private readonly ILogger<RecordingViewModel> _logger;

    private RecordingState _state = RecordingState.Idle;
    private RecordingStatistics _statistics = RecordingStatistics.Empty;
    private string _statusMessage = "Ready";

    public RecordingViewModel(
        IRecordingOrchestrator orchestrator,
        ISettingsService settings,
        IDialogService dialog,
        IUiDispatcher ui,
        ILogger<RecordingViewModel> logger)
    {
        _orchestrator = orchestrator;
        _settings = settings;
        _dialog = dialog;
        _ui = ui;
        _logger = logger;

        StartCommand = new AsyncRelayCommand(StartAsync, () => CanStart);
        StopCommand = new AsyncRelayCommand(StopAsync, () => IsRecording || IsPaused);
        PauseResumeCommand = new AsyncRelayCommand(PauseResumeAsync, () => IsRecording || IsPaused);

        _orchestrator.StateChanged += OnStateChanged;
        _orchestrator.StatisticsUpdated += OnStatisticsUpdated;
        _state = _orchestrator.State;
    }

    public AsyncRelayCommand StartCommand { get; }
    public AsyncRelayCommand StopCommand { get; }
    public AsyncRelayCommand PauseResumeCommand { get; }

    public RecordingState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                RaiseStateDerived();
            }
        }
    }

    public bool IsRecording => State == RecordingState.Recording;
    public bool IsPaused => State == RecordingState.Paused;
    public bool IsIdle => State is RecordingState.Idle or RecordingState.Error;
    public bool CanStart => State is RecordingState.Idle or RecordingState.Error;

    public string StateText => State switch
    {
        RecordingState.Idle => "Ready",
        RecordingState.Starting => "Starting…",
        RecordingState.Recording => "Recording",
        RecordingState.Paused => "Paused",
        RecordingState.Stopping => "Finalizing…",
        RecordingState.Error => "Error",
        _ => string.Empty,
    };

    public string PauseResumeText => IsPaused ? "Resume" : "Pause";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public RecordingStatistics Statistics
    {
        get => _statistics;
        private set
        {
            if (SetProperty(ref _statistics, value))
            {
                RaiseStatisticsDerived();
            }
        }
    }

    public string ElapsedDisplay => Statistics.Elapsed.ToString(@"hh\:mm\:ss");
    public string FpsDisplay => $"{Statistics.CurrentFps:0.#} fps";
    public string FileSizeDisplay => Statistics.OutputMegabytes >= 1024
        ? $"{Statistics.OutputMegabytes / 1024d:0.00} GB"
        : $"{Statistics.OutputMegabytes:0.0} MB";
    public string EncoderDisplay => string.IsNullOrEmpty(Statistics.EncoderName) ? "—" : Statistics.EncoderName;
    public string OutputPathDisplay => string.IsNullOrEmpty(Statistics.OutputPath) ? "—" : Statistics.OutputPath;

    private async Task StartAsync()
    {
        try
        {
            StatusMessage = "Starting…";
            await _orchestrator.StartAsync(_settings.Current);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Start request could not be completed");
            StatusMessage = ex.Message;
            _dialog.ShowInfo("Not available yet", ex.Message);
        }
    }

    private async Task StopAsync()
    {
        try
        {
            var path = await _orchestrator.StopAsync();
            StatusMessage = path is null ? "Stopped." : $"Saved to {path}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stop failed");
            StatusMessage = ex.Message;
            _dialog.ShowError("Stop failed", ex.Message);
        }
    }

    private async Task PauseResumeAsync()
    {
        try
        {
            if (IsPaused)
            {
                await _orchestrator.ResumeAsync();
            }
            else
            {
                await _orchestrator.PauseAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pause/resume could not be completed");
            StatusMessage = ex.Message;
            _dialog.ShowInfo("Not available yet", ex.Message);
        }
    }

    private void OnStateChanged(object? sender, RecordingStateChangedEventArgs e) => _ui.Post(() =>
    {
        State = e.NewState;
        if (e.Error is not null)
        {
            StatusMessage = e.Error.Message;
        }
    });

    private void OnStatisticsUpdated(object? sender, RecordingStatistics stats) => _ui.Post(() => Statistics = stats);

    private void RaiseStateDerived()
    {
        OnPropertyChanged(nameof(IsRecording));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(PauseResumeText));
        StartCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        PauseResumeCommand.RaiseCanExecuteChanged();
    }

    private void RaiseStatisticsDerived()
    {
        OnPropertyChanged(nameof(ElapsedDisplay));
        OnPropertyChanged(nameof(FpsDisplay));
        OnPropertyChanged(nameof(FileSizeDisplay));
        OnPropertyChanged(nameof(EncoderDisplay));
        OnPropertyChanged(nameof(OutputPathDisplay));
    }
}
