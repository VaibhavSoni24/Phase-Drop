using System.IO;
using System.Text.Json.Nodes;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfSharp.Pdf.IO;
using PhaseDrop.Core.Models;
using PhaseDrop.Engines.Pdf;

namespace PhaseDrop.Engines.Tests;

public class PdfConversionEngineTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testPng;
    private readonly PdfConversionEngine _engine = new();

    public PdfConversionEngineTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"PhaseDrop_PdfTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _testPng = Path.Combine(_testDir, "page.png");
        CreateSamplePng(_testPng, 200, 200);
    }

    [Fact]
    public async Task ImageToPdf_And_SplitPdf_WorksSuccessfully()
    {
        var op = new FileOperation("convert:pdf", "PDF", ".pdf", OperationCategory.Image);
        var job = new ConversionJob([_testPng], op, _testDir);

        var result = await _engine.ExecuteAsync(job, new Progress<JobProgress>(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.OutputFiles);
        string pdfPath = result.OutputFiles[0];
        Assert.True(File.Exists(pdfPath));

        // Verify PDF page count with independent reader
        using (var reader = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import))
        {
            Assert.Equal(1, reader.PageCount);
        }

        // Test Split PDF
        var splitOp = new FileOperation("tool:split_pdf", "Split", ".pdf", OperationCategory.Pdf, true);
        var splitJob = new ConversionJob([pdfPath], splitOp, _testDir);

        var splitResult = await _engine.ExecuteAsync(splitJob, new Progress<JobProgress>(), CancellationToken.None);
        Assert.True(splitResult.Success);
        Assert.Single(splitResult.OutputFiles);
    }

    [Fact]
    public async Task MergePdfs_CombinesMultiplePdfs()
    {
        // Create 2 PDFs first
        var op = new FileOperation("convert:pdf", "PDF", ".pdf", OperationCategory.Image);
        var res1 = await _engine.ExecuteAsync(new ConversionJob([_testPng], op, _testDir), new Progress<JobProgress>(), CancellationToken.None);
        var res2 = await _engine.ExecuteAsync(new ConversionJob([_testPng], op, _testDir), new Progress<JobProgress>(), CancellationToken.None);

        string pdf1 = res1.OutputFiles[0];
        string pdf2 = res2.OutputFiles[0];

        var mergeOp = new FileOperation("tool:merge_pdf", "Merge", ".pdf", OperationCategory.Pdf, true);
        var mergeJob = new ConversionJob([pdf1, pdf2], mergeOp, _testDir);

        var mergeResult = await _engine.ExecuteAsync(mergeJob, new Progress<JobProgress>(), CancellationToken.None);
        Assert.True(mergeResult.Success);
        Assert.Single(mergeResult.OutputFiles);

        using var mergedReader = PdfReader.Open(mergeResult.OutputFiles[0], PdfDocumentOpenMode.Import);
        Assert.Equal(2, mergedReader.PageCount);
    }

    private static void CreateSamplePng(string path, int width, int height)
    {
        int rawStride = (width * 32 + 7) / 8;
        byte[] rawPixels = new byte[rawStride * height];
        for (int i = 0; i < rawPixels.Length; i += 4)
        {
            rawPixels[i] = 128;
            rawPixels[i + 1] = 128;
            rawPixels[i + 2] = 128;
            rawPixels[i + 3] = 255;
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
