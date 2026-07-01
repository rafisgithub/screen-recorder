using ScreenRecorder.Core.Abstractions;

namespace ScreenRecorder.Tests.TestDoubles;

/// <summary>An <see cref="ISystemClock"/> whose monotonic time can be advanced by tests.</summary>
public sealed class AdjustableClock : ISystemClock
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UnixEpoch;

    public DateTimeOffset LocalNow { get; set; } = DateTimeOffset.UnixEpoch;

    public TimeSpan Monotonic { get; set; } = TimeSpan.Zero;

    public TimeSpan GetMonotonicTimestamp() => Monotonic;

    public void Advance(TimeSpan by) => Monotonic += by;
}
