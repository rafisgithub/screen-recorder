namespace ScreenRecorder.Core.Models;

/// <summary>
/// A PCM audio format descriptor, defined independently of NAudio so it can
/// cross the Core boundary.
/// </summary>
public readonly record struct AudioFormat(int SampleRate, int Channels, int BitsPerSample, bool IsFloat)
{
    /// <summary>Bytes per sample frame (all channels).</summary>
    public int BlockAlign => Channels * (BitsPerSample / 8);

    /// <summary>Bytes per second of audio.</summary>
    public int AverageBytesPerSecond => SampleRate * BlockAlign;

    public static AudioFormat Pcm16Stereo48k { get; } = new(48_000, 2, 16, IsFloat: false);
    public static AudioFormat Float32Stereo48k { get; } = new(48_000, 2, 32, IsFloat: true);

    public override string ToString() =>
        $"{SampleRate} Hz, {Channels} ch, {(IsFloat ? "f" : "s")}{BitsPerSample}";
}
