namespace ScreenRecorder.Core.Enums;

/// <summary>How the encoder allocates bits across the stream.</summary>
public enum RateControlMode
{
    /// <summary>
    /// Constant quality (CRF / CQP). Best for archival / upload sources where
    /// file size is flexible — the recommended default for YouTube masters.
    /// </summary>
    ConstantQuality = 0,

    /// <summary>Constant bitrate — predictable file size, for streaming targets.</summary>
    ConstantBitrate,

    /// <summary>Variable bitrate constrained by a maximum — a balanced compromise.</summary>
    VariableBitrate,
}
