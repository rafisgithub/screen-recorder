namespace ScreenRecorder.Core.Enums;

/// <summary>What the video pipeline is capturing.</summary>
public enum CaptureTargetKind
{
    /// <summary>An entire monitor / display.</summary>
    Monitor = 0,

    /// <summary>A single application window.</summary>
    Window,

    /// <summary>A user-defined rectangular region of a monitor.</summary>
    Region,
}
