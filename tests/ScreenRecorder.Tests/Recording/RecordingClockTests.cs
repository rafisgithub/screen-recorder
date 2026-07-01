using FluentAssertions;
using ScreenRecorder.Infrastructure.Recording;
using ScreenRecorder.Tests.TestDoubles;
using Xunit;

namespace ScreenRecorder.Tests.Recording;

public class RecordingClockTests
{
    [Fact]
    public void Elapsed_is_zero_before_start()
    {
        var clock = new AdjustableClock { Monotonic = TimeSpan.FromSeconds(10) };
        var recording = new RecordingClock(clock);

        recording.Elapsed.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Elapsed_tracks_time_since_start()
    {
        var clock = new AdjustableClock { Monotonic = TimeSpan.FromSeconds(5) };
        var recording = new RecordingClock(clock);

        recording.Start();
        clock.Advance(TimeSpan.FromSeconds(3));

        recording.Elapsed.Should().Be(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void Paused_time_is_excluded_from_elapsed()
    {
        var clock = new AdjustableClock();
        var recording = new RecordingClock(clock);

        recording.Start();
        clock.Advance(TimeSpan.FromSeconds(2));
        recording.Pause();

        clock.Advance(TimeSpan.FromSeconds(10)); // paused span — must not count.
        recording.Elapsed.Should().Be(TimeSpan.FromSeconds(2));

        recording.Resume();
        clock.Advance(TimeSpan.FromSeconds(1));
        recording.Elapsed.Should().Be(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void Multiple_pause_resume_cycles_accumulate_only_active_time()
    {
        var clock = new AdjustableClock();
        var recording = new RecordingClock(clock);

        recording.Start();
        clock.Advance(TimeSpan.FromSeconds(1));

        recording.Pause();
        clock.Advance(TimeSpan.FromSeconds(5));
        recording.Resume();

        clock.Advance(TimeSpan.FromSeconds(2));

        recording.Pause();
        clock.Advance(TimeSpan.FromSeconds(3));
        recording.Resume();

        clock.Advance(TimeSpan.FromSeconds(1));

        recording.Elapsed.Should().Be(TimeSpan.FromSeconds(4)); // 1 + 2 + 1 active.
    }

    [Fact]
    public void SamplesFor_converts_elapsed_to_sample_count()
    {
        var clock = new AdjustableClock();
        var recording = new RecordingClock(clock);

        recording.Start();
        clock.Advance(TimeSpan.FromSeconds(2));

        recording.SamplesFor(48_000).Should().Be(96_000);
    }
}
