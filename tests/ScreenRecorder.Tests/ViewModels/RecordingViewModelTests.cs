using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScreenRecorder.App.Services;
using ScreenRecorder.App.ViewModels;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Enums;
using ScreenRecorder.Core.Models;
using ScreenRecorder.Tests.TestDoubles;
using Xunit;

namespace ScreenRecorder.Tests.ViewModels;

public class RecordingViewModelTests
{
    private static RecordingViewModel CreateViewModel(
        Mock<IRecordingOrchestrator> orchestrator,
        out Mock<IDialogService> dialog)
    {
        orchestrator.SetupGet(o => o.State).Returns(RecordingState.Idle);
        orchestrator.SetupGet(o => o.Statistics).Returns(RecordingStatistics.Empty);

        var settings = new Mock<ISettingsService>();
        settings.SetupGet(s => s.Current).Returns(RecordingSettings.CreateDefault());

        dialog = new Mock<IDialogService>();

        return new RecordingViewModel(
            orchestrator.Object,
            settings.Object,
            dialog.Object,
            new ImmediateUiDispatcher(),
            NullLogger<RecordingViewModel>.Instance);
    }

    [Fact]
    public void Initial_state_is_idle_and_start_is_enabled()
    {
        var orchestrator = new Mock<IRecordingOrchestrator>();
        var viewModel = CreateViewModel(orchestrator, out _);

        viewModel.State.Should().Be(RecordingState.Idle);
        viewModel.CanStart.Should().BeTrue();
        viewModel.StartCommand.CanExecute(null).Should().BeTrue();
        viewModel.StopCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Start_surfaces_orchestrator_failure_to_status_and_dialog()
    {
        var orchestrator = new Mock<IRecordingOrchestrator>();
        orchestrator
            .Setup(o => o.StartAsync(It.IsAny<RecordingSettings>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotImplementedException("pipeline pending"));

        var viewModel = CreateViewModel(orchestrator, out var dialog);

        await viewModel.StartCommand.ExecuteAsync(null);

        viewModel.StatusMessage.Should().Be("pipeline pending");
        dialog.Verify(d => d.ShowInfo(It.IsAny<string>(), "pipeline pending"), Times.Once);
    }
}
