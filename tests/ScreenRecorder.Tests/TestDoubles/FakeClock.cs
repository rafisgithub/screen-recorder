using ScreenRecorder.Core.Abstractions;

namespace ScreenRecorder.Tests.TestDoubles;

/// <summary>Deterministic <see cref="ISystemClock"/> for tests.</summary>
public sealed class FakeClock : ISystemClock
{
    public DateTimeOffset UtcNow { get; init; } = DateTimeOffset.UnixEpoch;
    public DateTimeOffset LocalNow { get; init; } = DateTimeOffset.UnixEpoch;
    public TimeSpan GetMonotonicTimestamp() => TimeSpan.Zero;
}
