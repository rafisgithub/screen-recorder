using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Core.Abstractions;

/// <summary>
/// Selects and instantiates encoders. <see cref="SelectVideoEncoder"/> is pure
/// decision logic (preferred vendor → HW → SW fallback) and is unit-tested
/// independently of any native code.
/// </summary>
public interface IEncoderFactory
{
    /// <summary>
    /// Chooses the best encoder for the requested settings given what the machine
    /// supports, honoring hardware preference and falling back to software.
    /// </summary>
    EncoderDescriptor SelectVideoEncoder(VideoSettings settings, HardwareCapabilities capabilities);

    IVideoEncoder CreateVideoEncoder(EncoderDescriptor descriptor);

    IAudioEncoder CreateAudioEncoder(AudioSettings settings);
}
