using PhaseDrop.Core.Models;
using PhaseDrop.Core.Queue;
using PhaseDrop.Core.Registry;
using PhaseDrop.Engines.Archives;
using PhaseDrop.Engines.Documents;
using PhaseDrop.Engines.Images;
using PhaseDrop.Engines.Media;
using PhaseDrop.Engines.Pdf;

namespace PhaseDrop.IntegrationTests;

public class EngineRoutingTests
{
    private readonly IReadOnlyList<IConversionEngine> _engines =
    [
        new ImageConversionEngine(),
        new MediaConversionEngine("dummy_ffmpeg.exe", "dummy_ffprobe.exe"),
        new PdfConversionEngine(),
        new ArchiveConversionEngine(),
        new DocumentConversionEngine()
    ];

    [Fact]
    public void CapabilityRegistry_EveryAdvertisedOperation_RoutesToAtLeastOneRegisteredEngine()
    {
        string[] sampleFiles =
        [
            "test.png", "test.jpg", "test.mp4", "test.mp3", "test.pdf",
            "test.zip", "test.csv", "test.json", "test.md"
        ];

        var allOperations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in sampleFiles)
        {
            var conversions = CapabilityRegistry.GetConversions([file]);
            foreach (var conv in conversions)
            {
                allOperations.Add(conv.Id);
            }

            var tools = CapabilityRegistry.GetTools([file]);
            foreach (var tool in tools)
            {
                allOperations.Add(tool.Id);
            }
        }

        Assert.NotEmpty(allOperations);

        foreach (var opId in allOperations)
        {
            var dummyOp = new FileOperation(opId, opId, ".out", OperationCategory.Unknown);
            bool handled = _engines.Any(e => e.CanHandle(dummyOp));
            Assert.True(handled, $"No registered engine can handle advertised operation: {opId}");
        }
    }

    [Fact]
    public void ImageToWebP_RoutesToMediaEngine_NotImageEngine()
    {
        var webpOp = new FileOperation("convert:webp", "WebP", ".webp", OperationCategory.Image);
        var imgEngine = _engines.OfType<ImageConversionEngine>().Single();
        var mediaEngine = _engines.OfType<MediaConversionEngine>().Single();

        // Image engine must not claim WebP (since it lacks native WebP WIC encoder)
        Assert.False(imgEngine.CanHandle(webpOp));

        // Media engine must handle WebP via FFmpeg libwebp bridge
        Assert.True(mediaEngine.CanHandle(webpOp));
    }

    [Fact]
    public void ImageToPdf_RoutesToPdfEngine_NotImageEngine()
    {
        var pdfOp = new FileOperation("convert:pdf", "PDF", ".pdf", OperationCategory.Image);
        var imgEngine = _engines.OfType<ImageConversionEngine>().Single();
        var pdfEngine = _engines.OfType<PdfConversionEngine>().Single();

        Assert.False(imgEngine.CanHandle(pdfOp));
        Assert.True(pdfEngine.CanHandle(pdfOp));
    }

    [Fact]
    public void StandardRasterImages_RouteToImageEngine()
    {
        var imgEngine = _engines.OfType<ImageConversionEngine>().Single();
        string[] rasterOpIds = ["convert:png", "convert:jpg", "convert:bmp", "convert:gif", "convert:tiff", "convert:ico"];

        foreach (var id in rasterOpIds)
        {
            var op = new FileOperation(id, id, ".out", OperationCategory.Image);
            Assert.True(imgEngine.CanHandle(op), $"Image engine should handle {id}");
        }
    }

    [Fact]
    public void Documents_RouteToDocumentEngine()
    {
        var docEngine = _engines.OfType<DocumentConversionEngine>().Single();
        string[] docOpIds = ["convert:csv", "convert:json", "convert:html", "convert:txt", "tool:table_to_json", "tool:md_to_html"];

        foreach (var id in docOpIds)
        {
            var op = new FileOperation(id, id, ".out", OperationCategory.Document);
            Assert.True(docEngine.CanHandle(op), $"Document engine should handle {id}");
        }
    }

    [Fact]
    public void Archives_RouteToArchiveEngine()
    {
        var archiveEngine = _engines.OfType<ArchiveConversionEngine>().Single();
        string[] archOpIds = ["convert:zip", "convert:7z", "convert:tar", "convert:tgz", "tool:pack_zip", "tool:pack_7z", "tool:extract", "tool:test_archive"];

        foreach (var id in archOpIds)
        {
            var op = new FileOperation(id, id, ".out", OperationCategory.Archive);
            Assert.True(archiveEngine.CanHandle(op), $"Archive engine should handle {id}");
        }
    }
}
