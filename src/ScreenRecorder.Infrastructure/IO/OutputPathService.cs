using System.Globalization;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Infrastructure.IO;

/// <summary>
/// Builds output file paths from settings. Pure, deterministic logic (time is
/// injected via <see cref="ISystemClock"/>), making it straightforward to test.
/// </summary>
public sealed class OutputPathService : IOutputPathService
{
    private const string TimestampToken = "{timestamp}";
    private readonly ISystemClock _clock;

    public OutputPathService(ISystemClock clock) => _clock = clock;

    public string BuildOutputPath(RecordingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = string.IsNullOrWhiteSpace(settings.OutputDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
            : settings.OutputDirectory;
        Directory.CreateDirectory(directory);

        var template = string.IsNullOrWhiteSpace(settings.FileNameTemplate)
            ? "Recording_{timestamp}"
            : settings.FileNameTemplate;

        var timestamp = _clock.LocalNow.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        var fileName = template.Replace(TimestampToken, timestamp, StringComparison.OrdinalIgnoreCase);
        fileName = Sanitize(fileName);

        var path = Path.Combine(directory, fileName + ".mp4");
        return EnsureUniquePath(path);
    }

    public string EnsureUniquePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        for (var index = 2; ; index++)
        {
            var candidate = Path.Combine(directory, $"{name} ({index}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private static string Sanitize(string fileName)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalid, '_');
        }

        return fileName;
    }
}
