using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Core.Abstractions;

/// <summary>
/// Builds the destination file path from settings — expanding the file-name
/// template, stamping the time, ensuring the directory exists, and de-duplicating.
/// </summary>
public interface IOutputPathService
{
    /// <summary>
    /// Resolves the full <c>.mp4</c> path for a new recording, creating the output
    /// directory if needed and guaranteeing the name is unique.
    /// </summary>
    string BuildOutputPath(RecordingSettings settings);

    /// <summary>Appends " (n)" until the path does not already exist.</summary>
    string EnsureUniquePath(string path);
}
