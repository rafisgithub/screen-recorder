using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ScreenRecorder.Core.Enums;

namespace ScreenRecorder.App.Converters;

/// <summary>Maps a <see cref="RecordingState"/> to a status-indicator brush.</summary>
public sealed class RecordingStateToBrushConverter : IValueConverter
{
    private static readonly Brush Idle = Frozen(0x9E, 0x9E, 0x9E);
    private static readonly Brush Active = Frozen(0xE5, 0x39, 0x35);
    private static readonly Brush Paused = Frozen(0xFF, 0xB3, 0x00);
    private static readonly Brush Transitioning = Frozen(0x42, 0xA5, 0xF5);
    private static readonly Brush Error = Frozen(0xEF, 0x53, 0x50);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        RecordingState.Recording => Active,
        RecordingState.Paused => Paused,
        RecordingState.Starting or RecordingState.Stopping => Transitioning,
        RecordingState.Error => Error,
        _ => Idle,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
