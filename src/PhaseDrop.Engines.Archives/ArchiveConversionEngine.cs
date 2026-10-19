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

            const int MaxEntryCount = 20000;
            const long MaxTotalUncompressedBytes = 4L * 1024 * 1024 * 1024; // 4 GB safety ceiling
            long totalExtractedBytes = 0;
            var extractedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var archive = ArchiveFactory.OpenArchive(archivePath);
                int total = archive.Entries.Count();
                if (total > MaxEntryCount)
                {
                    throw new InvalidOperationException($"Archive contains {total} entries, exceeding the maximum safe limit of {MaxEntryCount}.");
                }

                int current = 0;

                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    current++;
                    progress.Report(new JobProgress($"Extracting {entry.Key}", (double)current / total, current, total));

                    string entryKey = entry.Key ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(entryKey))
                    {
                        continue;
                    }

                    // 1. Rooted, drive-qualified, and alternate data stream (ADS) validation
                    if (entryKey.Contains(':') || Path.IsPathRooted(entryKey))
                    {
                        throw new SecurityException($"Malicious archive entry path detected (drive-qualified or rooted Zip Slip): {entryKey}");
                    }

                    // 2. Reject symlink and hardlink entries that could escape or manipulate external files
                    if (!string.IsNullOrEmpty(entry.LinkTarget))
                    {
                        throw new SecurityException($"Archive link entry rejected for security (Zip Slip): {entryKey} -> {entry.LinkTarget}");
                    }

                    // 3. Reject encrypted/password-protected entries cleanly
                    if (entry.IsEncrypted)
                    {
                        throw new NotSupportedException($"Encrypted archive entry '{entryKey}' is not supported.");
                    }

                    // 4. Reject invalid Windows filename characters
                    char[] invalidChars = Path.GetInvalidFileNameChars().Where(c => c is not '/' and not '\\').ToArray();
                    if (entryKey.IndexOfAny(invalidChars) >= 0)
                    {
                        throw new SecurityException($"Archive entry contains invalid filename characters: {entryKey}");
                    }

                    // 5. Traversal segments check ('..' segments)
                    string normalizedKey = entryKey.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                    var segments = normalizedKey.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Any(s => s == ".."))
                    {
                        throw new SecurityException($"Malicious archive entry path traversal detected (Zip Slip): {entryKey}");
                    }

                    // 6. Strict canonical containment check (prevent sibling prefix attacks like dest vs dest_evil)
                    string entryDestination = Path.GetFullPath(Path.Combine(destinationFolder, normalizedKey));
                    if (!entryDestination.StartsWith(canonicalDest, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new SecurityException($"Malicious archive entry path traversal detected (Zip Slip): {entryKey}");
                    }

                    // 7. Decompression bomb resource exhaustion defense (pre-check via header)
                    if (entry.Size > 0)
                    {
                        if (totalExtractedBytes + entry.Size > MaxTotalUncompressedBytes)
                        {
                            throw new InvalidOperationException("Archive exceeds maximum permitted extraction size (4 GB decompression limit).");
                        }
                    }

                    // 8. Safe handling for duplicate entries and case-insensitive collisions on Windows
                    if (extractedPaths.Contains(entryDestination))
                    {
                        string dir = Path.GetDirectoryName(entryDestination)!;
                        string nameWithoutExt = Path.GetFileNameWithoutExtension(entryDestination);
                        string ext = Path.GetExtension(entryDestination);
                        int disambiguate = 1;

                        while (extractedPaths.Contains(entryDestination) || File.Exists(entryDestination))
                        {
                            entryDestination = Path.Combine(dir, $"{nameWithoutExt} ({disambiguate}){ext}");
                            disambiguate++;
                        }
                    }

                    extractedPaths.Add(entryDestination);

                    string? entryDir = Path.GetDirectoryName(entryDestination);
                    if (!string.IsNullOrEmpty(entryDir))
                    {
                        Directory.CreateDirectory(entryDir);
                    }

                    entry.WriteToFile(entryDestination, new ExtractionOptions { Overwrite = true });

                    // 9. Post-extraction actual disk size check (defense against falsified archive header sizes)
                    if (File.Exists(entryDestination))
                    {
                        long actualWritten = new FileInfo(entryDestination).Length;
                        totalExtractedBytes += actualWritten;
                        if (totalExtractedBytes > MaxTotalUncompressedBytes)
                        {
                            throw new InvalidOperationException("Archive exceeds maximum permitted extraction size (4 GB decompression limit).");
                        }
                    }
                }

                outputDirs.Add(destinationFolder);
            }
            catch
            {
                try
                {
                    if (Directory.Exists(destinationFolder))
                    {
                        Directory.Delete(destinationFolder, true);
                    }
                }
                catch
                {
                    // Best effort cleanup on extraction failure
                }

                throw;
            }
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
