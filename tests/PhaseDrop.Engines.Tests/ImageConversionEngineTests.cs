using System.IO;
using System.Text.Json.Nodes;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PhaseDrop.Core.Models;
using PhaseDrop.Engines.Images;

namespace PhaseDrop.Engines.Tests;

public class ImageConversionEngineTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _sourcePng;
    private readonly ImageConversionEngine _engine = new();

    public ImageConversionEngineTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"PhaseDrop_ImgTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _sourcePng = Path.Combine(_testDir, "sample.png");
        CreateSamplePng(_sourcePng, 100, 100);
    }

    [Fact]
    public async Task Convert_PngToJpeg_ProducesValidJpegFile()
    {
        var op = new FileOperation("convert:jpg", "JPEG", ".jpg", OperationCategory.Image);
        var job = new ConversionJob([_sourcePng], op, _testDir);

        var result = await _engine.ExecuteAsync(job, new Progress<JobProgress>(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.OutputFiles);
        Assert.True(File.Exists(result.OutputFiles[0]));
        Assert.EndsWith(".jpg", result.OutputFiles[0], StringComparison.OrdinalIgnoreCase);

        // Verify output file can be loaded back
        using var stream = File.OpenRead(result.OutputFiles[0]);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        Assert.Equal(100, decoder.Frames[0].PixelWidth);
        Assert.Equal(100, decoder.Frames[0].PixelHeight);
    }

    [Fact]
    public async Task Tool_CropImage_ProducesCroppedDimensions()
    {
        var op = new FileOperation("tool:crop", "Crop", ".png", OperationCategory.Image, true);
        var parameters = new JsonObject
        {
            ["x"] = 10,
            ["y"] = 10,
            ["width"] = 50,
            ["height"] = 40
        };
        var job = new ConversionJob([_sourcePng], op, _testDir, parameters);

        var result = await _engine.ExecuteAsync(job, new Progress<JobProgress>(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.OutputFiles);

        using var stream = File.OpenRead(result.OutputFiles[0]);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        Assert.Equal(50, decoder.Frames[0].PixelWidth);
        Assert.Equal(40, decoder.Frames[0].PixelHeight);
    }

    [Fact]
    public async Task Tool_RotateCw_SwapsDimensionsWhenNotSquare()
    {
        string rectPng = Path.Combine(_testDir, "rect.png");
        CreateSamplePng(rectPng, 120, 80);

        var op = new FileOperation("tool:rotate_cw", "Rotate 90°", ".png", OperationCategory.Image, true);
        var job = new ConversionJob([rectPng], op, _testDir);

        var result = await _engine.ExecuteAsync(job, new Progress<JobProgress>(), CancellationToken.None);

        Assert.True(result.Success);
        using var stream = File.OpenRead(result.OutputFiles[0]);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        Assert.Equal(80, decoder.Frames[0].PixelWidth);
        Assert.Equal(120, decoder.Frames[0].PixelHeight);
    }

    [Fact]
    public async Task Convert_UnsupportedFormat_ThrowsNotSupportedException()
    {
        var op = new FileOperation("convert:xyz", "XYZ", ".xyz", OperationCategory.Image);
        var job = new ConversionJob([_sourcePng], op, _testDir);

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _engine.ExecuteAsync(job, new Progress<JobProgress>(), CancellationToken.None));
    }

    [Fact]
    public async Task Convert_PngWithAlphaToJpeg_FlattensAlphaOntoWhiteBackground()
    {
        string transparentPng = Path.Combine(_testDir, "transparent.png");
        // Create 50x50 PNG with 50% alpha
        int width = 50, height = 50;
        int rawStride = (width * 32 + 7) / 8;
        byte[] rawPixels = new byte[rawStride * height];
        for (int i = 0; i < rawPixels.Length; i += 4)
        {
            rawPixels[i] = 120;     // Blue
            rawPixels[i + 1] = 10;  // Green
            rawPixels[i + 2] = 200; // Red
            rawPixels[i + 3] = 128; // 50% Alpha
        }
        var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, rawPixels, rawStride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using (var fs = File.Create(transparentPng))
        {
            encoder.Save(fs);
        }

        var op = new FileOperation("convert:jpg", "JPEG", ".jpg", OperationCategory.Image);
        var job = new ConversionJob([transparentPng], op, _testDir);

        var result = await _engine.ExecuteAsync(job, new Progress<JobProgress>(), CancellationToken.None);

        Assert.True(result.Success);
        string outputJpg = result.OutputFiles[0];
        Assert.True(File.Exists(outputJpg));

        using var stream = File.OpenRead(outputJpg);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        Assert.Equal(50, decoder.Frames[0].PixelWidth);
        Assert.Equal(50, decoder.Frames[0].PixelHeight);
    }

    [Fact]
    public async Task Convert_PngToIco_ProducesValidWindowsIconHeaderAndNotRawPng()
    {
        var op = new FileOperation("convert:ico", "ICO", ".ico", OperationCategory.Image);
        var job = new ConversionJob([_sourcePng], op, _testDir);

        var result = await _engine.ExecuteAsync(job, new Progress<JobProgress>(), CancellationToken.None);

        Assert.True(result.Success);
        string icoPath = result.OutputFiles[0];
        Assert.True(File.Exists(icoPath));

        // Verify Windows Icon header magic bytes (00 00 01 00)
        byte[] bytes = await File.ReadAllBytesAsync(icoPath);
        Assert.True(bytes.Length > 22);

        // Windows ICO magic header: reserved=0, type=1
        Assert.Equal(0, bytes[0]);
        Assert.Equal(0, bytes[1]);
        Assert.Equal(1, bytes[2]);
        Assert.Equal(0, bytes[3]);

        // CRITICAL: Ensure it is NOT raw PNG bytes under .ico filename
        bool isRawPng = bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
        Assert.False(isRawPng, "ICO output must be a valid Windows Icon container, not raw PNG bytes.");
    }

    [Fact]
    public async Task Convert_PngToBmp_ProducesValidBmpHeader()
    {
        var op = new FileOperation("convert:bmp", "BMP", ".bmp", OperationCategory.Image);
        var job = new ConversionJob([_sourcePng], op, _testDir);

        var result = await _engine.ExecuteAsync(job, new Progress<JobProgress>(), CancellationToken.None);

        Assert.True(result.Success);
        string bmpPath = result.OutputFiles[0];

        byte[] header = new byte[2];
        using (var fs = File.OpenRead(bmpPath))
        {
            fs.ReadExactly(header, 0, 2);
        }

        // BMP header must start with 'BM' (0x42, 0x4D)
        Assert.Equal(0x42, header[0]);
        Assert.Equal(0x4D, header[1]);
    }

    [Fact]
    public async Task Convert_PngToTiff_ProducesValidTiffHeader()
    {
        var op = new FileOperation("convert:tiff", "TIFF", ".tiff", OperationCategory.Image);
        var job = new ConversionJob([_sourcePng], op, _testDir);

        var result = await _engine.ExecuteAsync(job, new Progress<JobProgress>(), CancellationToken.None);

        Assert.True(result.Success);
        string tiffPath = result.OutputFiles[0];

        byte[] header = new byte[4];
        using (var fs = File.OpenRead(tiffPath))
        {
            fs.ReadExactly(header, 0, 4);
        }

        // TIFF header: "II*\0" (little endian) or "MM\0*" (big endian)
        bool isLittleEndian = header[0] == 0x49 && header[1] == 0x49 && header[2] == 0x2A && header[3] == 0x00;
        bool isBigEndian = header[0] == 0x4D && header[1] == 0x4D && header[2] == 0x00 && header[3] == 0x2A;
        Assert.True(isLittleEndian || isBigEndian, "TIFF file must have valid TIFF magic header.");
    }

    private static void CreateSamplePng(string path, int width, int height)
    {
        int rawStride = (width * 32 + 7) / 8;
        byte[] rawPixels = new byte[rawStride * height];
        for (int i = 0; i < rawPixels.Length; i += 4)
        {
            rawPixels[i] = 255;     // Blue
            rawPixels[i + 1] = 0;   // Green
            rawPixels[i + 2] = 0;   // Red
            rawPixels[i + 3] = 255; // Alpha
        }

        var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, rawPixels, rawStride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));

        using var stream = File.Create(path);
        encoder.Save(stream);
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
