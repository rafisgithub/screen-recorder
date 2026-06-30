using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ScreenRecorder.Core.Enums;
using ScreenRecorder.Core.Models;
using ScreenRecorder.Infrastructure.Settings;
using Xunit;

namespace ScreenRecorder.Tests.Settings;

public class JsonSettingsServiceTests
{
    private static string CreateTempFile() =>
        Path.Combine(Path.GetTempPath(), "yt-rec-tests", Guid.NewGuid().ToString("N"), "settings.json");

    [Fact]
    public async Task SaveAsync_then_LoadAsync_round_trips_values()
    {
        var file = CreateTempFile();
        var settings = RecordingSettings.CreateDefault();
        settings.Video.FrameRate = 30;
        settings.Video.Codec = VideoCodec.Hevc;
        settings.Audio.CaptureMicrophone = true;
        settings.Audio.BitrateKbps = 256;

        try
        {
            await new JsonSettingsService(NullLogger<JsonSettingsService>.Instance, file).SaveAsync(settings);
            var reloaded = await new JsonSettingsService(NullLogger<JsonSettingsService>.Instance, file).LoadAsync();

            reloaded.Video.FrameRate.Should().Be(30);
            reloaded.Video.Codec.Should().Be(VideoCodec.Hevc);
            reloaded.Audio.CaptureMicrophone.Should().BeTrue();
            reloaded.Audio.BitrateKbps.Should().Be(256);
        }
        finally
        {
            var directory = Path.GetDirectoryName(file)!;
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_returns_defaults_when_file_missing()
    {
        var file = CreateTempFile();
        var service = new JsonSettingsService(NullLogger<JsonSettingsService>.Instance, file);

        var loaded = await service.LoadAsync();

        loaded.Video.FrameRate.Should().Be(60);
        loaded.Video.Resolution.Should().Be(Resolution.FullHd1080);
    }
}
