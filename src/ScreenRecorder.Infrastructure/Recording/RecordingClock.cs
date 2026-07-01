using ScreenRecorder.Core.Abstractions;

namespace ScreenRecorder.Infrastructure.Recording;

/// <summary>
/// The single monotonic timeline shared by the video and audio paths so they stay
/// in sync. Video frames are stamped with <see cref="Elapsed"/> at capture; the
/// audio pump derives a sample-accurate position from <see cref="SamplesFor"/>.
/// Pause/resume freeze the timeline so the output has no gap.
/// </summary>
internal sealed class RecordingClock
{
    private readonly ISystemClock _clock;
    private readonly object _sync = new();

    private TimeSpan _start;
    private TimeSpan _pausedTotal;
    private TimeSpan _pauseStarted;
    private bool _running;
    private bool _paused;

    public RecordingClock(ISystemClock clock) => _clock = clock;

    /// <summary>Begins (or restarts) the timeline at zero.</summary>
    public void Start()
    {
        lock (_sync)
        {
            _start = _clock.GetMonotonicTimestamp();
            _pausedTotal = TimeSpan.Zero;
            _pauseStarted = TimeSpan.Zero;
            _running = true;
            _paused = false;
        }
    }

    public void Pause()
    {
        lock (_sync)
        {
            if (_running && !_paused)
            {
                _pauseStarted = _clock.GetMonotonicTimestamp();
                _paused = true;
            }
        }
    }

    public void Resume()
    {
        lock (_sync)
        {
            if (_running && _paused)
            {
                _pausedTotal += _clock.GetMonotonicTimestamp() - _pauseStarted;
                _paused = false;
            }
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            _running = false;
            _paused = false;
        }
    }

    public bool IsPaused
    {
        get
        {
            lock (_sync)
            {
                return _paused;
            }
        }
    }

    /// <summary>Time elapsed since <see cref="Start"/>, excluding paused spans. Never negative.</summary>
    public TimeSpan Elapsed
    {
        get
        {
            lock (_sync)
            {
                if (!_running)
                {
                    return TimeSpan.Zero;
                }

                var nowOrPause = _paused ? _pauseStarted : _clock.GetMonotonicTimestamp();
                var elapsed = nowOrPause - _start - _pausedTotal;
                return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
            }
        }
    }

    /// <summary>Number of audio sample-frames that should have been emitted by now at <paramref name="sampleRate"/>.</summary>
    public long SamplesFor(int sampleRate) => (long)(Elapsed.TotalSeconds * sampleRate);
}
