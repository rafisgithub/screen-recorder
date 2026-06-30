using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Infrastructure.Settings;

/// <summary>
/// Persists <see cref="RecordingSettings"/> as indented JSON under
/// <c>%AppData%\YouTubeScreenRecorder\settings.json</c>. The file path is
/// injectable so tests can point at a temp directory.
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    private readonly ILogger<JsonSettingsService> _logger;
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;

    private RecordingSettings _current = RecordingSettings.CreateDefault();

    public JsonSettingsService(ILogger<JsonSettingsService> logger, string? filePath = null)
    {
        _logger = logger;
        _filePath = filePath ?? GetDefaultSettingsPath();
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };
    }

    public RecordingSettings Current => _current;

    public event EventHandler<RecordingSettings>? SettingsChanged;

    public async Task<RecordingSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (File.Exists(_filePath))
            {
                await using var stream = File.OpenRead(_filePath);
                var loaded = await JsonSerializer
                    .DeserializeAsync<RecordingSettings>(stream, _jsonOptions, cancellationToken)
                    .ConfigureAwait(false);

                if (loaded is not null)
                {
                    _current = loaded;
                    _logger.LogInformation("Loaded settings from {Path}", _filePath);
                }
            }
            else
            {
                _logger.LogInformation("No settings file at {Path}; using defaults", _filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings; falling back to defaults");
            _current = RecordingSettings.CreateDefault();
        }

        SettingsChanged?.Invoke(this, _current);
        return _current;
    }

    public async Task SaveAsync(RecordingSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using (var stream = File.Create(_filePath))
        {
            await JsonSerializer
                .SerializeAsync(stream, settings, _jsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        _current = settings;
        _logger.LogInformation("Saved settings to {Path}", _filePath);
        SettingsChanged?.Invoke(this, _current);
    }

    private static string GetDefaultSettingsPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YouTubeScreenRecorder",
        "settings.json");
}
