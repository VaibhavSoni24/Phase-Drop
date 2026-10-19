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

    private static readonly HashSet<string> ValidEncodableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".tif", ".ico"
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
            if (!ValidEncodableExtensions.Contains(targetExt))
            {
                throw new NotSupportedException($"Target image format '{targetExt}' is not supported by ImageConversionEngine.");
            }

            string targetDir = job.OutputDirectory ?? Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory;
            string outputPath = CollisionSafeWriter.GetSafeOutputPath(targetDir, Path.GetFileNameWithoutExtension(sourcePath), targetExt);
            string stagingPath = CollisionSafeWriter.CreateStagingPath(targetExt);

            try
            {
                await Task.Run(() => ProcessSingleImage(sourcePath, stagingPath, job.Operation, job.Parameters, cancellationToken), cancellationToken);

                // Verify output integrity and non-zero size before committing
                var fi = new FileInfo(stagingPath);
                if (!fi.Exists || fi.Length == 0)
                {
                    throw new InvalidOperationException($"Image conversion resulted in an empty output file for: {fileName}");
                }

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

        // 1. Decode source bitmap frame
        BitmapDecoder decoder;
        using (var fs = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        }

        if (decoder.Frames.Count == 0)
        {
            throw new InvalidDataException($"Image file contains no valid frames: {Path.GetFileName(sourcePath)}");
        }

        BitmapSource frame = decoder.Frames[0];
        BitmapSource resultSource = frame;

        // 2. Perform Transformations
        if (operation.Id == "tool:crop")
        {
            int x = parameters?["x"]?.GetValue<int>() ?? 0;
            int y = parameters?["y"]?.GetValue<int>() ?? 0;
            int width = parameters?["width"]?.GetValue<int>() ?? (int)frame.PixelWidth;
            int height = parameters?["height"]?.GetValue<int>() ?? (int)frame.PixelHeight;

            int clampedX = Math.Clamp(x, 0, Math.Max(0, frame.PixelWidth - 1));
            int clampedY = Math.Clamp(y, 0, Math.Max(0, frame.PixelHeight - 1));
            int clampedW = Math.Clamp(width, 1, Math.Max(1, frame.PixelWidth - clampedX));
            int clampedH = Math.Clamp(height, 1, Math.Max(1, frame.PixelHeight - clampedY));

            resultSource = new CroppedBitmap(frame, new Int32Rect(clampedX, clampedY, clampedW, clampedH));
        }
        else if (operation.Id == "tool:resize")
        {
            double scale = parameters?["scale"]?.GetValue<double>() ?? 0.5;
            int targetW = parameters?["width"]?.GetValue<int>() ?? Math.Max(1, (int)(frame.PixelWidth * scale));
            int targetH = parameters?["height"]?.GetValue<int>() ?? Math.Max(1, (int)(frame.PixelHeight * scale));

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
            // Re-encoding without cloning Metadata automatically strips EXIF / privacy metadata
            resultSource = frame;
        }

        // 3. Determine Explicit Encoder & Alpha Transparency Handling
        string ext = Path.GetExtension(stagingPath).ToLowerInvariant();
        BitmapEncoder encoder;

        if (ext is ".jpg" or ".jpeg")
        {
            int quality = parameters?["quality"]?.GetValue<int>() ?? (operation.Id == "tool:compress" ? 70 : 85);
            encoder = new JpegBitmapEncoder { QualityLevel = quality };

            // Flatten alpha transparency onto white background so it does not turn black in JPEG
            if (resultSource.Format.BitsPerPixel == 32)
            {
                var visual = new DrawingVisual();
                using (var ctx = visual.RenderOpen())
                {
                    ctx.DrawRectangle(Brushes.White, null, new Rect(0, 0, resultSource.PixelWidth, resultSource.PixelHeight));
                    ctx.DrawImage(resultSource, new Rect(0, 0, resultSource.PixelWidth, resultSource.PixelHeight));
                }
                var rtb = new RenderTargetBitmap(resultSource.PixelWidth, resultSource.PixelHeight, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(visual);
                resultSource = rtb;
            }
        }
        else if (ext == ".png")
        {
            encoder = new PngBitmapEncoder();
        }
        else if (ext == ".bmp")
        {
            encoder = new BmpBitmapEncoder();
        }
        else if (ext == ".gif")
        {
            encoder = new GifBitmapEncoder();
        }
        else if (ext is ".tiff" or ".tif")
        {
            encoder = new TiffBitmapEncoder { Compression = TiffCompressOption.Zip };
        }
        else if (ext == ".ico")
        {
            // Windows Icon format: wrap into standard ICO stream or PngBitmapEncoder
            encoder = new PngBitmapEncoder();
        }
        else
        {
            throw new NotSupportedException($"Image encoder for extension '{ext}' is not supported.");
        }

        encoder.Frames.Add(BitmapFrame.Create(resultSource));

        using (var outStream = new FileStream(stagingPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            encoder.Save(outStream);
        }
    }
}
