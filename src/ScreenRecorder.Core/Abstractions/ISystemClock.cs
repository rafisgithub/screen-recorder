namespace ScreenRecorder.Core.Abstractions;

/// <summary>
/// Abstracts time so timestamp/filename logic stays deterministic under test.
/// Provides both wall-clock time and a monotonic source for A/V synchronization.
/// </summary>
public interface ISystemClock
{
    /// <summary>Current UTC wall-clock time.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Current local wall-clock time (used for output file names).</summary>
    DateTimeOffset LocalNow { get; }

    /// <summary>
    /// A monotonic, high-resolution elapsed timestamp suitable for stamping
    /// captured frames. Never goes backwards; unaffected by clock changes.
    /// </summary>
    TimeSpan GetMonotonicTimestamp();
}
