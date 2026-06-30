using System.Diagnostics;
using ScreenRecorder.Core.Abstractions;

namespace ScreenRecorder.Infrastructure.Time;

/// <summary>
/// Default <see cref="ISystemClock"/> backed by the system wall clock and a
/// process-wide monotonic <see cref="Stopwatch"/> origin.
/// </summary>
public sealed class SystemClock : ISystemClock
{
    private static readonly long s_origin = Stopwatch.GetTimestamp();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateTimeOffset LocalNow => DateTimeOffset.Now;

    public TimeSpan GetMonotonicTimestamp() => Stopwatch.GetElapsedTime(s_origin);
}
