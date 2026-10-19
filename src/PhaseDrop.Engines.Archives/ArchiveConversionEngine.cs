using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Security;
using PhaseDrop.Core.IO;
using PhaseDrop.Core.Models;
using PhaseDrop.Core.Queue;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.SevenZip;

namespace PhaseDrop.Engines.Archives;

public sealed class ArchiveConversionEngine : IConversionEngine
{
    private static readonly HashSet<string> SupportedOperationIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "convert:zip", "convert:7z", "convert:tar", "convert:tgz",
        "tool:pack_zip", "tool:pack_7z", "tool:extract", "tool:test_archive"
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
                "convert:zip" or "tool:pack_zip" => PackZip(job, progress, cancellationToken),
                "convert:7z" or "tool:pack_7z" => PackSevenZip(job, progress, cancellationToken),
                "convert:tar" => PackTar(job, false, progress, cancellationToken),
                "convert:tgz" => PackTar(job, true, progress, cancellationToken),
                "tool:extract" => ExtractArchive(job, progress, cancellationToken),
                "tool:test_archive" => TestArchive(job, progress, cancellationToken),
                _ => throw new NotSupportedException($"Archive operation {job.Operation.Id} not supported.")
            };
        }, cancellationToken);
    }

    private static JobResult PackZip(ConversionJob job, IProgress<JobProgress> progress, CancellationToken cancellationToken)
    {
        string targetDir = job.OutputDirectory ?? Path.GetDirectoryName(job.SourcePaths[0]) ?? Environment.CurrentDirectory;
        string baseName = job.SourcePaths.Count == 1 ? Path.GetFileNameWithoutExtension(job.SourcePaths[0]) : "Archive";
        string outputPath = CollisionSafeWriter.GetSafeOutputPath(targetDir, baseName, ".zip");
        string stagingPath = CollisionSafeWriter.CreateStagingPath(".zip");

        try
        {
            using (var zipStream = new FileStream(stagingPath, FileMode.Create, FileAccess.Write))
            using (var archive = new System.IO.Compression.ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                int total = job.SourcePaths.Count;
                for (int i = 0; i < total; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string path = job.SourcePaths[i];
                    progress.Report(new JobProgress($"Archiving {Path.GetFileName(path)}", (double)i / total, i, total));

                    if (File.Exists(path))
                    {
                        archive.CreateEntryFromFile(path, Path.GetFileName(path), CompressionLevel.Optimal);
                    }
                    else if (Directory.Exists(path))
                    {
                        AddDirectoryToZip(archive, path, Path.GetFileName(path));
                    }
                }
            }

            CollisionSafeWriter.CommitStagingFile(stagingPath, outputPath);
            return new JobResult(true, [outputPath]);
        }
        catch
        {
            CollisionSafeWriter.CleanStagingFile(stagingPath);
            throw;
        }
    }

    private static JobResult PackSevenZip(ConversionJob job, IProgress<JobProgress> progress, CancellationToken cancellationToken)
    {
        string targetDir = job.OutputDirectory ?? Path.GetDirectoryName(job.SourcePaths[0]) ?? Environment.CurrentDirectory;
        string baseName = job.SourcePaths.Count == 1 ? Path.GetFileNameWithoutExtension(job.SourcePaths[0]) : "Archive";
        string outputPath = CollisionSafeWriter.GetSafeOutputPath(targetDir, baseName, ".7z");
        string stagingPath = CollisionSafeWriter.CreateStagingPath(".7z");

        try
        {
            using (var fs = File.Create(stagingPath))
            using (var writer = WriterFactory.OpenWriter(fs, ArchiveType.SevenZip, new SevenZipWriterOptions(CompressionType.LZMA)))
            {
                int total = job.SourcePaths.Count;
                for (int i = 0; i < total; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string path = job.SourcePaths[i];
                    progress.Report(new JobProgress($"Compressing {Path.GetFileName(path)} into 7z", (double)i / total, i, total));

                    if (File.Exists(path))
                    {
                        using var fileStream = File.OpenRead(path);
                        writer.Write(Path.GetFileName(path), fileStream, File.GetLastWriteTime(path));
                    }
                    else if (Directory.Exists(path))
                    {
                        AddDirectoryToSevenZip(writer, path, Path.GetFileName(path));
                    }
                }
            }

            CollisionSafeWriter.CommitStagingFile(stagingPath, outputPath);
            return new JobResult(true, [outputPath]);
        }
        catch
        {
            CollisionSafeWriter.CleanStagingFile(stagingPath);
            throw;
        }
    }

    private static JobResult PackTar(ConversionJob job, bool gzip, IProgress<JobProgress> progress, CancellationToken cancellationToken)
    {
        string ext = gzip ? ".tar.gz" : ".tar";
        string targetDir = job.OutputDirectory ?? Path.GetDirectoryName(job.SourcePaths[0]) ?? Environment.CurrentDirectory;
        string baseName = job.SourcePaths.Count == 1 ? Path.GetFileNameWithoutExtension(job.SourcePaths[0]) : "Archive";
        string outputPath = CollisionSafeWriter.GetSafeOutputPath(targetDir, baseName, ext);
        string stagingPath = CollisionSafeWriter.CreateStagingPath(ext);

        try
        {
            using var fileStream = File.Create(stagingPath);
            Stream tarStream = gzip ? new GZipStream(fileStream, CompressionLevel.Optimal) : fileStream;

            using (var writer = new TarWriter(tarStream, leaveOpen: gzip))
            {
                int total = job.SourcePaths.Count;
                for (int i = 0; i < total; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string path = job.SourcePaths[i];
                    progress.Report(new JobProgress($"Adding {Path.GetFileName(path)} to TAR", (double)i / total, i, total));

                    if (File.Exists(path))
                    {
                        writer.WriteEntry(path, Path.GetFileName(path));
                    }
                }
            }

            if (gzip)
            {
                tarStream.Dispose();
            }

            CollisionSafeWriter.CommitStagingFile(stagingPath, outputPath);
            return new JobResult(true, [outputPath]);
        }
        catch
        {
            CollisionSafeWriter.CleanStagingFile(stagingPath);
            throw;
        }
    }

    private static JobResult ExtractArchive(ConversionJob job, IProgress<JobProgress> progress, CancellationToken cancellationToken)
    {
        var outputDirs = new List<string>();

        foreach (string archivePath in job.SourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string parentDir = job.OutputDirectory ?? Path.GetDirectoryName(archivePath) ?? Environment.CurrentDirectory;
            string folderName = Path.GetFileNameWithoutExtension(archivePath);
            string destinationFolder = Path.Combine(parentDir, folderName);

            // Ensure destination uniqueness
            int counter = 1;
            while (Directory.Exists(destinationFolder))
            {
                destinationFolder = Path.Combine(parentDir, $"{folderName} ({counter})");
                counter++;
            }

            Directory.CreateDirectory(destinationFolder);
            string canonicalDest = Path.GetFullPath(destinationFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            using var archive = ArchiveFactory.OpenArchive(archivePath);
            int total = archive.Entries.Count();
            int current = 0;

            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                current++;
                progress.Report(new JobProgress($"Extracting {entry.Key}", (double)current / total, current, total));

                // Strict Zip Slip defense: verify entry canonical path stays inside destination
                string entryDestination = Path.GetFullPath(Path.Combine(destinationFolder, entry.Key ?? "file"));
                if (!entryDestination.StartsWith(canonicalDest, StringComparison.OrdinalIgnoreCase))
                {
                    throw new SecurityException($"Malicious archive entry path traversal detected (Zip Slip): {entry.Key}");
                }

                string? entryDir = Path.GetDirectoryName(entryDestination);
                if (!string.IsNullOrEmpty(entryDir))
                    Directory.CreateDirectory(entryDir);

                entry.WriteToFile(entryDestination, new ExtractionOptions { Overwrite = true });
            }

            outputDirs.Add(destinationFolder);
        }

        return new JobResult(true, outputDirs);
    }

    private static JobResult TestArchive(ConversionJob job, IProgress<JobProgress> progress, CancellationToken cancellationToken)
    {
        int passed = 0;
        foreach (string path in job.SourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(new JobProgress($"Verifying {Path.GetFileName(path)}", 0.5));

            using var archive = ArchiveFactory.OpenArchive(path);
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Test entry stream reading without writing to disk
                using var entryStream = entry.OpenEntryStream();
                byte[] buffer = new byte[81920];
                while (entryStream.Read(buffer, 0, buffer.Length) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            passed++;
        }

        return new JobResult(true, job.SourcePaths, $"Successfully verified {passed} archive(s) with intact checksums.");
    }

    private static void AddDirectoryToZip(System.IO.Compression.ZipArchive archive, string rootPath, string entryPrefix)
    {
        foreach (string file in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(rootPath, file);
            string entryName = Path.Combine(entryPrefix, rel).Replace('\\', '/');
            archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
        }
    }

    private static void AddDirectoryToSevenZip(IWriter writer, string rootPath, string entryPrefix)
    {
        foreach (string file in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(rootPath, file);
            string entryName = Path.Combine(entryPrefix, rel).Replace('\\', '/');
            using var fileStream = File.OpenRead(file);
            writer.Write(entryName, fileStream, File.GetLastWriteTime(file));
        }
    }
}
