namespace ScreenRecorder.Core.Models;

/// <summary>An immutable pixel resolution with common presets.</summary>
public readonly record struct Resolution(int Width, int Height)
{
    public static readonly Resolution Hd720 = new(1280, 720);
    public static readonly Resolution FullHd1080 = new(1920, 1080);
    public static readonly Resolution Qhd1440 = new(2560, 1440);
    public static readonly Resolution Uhd2160 = new(3840, 2160);

    /// <summary>Width-to-height ratio (0 when the height is 0).</summary>
    public double AspectRatio => Height == 0 ? 0d : (double)Width / Height;

    /// <summary>Total pixel count.</summary>
    public long PixelCount => (long)Width * Height;

    public override string ToString() => $"{Width}×{Height}";
}
