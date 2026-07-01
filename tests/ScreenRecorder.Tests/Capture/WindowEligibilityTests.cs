using FluentAssertions;
using ScreenRecorder.Infrastructure.Display;
using Xunit;

namespace ScreenRecorder.Tests.Capture;

public class WindowEligibilityTests
{
    private const int ToolWindow = 0x00000080;

    [Fact]
    public void Visible_titled_normal_window_is_eligible()
    {
        WindowEligibility.IsEligible(isVisible: true, titleLength: 5, exStyle: 0, isCloaked: false, width: 800, height: 600)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(false, 5, 0, false, 800, 600)] // hidden
    [InlineData(true, 0, 0, false, 800, 600)]  // untitled
    [InlineData(true, 5, ToolWindow, false, 800, 600)] // tool window
    [InlineData(true, 5, 0, true, 800, 600)]   // cloaked
    [InlineData(true, 5, 0, false, 0, 600)]    // zero width
    [InlineData(true, 5, 0, false, 800, 0)]    // zero height
    public void Disqualifying_conditions_make_a_window_ineligible(
        bool visible, int titleLength, long exStyle, bool cloaked, int width, int height)
    {
        WindowEligibility.IsEligible(visible, titleLength, exStyle, cloaked, width, height)
            .Should().BeFalse();
    }

    [Fact]
    public void Tool_window_flag_combined_with_other_styles_is_still_excluded()
    {
        // WS_EX_TOOLWINDOW set alongside unrelated extended-style bits.
        const long exStyle = ToolWindow | 0x00040000 /* WS_EX_APPWINDOW */;
        WindowEligibility.IsEligible(true, 10, exStyle, false, 1920, 1080)
            .Should().BeFalse();
    }
}
