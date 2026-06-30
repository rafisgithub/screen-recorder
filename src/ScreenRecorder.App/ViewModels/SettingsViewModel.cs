using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using ScreenRecorder.App.Commands;
using ScreenRecorder.App.Services;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Enums;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.App.ViewModels;

/// <summary>
/// Edits the <see cref="RecordingSettings"/> the orchestrator will use. Operates
/// on the live settings instance so edits take effect immediately; the Save
/// command persists them to disk.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private static readonly AudioDeviceInfo DefaultRenderDevice = new()
    {
        Id = string.Empty,
        DisplayName = "System default playback",
        Kind = AudioDeviceKind.Render,
        IsDefault = true,
    };

    private static readonly AudioDeviceInfo DefaultCaptureDevice = new()
    {
        Id = string.Empty,
        DisplayName = "System default microphone",
        Kind = AudioDeviceKind.Capture,
        IsDefault = true,
    };

    private readonly ISettingsService _settingsService;
    private readonly ICaptureTargetProvider _targets;
    private readonly IAudioDeviceProvider _audioDevices;
    private readonly IHardwareCapabilityService _capabilities;
    private readonly IEncoderFactory _encoderFactory;
    private readonly IDialogService _dialog;
    private readonly ILogger<SettingsViewModel> _logger;

    private RecordingSettings _working;
    private CaptureTarget? _selectedTarget;
    private AudioDeviceInfo _selectedRenderDevice = DefaultRenderDevice;
    private AudioDeviceInfo _selectedCaptureDevice = DefaultCaptureDevice;

    public SettingsViewModel(
        ISettingsService settingsService,
        ICaptureTargetProvider targets,
        IAudioDeviceProvider audioDevices,
        IHardwareCapabilityService capabilities,
        IEncoderFactory encoderFactory,
        IDialogService dialog,
        ILogger<SettingsViewModel> logger)
    {
        _settingsService = settingsService;
        _targets = targets;
        _audioDevices = audioDevices;
        _capabilities = capabilities;
        _encoderFactory = encoderFactory;
        _dialog = dialog;
        _logger = logger;
        _working = settingsService.Current;

        PickTargetCommand = new AsyncRelayCommand(PickTargetAsync);
        BrowseOutputCommand = new RelayCommand(BrowseOutput);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ResetCommand = new RelayCommand(ResetToDefaults);
        RefreshDevicesCommand = new RelayCommand(RefreshDevices);
    }

    public AsyncRelayCommand PickTargetCommand { get; }
    public RelayCommand BrowseOutputCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand RefreshDevicesCommand { get; }

    // ---- Option sources --------------------------------------------------
    public IReadOnlyList<Resolution> ResolutionOptions { get; } =
        new[] { Resolution.Hd720, Resolution.FullHd1080, Resolution.Qhd1440, Resolution.Uhd2160 };

    public IReadOnlyList<int> FrameRateOptions { get; } = new[] { 24, 30, 48, 60, 120 };

    public IReadOnlyList<VideoCodec> CodecOptions { get; } = Enum.GetValues<VideoCodec>();

    public IReadOnlyList<RateControlMode> RateControlOptions { get; } = Enum.GetValues<RateControlMode>();

    public ObservableCollection<EncoderDescriptor> AvailableEncoders { get; } = new();
    public ObservableCollection<CaptureTarget> Targets { get; } = new();
    public ObservableCollection<AudioDeviceInfo> RenderDevices { get; } = new();
    public ObservableCollection<AudioDeviceInfo> CaptureDevices { get; } = new();

    // ---- Video -----------------------------------------------------------
    public Resolution SelectedResolution
    {
        get => _working.Video.Resolution;
        set { if (_working.Video.Resolution != value) { _working.Video.Resolution = value; OnPropertyChanged(); } }
    }

    public int SelectedFrameRate
    {
        get => _working.Video.FrameRate;
        set { if (_working.Video.FrameRate != value) { _working.Video.FrameRate = value; OnPropertyChanged(); } }
    }

    public VideoCodec SelectedCodec
    {
        get => _working.Video.Codec;
        set
        {
            if (_working.Video.Codec != value)
            {
                _working.Video.Codec = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedEncoderPreview));
            }
        }
    }

    public RateControlMode SelectedRateControl
    {
        get => _working.Video.RateControl;
        set
        {
            if (_working.Video.RateControl != value)
            {
                _working.Video.RateControl = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsQualityMode));
                OnPropertyChanged(nameof(IsBitrateMode));
            }
        }
    }

    public int QualityLevel
    {
        get => _working.Video.QualityLevel;
        set { if (_working.Video.QualityLevel != value) { _working.Video.QualityLevel = value; OnPropertyChanged(); } }
    }

    public int BitrateKbps
    {
        get => _working.Video.BitrateKbps;
        set { if (_working.Video.BitrateKbps != value) { _working.Video.BitrateKbps = value; OnPropertyChanged(); } }
    }

    public double KeyframeIntervalSeconds
    {
        get => _working.Video.KeyframeIntervalSeconds;
        set { if (Math.Abs(_working.Video.KeyframeIntervalSeconds - value) > double.Epsilon) { _working.Video.KeyframeIntervalSeconds = value; OnPropertyChanged(); } }
    }

    public bool PreferHardwareEncoding
    {
        get => _working.Video.PreferHardwareEncoding;
        set
        {
            if (_working.Video.PreferHardwareEncoding != value)
            {
                _working.Video.PreferHardwareEncoding = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedEncoderPreview));
            }
        }
    }

    public bool IsQualityMode => SelectedRateControl == RateControlMode.ConstantQuality;
    public bool IsBitrateMode => !IsQualityMode;

    /// <summary>Live preview of the encoder the factory would pick for the current choices.</summary>
    public string SelectedEncoderPreview =>
        _encoderFactory.SelectVideoEncoder(_working.Video, _capabilities.Detect()).DisplayName;

    // ---- Audio -----------------------------------------------------------
    public bool CaptureSystemAudio
    {
        get => _working.Audio.CaptureSystemAudio;
        set { if (_working.Audio.CaptureSystemAudio != value) { _working.Audio.CaptureSystemAudio = value; OnPropertyChanged(); } }
    }

    public bool CaptureMicrophone
    {
        get => _working.Audio.CaptureMicrophone;
        set { if (_working.Audio.CaptureMicrophone != value) { _working.Audio.CaptureMicrophone = value; OnPropertyChanged(); } }
    }

    public int AudioBitrateKbps
    {
        get => _working.Audio.BitrateKbps;
        set { if (_working.Audio.BitrateKbps != value) { _working.Audio.BitrateKbps = value; OnPropertyChanged(); } }
    }

    public AudioDeviceInfo SelectedRenderDevice
    {
        get => _selectedRenderDevice;
        set
        {
            if (SetProperty(ref _selectedRenderDevice, value))
            {
                _working.Audio.SystemAudioDeviceId = string.IsNullOrEmpty(value?.Id) ? null : value.Id;
            }
        }
    }

    public AudioDeviceInfo SelectedCaptureDevice
    {
        get => _selectedCaptureDevice;
        set
        {
            if (SetProperty(ref _selectedCaptureDevice, value))
            {
                _working.Audio.MicrophoneDeviceId = string.IsNullOrEmpty(value?.Id) ? null : value.Id;
            }
        }
    }

    // ---- Output ----------------------------------------------------------
    public string OutputDirectory
    {
        get => _working.OutputDirectory;
        set { if (_working.OutputDirectory != value) { _working.OutputDirectory = value; OnPropertyChanged(); } }
    }

    public string FileNameTemplate
    {
        get => _working.FileNameTemplate;
        set { if (_working.FileNameTemplate != value) { _working.FileNameTemplate = value; OnPropertyChanged(); } }
    }

    public bool CaptureCursor
    {
        get => _working.CaptureCursor;
        set { if (_working.CaptureCursor != value) { _working.CaptureCursor = value; OnPropertyChanged(); } }
    }

    // ---- Capture target --------------------------------------------------
    public CaptureTarget? SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            if (SetProperty(ref _selectedTarget, value))
            {
                _working.Target = value;
                OnPropertyChanged(nameof(TargetDisplay));
            }
        }
    }

    public string TargetDisplay => _selectedTarget?.DisplayName ?? "None selected";

    /// <summary>Populates option lists from the (now loaded) settings and providers.</summary>
    public void Initialize()
    {
        _working = _settingsService.Current;

        if (string.IsNullOrWhiteSpace(_working.OutputDirectory))
        {
            _working.OutputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        }

        AvailableEncoders.Clear();
        foreach (var encoder in _capabilities.Detect().VideoEncoders)
        {
            AvailableEncoders.Add(encoder);
        }

        RefreshDevices();
        RefreshTargets();
        OnPropertyChanged(string.Empty); // refresh every bound property
    }

    private void RefreshTargets()
    {
        Targets.Clear();
        foreach (var window in _targets.GetWindows())
        {
            Targets.Add(window);
        }

        foreach (var monitor in _targets.GetMonitors())
        {
            Targets.Add(monitor.ToCaptureTarget());
        }

        if (Targets.Count == 0)
        {
            // Placeholder until the enumeration/capture milestone; represents the
            // full primary display so the rest of the flow is demonstrable.
            Targets.Add(CaptureTarget.ForMonitor(
                "primary", "Primary Display (full screen)", nint.Zero, new PixelRect(0, 0, 1920, 1080)));
        }

        SelectedTarget = _working.Target ?? Targets.FirstOrDefault();
    }

    private void RefreshDevices()
    {
        RenderDevices.Clear();
        RenderDevices.Add(DefaultRenderDevice);
        foreach (var device in _audioDevices.GetRenderDevices())
        {
            RenderDevices.Add(device);
        }

        CaptureDevices.Clear();
        CaptureDevices.Add(DefaultCaptureDevice);
        foreach (var device in _audioDevices.GetCaptureDevices())
        {
            CaptureDevices.Add(device);
        }

        SelectedRenderDevice = RenderDevices.FirstOrDefault(d => d.Id == (_working.Audio.SystemAudioDeviceId ?? string.Empty)) ?? DefaultRenderDevice;
        SelectedCaptureDevice = CaptureDevices.FirstOrDefault(d => d.Id == (_working.Audio.MicrophoneDeviceId ?? string.Empty)) ?? DefaultCaptureDevice;
    }

    private async Task PickTargetAsync()
    {
        try
        {
            var target = await _targets.PickTargetAsync();
            if (target is not null)
            {
                if (!Targets.Contains(target))
                {
                    Targets.Add(target);
                }

                SelectedTarget = target;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Target picker is not available yet");
            _dialog.ShowInfo("Not available yet", ex.Message);
        }
    }

    private void BrowseOutput()
    {
        var directory = _dialog.BrowseForFolder("Choose where recordings are saved", OutputDirectory);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            OutputDirectory = directory;
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            await _settingsService.SaveAsync(_working);
            _dialog.ShowInfo("Settings", "Your settings have been saved.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saving settings failed");
            _dialog.ShowError("Save failed", ex.Message);
        }
    }

    private void ResetToDefaults()
    {
        var defaults = RecordingSettings.CreateDefault();
        _working.Video = defaults.Video;
        _working.Audio = defaults.Audio;
        _working.OutputDirectory = defaults.OutputDirectory;
        _working.FileNameTemplate = defaults.FileNameTemplate;
        _working.CaptureCursor = defaults.CaptureCursor;

        RefreshDevices();
        OnPropertyChanged(string.Empty);
    }
}
