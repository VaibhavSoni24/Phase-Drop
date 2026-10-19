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

    [Fact]
    public async Task ExecuteAsync_WithRealFfmpeg_ExtractsMp3FromVideoWithAudio()
    {
        var (ffmpeg, ffprobe) = FindFfmpegBinaries();
        if (ffmpeg == null || ffprobe == null) return; // Skip if FFmpeg not available

        var engine = new MediaConversionEngine(ffmpeg, ffprobe);
        string inputVideo = Path.Combine(_testDir, "test_with_audio.mp4");
        if (!TryGenerateSyntheticVideo(ffmpeg, inputVideo, withAudio: true)) return;

        var op = new FileOperation("convert:mp3", "MP3", ".mp3", OperationCategory.Audio);
        var job = new ConversionJob([inputVideo], op, _testDir);

        var result = await engine.ExecuteAsync(job, new Progress<JobProgress>(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.OutputFiles);
        string outputMp3 = result.OutputFiles[0];
        Assert.True(File.Exists(outputMp3));
        Assert.True(new FileInfo(outputMp3).Length > 0);
        Assert.EndsWith(".mp3", outputMp3, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithRealFfmpeg_ConvertsVideoWithoutAudioToWebm()
    {
        var (ffmpeg, ffprobe) = FindFfmpegBinaries();
        if (ffmpeg == null || ffprobe == null) return;

        var engine = new MediaConversionEngine(ffmpeg, ffprobe);
        string inputVideo = Path.Combine(_testDir, "test_no_audio.mp4");
        if (!TryGenerateSyntheticVideo(ffmpeg, inputVideo, withAudio: false)) return;

        var op = new FileOperation("convert:webm", "WebM", ".webm", OperationCategory.Video);
        var job = new ConversionJob([inputVideo], op, _testDir);

        var result = await engine.ExecuteAsync(job, new Progress<JobProgress>(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.OutputFiles);
        string outputWebm = result.OutputFiles[0];
        Assert.True(File.Exists(outputWebm));
        Assert.True(new FileInfo(outputWebm).Length > 0);
        Assert.EndsWith(".webm", outputWebm, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithRealFfmpeg_ConvertsRasterImageToWebp()
    {
        var (ffmpeg, ffprobe) = FindFfmpegBinaries();
        if (ffmpeg == null || ffprobe == null) return;

        var engine = new MediaConversionEngine(ffmpeg, ffprobe);
        string inputPng = Path.Combine(_testDir, "sample.png");
        CreateSamplePng(inputPng);

        var op = new FileOperation("convert:webp", "WebP", ".webp", OperationCategory.Image);
        var job = new ConversionJob([inputPng], op, _testDir);

        var result = await engine.ExecuteAsync(job, new Progress<JobProgress>(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.OutputFiles);
        string outputWebp = result.OutputFiles[0];
        Assert.True(File.Exists(outputWebp));

        byte[] header = File.ReadAllBytes(outputWebp);
        Assert.True(header.Length >= 12);
        // RIFF header
        Assert.Equal((byte)'R', header[0]);
        Assert.Equal((byte)'I', header[1]);
        Assert.Equal((byte)'F', header[2]);
        Assert.Equal((byte)'F', header[3]);
        // WEBP fourcc
        Assert.Equal((byte)'W', header[8]);
        Assert.Equal((byte)'E', header[9]);
        Assert.Equal((byte)'B', header[10]);
        Assert.Equal((byte)'P', header[11]);
    }

    [Fact]
    public async Task ExecuteAsync_CorruptInputFile_ThrowsInvalidOperationException()
    {
        var (ffmpeg, ffprobe) = FindFfmpegBinaries();
        if (ffmpeg == null || ffprobe == null) return;

        var engine = new MediaConversionEngine(ffmpeg, ffprobe);
        string corruptFile = Path.Combine(_testDir, "corrupt.mp4");
        File.WriteAllText(corruptFile, "Not a valid MP4 header or container content");

        var op = new FileOperation("convert:mp3", "MP3", ".mp3", OperationCategory.Audio);
        var job = new ConversionJob([corruptFile], op, _testDir);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.ExecuteAsync(job, new Progress<JobProgress>(), CancellationToken.None));
    }

    private static (string? ffmpeg, string? ffprobe) FindFfmpegBinaries()
    {
        string staged = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/publish/engines/media"));
        if (File.Exists(Path.Combine(staged, "ffmpeg.exe")) && File.Exists(Path.Combine(staged, "ffprobe.exe")))
            return (Path.Combine(staged, "ffmpeg.exe"), Path.Combine(staged, "ffprobe.exe"));

        string localStaged = Path.GetFullPath("artifacts/publish/engines/media");
        if (File.Exists(Path.Combine(localStaged, "ffmpeg.exe")) && File.Exists(Path.Combine(localStaged, "ffprobe.exe")))
            return (Path.Combine(localStaged, "ffmpeg.exe"), Path.Combine(localStaged, "ffprobe.exe"));

        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv != null)
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                string ffmpeg = Path.Combine(dir.Trim(), "ffmpeg.exe");
                string ffprobe = Path.Combine(dir.Trim(), "ffprobe.exe");
                if (File.Exists(ffmpeg) && File.Exists(ffprobe))
                    return (ffmpeg, ffprobe);
            }
        }

        return (null, null);
    }

    private static bool TryGenerateSyntheticVideo(string ffmpegPath, string outputPath, bool withAudio)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = withAudio
                    ? $"-y -f lavfi -i testsrc=duration=1:size=320x240:rate=10 -f lavfi -i sine=frequency=1000:duration=1 -c:v libx264 -c:a aac -pix_fmt yuv420p \"{outputPath}\""
                    : $"-y -f lavfi -i testsrc=duration=1:size=320x240:rate=10 -c:v libx264 -pix_fmt yuv420p \"{outputPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = System.Diagnostics.Process.Start(psi);
            p?.WaitForExit(10000);
            return p?.ExitCode == 0 && File.Exists(outputPath);
        }
        catch
        {
            return false;
        }
    }

    private static void CreateSamplePng(string path)
    {
        int width = 16, height = 16;
        var pixels = new byte[width * height * 4];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 255;
            pixels[i + 1] = 0;
            pixels[i + 2] = 0;
            pixels[i + 3] = 255;
        }
        var wb = System.Windows.Media.Imaging.BitmapSource.Create(
            width, height, 96, 96,
            System.Windows.Media.PixelFormats.Bgra32, null, pixels, width * 4);

        using var fs = File.Create(path);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(wb));
        encoder.Save(fs);
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
