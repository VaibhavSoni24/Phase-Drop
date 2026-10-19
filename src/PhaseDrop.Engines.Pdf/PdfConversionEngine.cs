using System.IO;
using System.Text.Json.Nodes;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PhaseDrop.Core.IO;
using PhaseDrop.Core.Models;
using PhaseDrop.Core.Queue;

namespace PhaseDrop.Engines.Pdf;

public sealed class PdfConversionEngine : IConversionEngine
{
    private static readonly HashSet<string> SupportedOperationIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "convert:pdf", "tool:img_to_pdf", "tool:merge_pdf", "tool:split_pdf",
        "tool:extract_pages", "tool:rotate_pages", "tool:strip_metadata"
    };

    public bool CanHandle(FileOperation operation)
    {
        return SupportedOperationIds.Contains(operation.Id);
    }

    public async Task<JobResult> ExecuteAsync(ConversionJob job, IProgress<JobProgress> progress, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            return job.Operation.Id switch
            {
                "tool:merge_pdf" => MergePdfs(job, progress, cancellationToken),
                "tool:split_pdf" => SplitPdf(job, progress, cancellationToken),
                "tool:extract_pages" => ExtractPages(job, progress, cancellationToken),
                "tool:rotate_pages" => RotatePages(job, progress, cancellationToken),
                "convert:pdf" or "tool:img_to_pdf" => ImagesToPdf(job, progress, cancellationToken),
                "tool:strip_metadata" => StripMetadata(job, progress, cancellationToken),
                _ => throw new NotSupportedException($"Operation {job.Operation.Id} not supported by PDF engine.")
            };
        }, cancellationToken);
    }

    private static JobResult MergePdfs(ConversionJob job, IProgress<JobProgress> progress, CancellationToken cancellationToken)
    {
        string targetDir = job.OutputDirectory ?? Path.GetDirectoryName(job.SourcePaths[0]) ?? Environment.CurrentDirectory;
        string outputPath = CollisionSafeWriter.GetSafeOutputPath(targetDir, "Merged_Document", ".pdf");
        string stagingPath = CollisionSafeWriter.CreateStagingPath(".pdf");

        try
        {
            using var outputDoc = new PdfDocument();
            int total = job.SourcePaths.Count;

            for (int i = 0; i < total; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = job.SourcePaths[i];
                progress.Report(new JobProgress($"Merging {Path.GetFileName(path)}", (double)i / total, i, total, Path.GetFileName(path)));

                using var inputDoc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
                for (int pageIdx = 0; pageIdx < inputDoc.PageCount; pageIdx++)
                {
                    outputDoc.AddPage(inputDoc.Pages[pageIdx]);
                }
            }

            outputDoc.Save(stagingPath);
            CollisionSafeWriter.CommitStagingFile(stagingPath, outputPath);
            return new JobResult(true, [outputPath]);
        }
        catch
        {
            CollisionSafeWriter.CleanStagingFile(stagingPath);
            throw;
        }
    }

    private static JobResult SplitPdf(ConversionJob job, IProgress<JobProgress> progress, CancellationToken cancellationToken)
    {
        string sourcePath = job.SourcePaths[0];
        string baseName = Path.GetFileNameWithoutExtension(sourcePath);
        string targetDir = job.OutputDirectory ?? Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory;

        using var inputDoc = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import);
        int pageCount = inputDoc.PageCount;
        var outputFiles = new List<string>();

        for (int i = 0; i < pageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(new JobProgress($"Splitting page {i + 1} of {pageCount}", (double)i / pageCount, i, pageCount));

            string outputPath = CollisionSafeWriter.GetSafeOutputPath(targetDir, $"{baseName}_page_{i + 1}", ".pdf");
            string stagingPath = CollisionSafeWriter.CreateStagingPath(".pdf");

            try
            {
                using var singlePageDoc = new PdfDocument();
                singlePageDoc.AddPage(inputDoc.Pages[i]);
                singlePageDoc.Save(stagingPath);
                CollisionSafeWriter.CommitStagingFile(stagingPath, outputPath);
                outputFiles.Add(outputPath);
            }
            catch
            {
                CollisionSafeWriter.CleanStagingFile(stagingPath);
                throw;
            }
        }

        return new JobResult(true, outputFiles);
    }

    private static JobResult ExtractPages(ConversionJob job, IProgress<JobProgress> progress, CancellationToken cancellationToken)
    {
        string sourcePath = job.SourcePaths[0];
        string baseName = Path.GetFileNameWithoutExtension(sourcePath);
        string targetDir = job.OutputDirectory ?? Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory;
        string outputPath = CollisionSafeWriter.GetSafeOutputPath(targetDir, $"{baseName}_extracted", ".pdf");
        string stagingPath = CollisionSafeWriter.CreateStagingPath(".pdf");

        // Parse page range e.g. "1-3,5" or default to page 1
        string range = job.Parameters?["pages"]?.GetValue<string>() ?? "1";
        var pageNumbers = ParsePageRanges(range);

        try
        {
            using var inputDoc = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import);
            using var outputDoc = new PdfDocument();

            foreach (int pageNum in pageNumbers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (pageNum >= 1 && pageNum <= inputDoc.PageCount)
                {
                    outputDoc.AddPage(inputDoc.Pages[pageNum - 1]);
                }
            }

            outputDoc.Save(stagingPath);
            CollisionSafeWriter.CommitStagingFile(stagingPath, outputPath);
            return new JobResult(true, [outputPath]);
        }
        catch
        {
            CollisionSafeWriter.CleanStagingFile(stagingPath);
            throw;
        }
    }

    private static JobResult RotatePages(ConversionJob job, IProgress<JobProgress> progress, CancellationToken cancellationToken)
    {
        var outputFiles = new List<string>();
        int degrees = job.Parameters?["degrees"]?.GetValue<int>() ?? 90;

        foreach (string sourcePath in job.SourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string targetDir = job.OutputDirectory ?? Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory;
            string outputPath = CollisionSafeWriter.GetSafeOutputPath(targetDir, $"{Path.GetFileNameWithoutExtension(sourcePath)}_rotated", ".pdf");
            string stagingPath = CollisionSafeWriter.CreateStagingPath(".pdf");

            try
            {
                using var inputDoc = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Modify);
                foreach (var page in inputDoc.Pages)
                {
                    page.Orientation = degrees switch
                    {
                        90 => PdfSharp.PageOrientation.Landscape,
                        180 => PdfSharp.PageOrientation.Portrait,
                        270 => PdfSharp.PageOrientation.Landscape,
                        _ => page.Orientation
                    };
                    page.Rotate = (page.Rotate + degrees) % 360;
                }

                inputDoc.Save(stagingPath);
                CollisionSafeWriter.CommitStagingFile(stagingPath, outputPath);
                outputFiles.Add(outputPath);
            }
            catch
            {
                CollisionSafeWriter.CleanStagingFile(stagingPath);
                throw;
            }
        }

        return new JobResult(true, outputFiles);
    }

    private static JobResult ImagesToPdf(ConversionJob job, IProgress<JobProgress> progress, CancellationToken cancellationToken)
    {
        string targetDir = job.OutputDirectory ?? Path.GetDirectoryName(job.SourcePaths[0]) ?? Environment.CurrentDirectory;
        string baseName = job.SourcePaths.Count == 1 ? Path.GetFileNameWithoutExtension(job.SourcePaths[0]) : "Images_Compiled";
        string outputPath = CollisionSafeWriter.GetSafeOutputPath(targetDir, baseName, ".pdf");
        string stagingPath = CollisionSafeWriter.CreateStagingPath(".pdf");

        try
        {
            using var doc = new PdfDocument();
            int total = job.SourcePaths.Count;

            for (int i = 0; i < total; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string imgPath = job.SourcePaths[i];
                progress.Report(new JobProgress($"Adding {Path.GetFileName(imgPath)} to PDF", (double)i / total, i, total));

                using var xImage = XImage.FromFile(imgPath);
                var page = doc.AddPage();
                double w = xImage.PixelWidth * 72.0 / (xImage.HorizontalResolution > 0 ? xImage.HorizontalResolution : 96.0);
                double h = xImage.PixelHeight * 72.0 / (xImage.VerticalResolution > 0 ? xImage.VerticalResolution : 96.0);
                page.Width = XUnit.FromPoint(w);
                page.Height = XUnit.FromPoint(h);

                using var gfx = XGraphics.FromPdfPage(page);
                gfx.DrawImage(xImage, 0, 0, page.Width.Point, page.Height.Point);
            }

            doc.Save(stagingPath);
            CollisionSafeWriter.CommitStagingFile(stagingPath, outputPath);
            return new JobResult(true, [outputPath]);
        }
        catch
        {
            CollisionSafeWriter.CleanStagingFile(stagingPath);
            throw;
        }
    }

    private static JobResult StripMetadata(ConversionJob job, IProgress<JobProgress> progress, CancellationToken cancellationToken)
    {
        var outputFiles = new List<string>();

        foreach (string sourcePath in job.SourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string targetDir = job.OutputDirectory ?? Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory;
            string outputPath = CollisionSafeWriter.GetSafeOutputPath(targetDir, $"{Path.GetFileNameWithoutExtension(sourcePath)}_clean", ".pdf");
            string stagingPath = CollisionSafeWriter.CreateStagingPath(".pdf");

            try
            {
                using var inputDoc = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Modify);
                inputDoc.Info.Author = "";
                inputDoc.Info.Title = "";
                inputDoc.Info.Subject = "";
                inputDoc.Info.Keywords = "";
                inputDoc.Info.Creator = "Phase Drop Clean Engine";

                inputDoc.Save(stagingPath);
                CollisionSafeWriter.CommitStagingFile(stagingPath, outputPath);
                outputFiles.Add(outputPath);
            }
            catch
            {
                CollisionSafeWriter.CleanStagingFile(stagingPath);
                throw;
            }
        }

        return new JobResult(true, outputFiles);
    }

    private static List<int> ParsePageRanges(string range)
    {
        var result = new List<int>();
        var parts = range.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (part.Contains('-'))
            {
                var span = part.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (span.Length == 2 && int.TryParse(span[0], out int start) && int.TryParse(span[1], out int end))
                {
                    for (int p = Math.Min(start, end); p <= Math.Max(start, end); p++)
                        result.Add(p);
                }
            }
            else if (int.TryParse(part, out int single))
            {
                result.Add(single);
            }
        }

        return result.Distinct().OrderBy(x => x).ToList();
    }
}
