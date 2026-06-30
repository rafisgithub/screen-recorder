using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Core.Abstractions;

/// <summary>Loads and persists <see cref="RecordingSettings"/> (JSON on disk).</summary>
public interface ISettingsService
{
    /// <summary>The most recently loaded/saved settings (defaults until loaded).</summary>
    RecordingSettings Current { get; }

    event EventHandler<RecordingSettings>? SettingsChanged;

    Task<RecordingSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(RecordingSettings settings, CancellationToken cancellationToken = default);
}
