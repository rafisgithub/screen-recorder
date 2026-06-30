namespace ScreenRecorder.Core.Models;

/// <summary>
/// An immutable snapshot of live recording metrics, pushed to the UI on a timer
/// by the orchestrator.
/// </summary>
public sealed record RecordingStatistics
{
    public TimeSpan Elapsed { get; init; }
    public long CapturedFrames { get; init; }
    public long EncodedFrames { get; init; }
    public long DroppedFrames { get; init; }

    /// <summary>Instantaneous encoding throughput in FPS.</summary>
    public double CurrentFps { get; init; }

    public long OutputBytes { get; init; }

    /// <summary>Observed average output bitrate (kbps).</summary>
    public double OutputBitrateKbps { get; init; }

    public string EncoderName { get; init; } = string.Empty;
    public string OutputPath { get; init; } = string.Empty;

    public double OutputMegabytes => OutputBytes / (1024d * 1024d);

    public static RecordingStatistics Empty { get; } = new();
}
