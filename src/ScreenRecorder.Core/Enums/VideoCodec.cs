namespace ScreenRecorder.Core.Enums;

/// <summary>Video codec used for the output MP4 stream.</summary>
public enum VideoCodec
{
    /// <summary>H.264 / AVC — the most compatible choice for YouTube.</summary>
    H264 = 0,

    /// <summary>H.265 / HEVC — better compression, less universal support.</summary>
    Hevc,
}
