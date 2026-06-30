using FluentAssertions;
using ScreenRecorder.Core.Models;
using ScreenRecorder.Infrastructure.IO;
using ScreenRecorder.Tests.TestDoubles;
using Xunit;

namespace ScreenRecorder.Tests.IO;

public class OutputPathServiceTests
{
    private static string CreateTempDirectory() =>
        Path.Combine(Path.GetTempPath(), "yt-rec-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void BuildOutputPath_expands_timestamp_token()
    {
        var directory = CreateTempDirectory();
        var clock = new FakeClock { LocalNow = new DateTimeOffset(2026, 6, 30, 14, 5, 9, TimeSpan.Zero) };
        var service = new OutputPathService(clock);
        var settings = new RecordingSettings { OutputDirectory = directory, FileNameTemplate = "Rec_{timestamp}" };

        try
        {
            var path = service.BuildOutputPath(settings);

            Path.GetFileName(path).Should().Be("Rec_2026-06-30_14-05-09.mp4");
            Path.GetDirectoryName(path).Should().Be(directory);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void EnsureUniquePath_appends_index_when_file_exists()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        var original = Path.Combine(directory, "clip.mp4");
        File.WriteAllText(original, "x");
        var service = new OutputPathService(new FakeClock());

        try
        {
            service.EnsureUniquePath(original).Should().Be(Path.Combine(directory, "clip (2).mp4"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
