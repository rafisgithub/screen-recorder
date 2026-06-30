namespace ScreenRecorder.Core.Enums;

/// <summary>
/// GPU vendor backing a hardware encoder, in the order we prefer to probe them.
/// </summary>
public enum HardwareVendor
{
    /// <summary>No hardware encoder; CPU/software encoding (x264/x265).</summary>
    None = 0,

    /// <summary>NVIDIA NVENC.</summary>
    Nvidia,

    /// <summary>Intel Quick Sync Video (QSV).</summary>
    Intel,

    /// <summary>AMD Advanced Media Framework (AMF/VCE).</summary>
    Amd,
}
