namespace ScreenRecorder.Core.Models;

/// <summary>
/// A rectangle in pixels. Defined locally so Core stays free of System.Drawing
/// and any platform dependency.
/// </summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public Resolution Size => new(Width, Height);

    public override string ToString() => $"({X},{Y} {Width}×{Height})";
}
