using System.Text.Json.Serialization;

namespace ScreenRecorder.Core.Models;

/// <summary>
/// The complete, persisted configuration for a recording session: video, audio,
/// the capture target, and output file options.
/// </summary>
public sealed class RecordingSettings
{
    /// <summary>Video encoding settings.</summary>
    public VideoSettings Video { get; set; } = new();

    /// <summary>Audio capture/encoding settings.</summary>
    public AudioSettings Audio { get; set; } = new();

    /// <summary>
    /// What to capture. <c>null</c> means "not chosen yet" — the orchestrator
    /// requires a target before starting. Not persisted: it carries a live OS
    /// handle that is only meaningful within the current session.
    /// </summary>
    [JsonIgnore]
    public CaptureTarget? Target { get; set; }

    /// <summary>Folder the output MP4 is written to.</summary>
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>
    /// File name template. <c>{timestamp}</c> is expanded by the output-path
    /// service; the <c>.mp4</c> extension is appended automatically.
    /// </summary>
    public string FileNameTemplate { get; set; } = "Recording_{timestamp}";

    /// <summary>Render the mouse cursor into the captured video.</summary>
    public bool CaptureCursor { get; set; } = true;

    /// <summary>Settings tuned for a 1080p60 YouTube master in the user's Videos folder.</summary>
    public static RecordingSettings CreateDefault() => new()
    {
        Video = new VideoSettings(),
        Audio = new AudioSettings(),
        OutputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        FileNameTemplate = "Recording_{timestamp}",
        CaptureCursor = true,
    };

    /// <summary>Deep copy — used by the settings editor so edits can be cancelled.</summary>
    public RecordingSettings Clone() => new()
    {
        Video = Video.Clone(),
        Audio = Audio.Clone(),
        Target = Target,
        OutputDirectory = OutputDirectory,
        FileNameTemplate = FileNameTemplate,
        CaptureCursor = CaptureCursor,
    };
}
