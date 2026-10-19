using System.IO;
using PhaseDrop.Core.Models;
using PhaseDrop.Engines.Media;

namespace PhaseDrop.Engines.Tests;

public class MediaConversionEngineTests : IDisposable
{
    private readonly string _testDir;

    public MediaConversionEngineTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"PhaseDrop_MediaTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    [Fact]
    public void CanHandle_RecognizesMediaOperations()
    {
        var engine = new MediaConversionEngine("dummy_ffmpeg.exe", "dummy_ffprobe.exe");

        Assert.True(engine.CanHandle(new FileOperation("convert:mp4", "MP4", ".mp4", OperationCategory.Video)));
        Assert.True(engine.CanHandle(new FileOperation("convert:mp3", "MP3", ".mp3", OperationCategory.Audio)));
        Assert.True(engine.CanHandle(new FileOperation("convert:webp", "WebP", ".webp", OperationCategory.Image)));
        Assert.True(engine.CanHandle(new FileOperation("tool:trim", "Trim", ".mp4", OperationCategory.Video, true)));
        Assert.False(engine.CanHandle(new FileOperation("convert:zip", "ZIP", ".zip", OperationCategory.Archive)));
    }

    [Fact]
    public async Task ExecuteAsync_MissingBinary_ThrowsFileNotFoundException()
    {
        string dummyFfmpeg = Path.Combine(_testDir, "non_existent_ffmpeg.exe");
        string dummyFfprobe = Path.Combine(_testDir, "non_existent_ffprobe.exe");
        var engine = new MediaConversionEngine(dummyFfmpeg, dummyFfprobe);

        string sampleFile = Path.Combine(_testDir, "sample.mp4");
        File.WriteAllText(sampleFile, "dummy media content");

        var op = new FileOperation("convert:mp3", "MP3", ".mp3", OperationCategory.Audio);
        var job = new ConversionJob([sampleFile], op, _testDir);

        var ex = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            engine.ExecuteAsync(job, new Progress<JobProgress>(), CancellationToken.None));

        Assert.Contains("FFmpeg media processing engine binary not found", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        string dummyFfmpeg = Path.Combine(_testDir, "non_existent_ffmpeg.exe");
        var engine = new MediaConversionEngine(dummyFfmpeg);

        string sampleFile = Path.Combine(_testDir, "sample.mp4");
        File.WriteAllText(sampleFile, "dummy media content");

        var op = new FileOperation("convert:mp3", "MP3", ".mp3", OperationCategory.Audio);
        var job = new ConversionJob([sampleFile], op, _testDir);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            engine.ExecuteAsync(job, new Progress<JobProgress>(), cts.Token));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }
        catch { }
    }
}
