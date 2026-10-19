using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PhaseDrop.Core.IO;
using PhaseDrop.Core.Models;
using PhaseDrop.Core.Queue;

namespace PhaseDrop.Engines.Images;

public sealed class ImageConversionEngine : IConversionEngine
{
    private static readonly HashSet<string> SupportedOperationIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "convert:png", "convert:jpg", "convert:jpeg", "convert:bmp", "convert:gif", "convert:tiff", "convert:ico",
        "tool:crop", "tool:resize", "tool:compress", "tool:rotate_cw", "tool:flip_h", "tool:strip_exif"
    };

    public bool CanHandle(FileOperation operation)
    {
        return SupportedOperationIds.Contains(operation.Id);
    }

    public async Task<JobResult> ExecuteAsync(ConversionJob job, IProgress<JobProgress> progress, CancellationToken cancellationToken)
    {
        var outputFiles = new List<string>();
        int total = job.SourcePaths.Count;

        for (int i = 0; i < total; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string sourcePath = job.SourcePaths[i];
            string fileName = Path.GetFileName(sourcePath);
            progress.Report(new JobProgress($"Processing {fileName}", (double)i / total, i, total, fileName));

            string targetExt = GetTargetExtension(job.Operation, sourcePath);
            string targetDir = job.OutputDirectory ?? Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory;
            string outputPath = CollisionSafeWriter.GetSafeOutputPath(targetDir, Path.GetFileNameWithoutExtension(sourcePath), targetExt);
            string stagingPath = CollisionSafeWriter.CreateStagingPath(targetExt);

            try
            {
                await Task.Run(() => ProcessSingleImage(sourcePath, stagingPath, job.Operation, job.Parameters, cancellationToken), cancellationToken);

                CollisionSafeWriter.CommitStagingFile(stagingPath, outputPath);
                outputFiles.Add(outputPath);
            }
            catch
            {
                CollisionSafeWriter.CleanStagingFile(stagingPath);
                throw;
            }

            progress.Report(new JobProgress($"Completed {fileName}", (double)(i + 1) / total, i + 1, total, fileName));
        }

        return new JobResult(true, outputFiles);
    }

    private static string GetTargetExtension(FileOperation operation, string sourcePath)
    {
        if (operation.Id.StartsWith("convert:", StringComparison.OrdinalIgnoreCase))
            return operation.TargetExtension;

        return operation.Id switch
        {
            "tool:crop" or "tool:resize" or "tool:rotate_cw" or "tool:flip_h" or "tool:strip_exif" => Path.GetExtension(sourcePath),
            "tool:compress" => ".jpg",
            _ => operation.TargetExtension
        };
    }

    private static void ProcessSingleImage(string sourcePath, string stagingPath, FileOperation operation, JsonObject? parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Decode source bitmap
        BitmapDecoder decoder;
        using (var fs = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        }

        BitmapSource frame = decoder.Frames[0];
        BitmapSource resultSource = frame;

        if (operation.Id == "tool:crop")
        {
            int x = parameters?["x"]?.GetValue<int>() ?? 0;
            int y = parameters?["y"]?.GetValue<int>() ?? 0;
            int width = parameters?["width"]?.GetValue<int>() ?? (int)frame.PixelWidth;
            int height = parameters?["height"]?.GetValue<int>() ?? (int)frame.PixelHeight;

            int clampedX = Math.Clamp(x, 0, frame.PixelWidth - 1);
            int clampedY = Math.Clamp(y, 0, frame.PixelHeight - 1);
            int clampedW = Math.Clamp(width, 1, frame.PixelWidth - clampedX);
            int clampedH = Math.Clamp(height, 1, frame.PixelHeight - clampedY);

            resultSource = new CroppedBitmap(frame, new Int32Rect(clampedX, clampedY, clampedW, clampedH));
        }
        else if (operation.Id == "tool:resize")
        {
            double scale = parameters?["scale"]?.GetValue<double>() ?? 0.5;
            int targetW = parameters?["width"]?.GetValue<int>() ?? (int)(frame.PixelWidth * scale);
            int targetH = parameters?["height"]?.GetValue<int>() ?? (int)(frame.PixelHeight * scale);

            double scaleX = (double)targetW / frame.PixelWidth;
            double scaleY = (double)targetH / frame.PixelHeight;

            var transform = new ScaleTransform(scaleX, scaleY);
            resultSource = new TransformedBitmap(frame, transform);
        }
        else if (operation.Id == "tool:rotate_cw")
        {
            var transform = new RotateTransform(90);
            resultSource = new TransformedBitmap(frame, transform);
        }
        else if (operation.Id == "tool:flip_h")
        {
            var transform = new ScaleTransform(-1, 1);
            resultSource = new TransformedBitmap(frame, transform);
        }
        else if (operation.Id == "tool:strip_exif")
        {
            // Re-encoding without copying Metadata automatically strips EXIF
            resultSource = frame;
        }

        // Determine encoder
        string ext = Path.GetExtension(stagingPath).ToLowerInvariant();
        BitmapEncoder encoder = ext switch
        {
            ".jpg" or ".jpeg" => new JpegBitmapEncoder
            {
                QualityLevel = parameters?["quality"]?.GetValue<int>() ?? 85
            },
            ".bmp" => new BmpBitmapEncoder(),
            ".gif" => new GifBitmapEncoder(),
            ".tiff" or ".tif" => new TiffBitmapEncoder(),
            _ => new PngBitmapEncoder()
        };

        encoder.Frames.Add(BitmapFrame.Create(resultSource));

        using (var outStream = new FileStream(stagingPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            encoder.Save(outStream);
        }
    }
}
