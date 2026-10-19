using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PhaseDrop.Core.IO;
using PhaseDrop.Core.Models;
using PhaseDrop.Core.Queue;

namespace PhaseDrop.Engines.Media;

public sealed partial class MediaConversionEngine : IConversionEngine
{
    private static readonly HashSet<string> SupportedOperationIds = new(StringComparer.OrdinalIgnoreCase)
    {
        // Video
        "convert:mp4", "convert:webm", "convert:mkv", "convert:mov", "convert:avi", "convert:gif",
        "tool:trim", "tool:compress", "tool:extract_audio", "tool:to_gif", "tool:speed", "tool:thumbnail",
        
        // Audio
        "convert:mp3", "convert:wav", "convert:flac", "convert:aac", "convert:ogg", "convert:opus",
        "tool:normalize",

        // WebP image conversion bridge via FFmpeg
        "convert:webp"
    };

    private readonly string? _ffmpegPath;
    private readonly string? _ffprobePath;

    public MediaConversionEngine(string? customFfmpegPath = null, string? customFfprobePath = null)
    {
        _ffmpegPath = customFfmpegPath ?? LocateBinary("ffmpeg.exe");
        _ffprobePath = customFfprobePath ?? LocateBinary("ffprobe.exe");
    }

    public bool CanHandle(FileOperation operation)
    {
        return SupportedOperationIds.Contains(operation.Id);
    }

    public async Task<TimeSpan?> ProbeDurationAsync(string filePath)
    {
        string? binary = _ffprobePath ?? _ffmpegPath;
        if (string.IsNullOrEmpty(binary) || !File.Exists(binary) || !File.Exists(filePath))
            return null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = binary,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(filePath);

            using var process = Process.Start(psi);
            if (process == null) return null;

            string stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var match = Regex.Match(stderr, @"Duration:\s*(\d{2}):(\d{2}):(\d{2}(?:\.\d+)?)");
            if (match.Success &&
                int.TryParse(match.Groups[1].Value, out int hours) &&
                int.TryParse(match.Groups[2].Value, out int minutes) &&
                double.TryParse(match.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds))
            {
                return TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
            }
        }
        catch
        {
            // Ignore probe exceptions
        }

        return null;
    }

    public async Task<bool> HasAudioStreamAsync(string filePath)
    {
        string? binary = _ffprobePath ?? _ffmpegPath;
        if (string.IsNullOrEmpty(binary) || !File.Exists(binary) || !File.Exists(filePath))
            return false;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = binary,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(filePath);

            using var process = Process.Start(psi);
            if (process == null) return false;

            string stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return stderr.Contains("Audio:", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public async Task<JobResult> ExecuteAsync(ConversionJob job, IProgress<JobProgress> progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(_ffmpegPath) || !File.Exists(_ffmpegPath))
        {
            throw new FileNotFoundException("FFmpeg media processing engine binary not found. Please ensure ffmpeg.exe is installed under engines/media/ or present in PATH.");
        }

        var outputFiles = new List<string>();
        int total = job.SourcePaths.Count;

        for (int i = 0; i < total; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string sourcePath = job.SourcePaths[i];
            string fileName = Path.GetFileName(sourcePath);
            progress.Report(new JobProgress($"Transcoding {fileName}", (double)i / total, i, total, fileName));

            string targetExt = GetTargetExtension(job.Operation, sourcePath);
            string targetDir = job.OutputDirectory ?? Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory;
            string outputPath = CollisionSafeWriter.GetSafeOutputPath(targetDir, Path.GetFileNameWithoutExtension(sourcePath), targetExt);
            string stagingPath = CollisionSafeWriter.CreateStagingPath(targetExt);

            try
            {
                // Probe total media duration for accurate progress tracking
                var duration = await ProbeDurationAsync(sourcePath);
                double totalSeconds = (duration.HasValue && duration.Value.TotalSeconds > 0) ? duration.Value.TotalSeconds : 60.0;

                await RunFfmpegAsync(sourcePath, stagingPath, job.Operation, job.Parameters, totalSeconds, progress, i, total, cancellationToken);

                // Verify output non-empty
                var fi = new FileInfo(stagingPath);
                if (!fi.Exists || fi.Length == 0)
                {
                    throw new InvalidOperationException($"Conversion produced an empty or missing output file: {Path.GetFileName(stagingPath)}");
                }

                // Validate output stream presence, container, and non-empty duration
                await ValidateMediaOutputAsync(stagingPath, job.Operation);

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

    private async Task ValidateMediaOutputAsync(string filePath, FileOperation operation)
    {
        string? binary = _ffprobePath ?? _ffmpegPath;
        if (string.IsNullOrEmpty(binary) || !File.Exists(binary))
            return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = binary,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(filePath);

            using var process = Process.Start(psi);
            if (process == null) return;

            string stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            // Audio validation: must contain Audio: stream
            if (ext is ".mp3" or ".wav" or ".flac" or ".aac" or ".m4a" or ".ogg" or ".opus" || operation.Category == OperationCategory.Audio)
            {
                if (!stderr.Contains("Audio:", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Validation failed: converted audio file {Path.GetFileName(filePath)} contains no audio stream.");
                }
            }
            // Video validation: must contain Video: stream (except thumbnails / gifs handled as image/video)
            else if (ext is ".mp4" or ".webm" or ".mkv" or ".mov" or ".avi")
            {
                if (!stderr.Contains("Video:", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Validation failed: converted video file {Path.GetFileName(filePath)} contains no video stream.");
                }
            }
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch
        {
            // Probe failure should not fail a verified output file
        }
    }

    private static string GetTargetExtension(FileOperation operation, string sourcePath)
    {
        if (operation.Id.StartsWith("convert:", StringComparison.OrdinalIgnoreCase))
            return operation.TargetExtension;

        return operation.Id switch
        {
            "tool:extract_audio" => ".mp3",
            "tool:to_gif" => ".gif",
            "tool:thumbnail" => ".png",
            "tool:trim" or "tool:compress" or "tool:speed" or "tool:normalize" => Path.GetExtension(sourcePath),
            _ => operation.TargetExtension
        };
    }

    private async Task RunFfmpegAsync(
        string sourcePath,
        string stagingPath,
        FileOperation operation,
        JsonObject? parameters,
        double totalSeconds,
        IProgress<JobProgress> progress,
        int itemIndex,
        int totalItems,
        CancellationToken cancellationToken)
    {
        bool hasAudio = await HasAudioStreamAsync(sourcePath);

        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        // Always overwrite staging file and suppress banner
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("info");

        // Tool specific pre-input parameters
        if (operation.Id == "tool:trim")
        {
            string? start = parameters?["start"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(start))
            {
                psi.ArgumentList.Add("-ss");
                psi.ArgumentList.Add(start);
            }
        }
        else if (operation.Id == "tool:thumbnail")
        {
            string? time = parameters?["time"]?.GetValue<string>() ?? "00:00:01";
            psi.ArgumentList.Add("-ss");
            psi.ArgumentList.Add(time);
        }

        // Input file
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(sourcePath);

        // Tool and conversion specific encoding parameters
        if (operation.Id == "tool:trim")
        {
            string? duration = parameters?["duration"]?.GetValue<string>();
            string? end = parameters?["end"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(duration))
            {
                psi.ArgumentList.Add("-t");
                psi.ArgumentList.Add(duration);
            }
            else if (!string.IsNullOrEmpty(end))
            {
                psi.ArgumentList.Add("-to");
                psi.ArgumentList.Add(end);
            }
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add("copy");
        }
        else if (operation.Id == "convert:mp4")
        {
            psi.ArgumentList.Add("-c:v");
            psi.ArgumentList.Add("libx264");
            psi.ArgumentList.Add("-pix_fmt");
            psi.ArgumentList.Add("yuv420p");
            if (hasAudio)
            {
                psi.ArgumentList.Add("-c:a");
                psi.ArgumentList.Add("aac");
                psi.ArgumentList.Add("-b:a");
                psi.ArgumentList.Add("192k");
            }
        }
        else if (operation.Id == "convert:webm")
        {
            psi.ArgumentList.Add("-c:v");
            psi.ArgumentList.Add("libvpx-vp9");
            psi.ArgumentList.Add("-crf");
            psi.ArgumentList.Add("32");
            psi.ArgumentList.Add("-b:v");
            psi.ArgumentList.Add("0");
            if (hasAudio)
            {
                psi.ArgumentList.Add("-c:a");
                psi.ArgumentList.Add("libopus");
                psi.ArgumentList.Add("-b:a");
                psi.ArgumentList.Add("128k");
            }
        }
        else if (operation.Id == "convert:mkv")
        {
            psi.ArgumentList.Add("-c:v");
            psi.ArgumentList.Add("libx264");
            if (hasAudio)
            {
                psi.ArgumentList.Add("-c:a");
                psi.ArgumentList.Add("aac");
            }
        }
        else if (operation.Id == "convert:mov")
        {
            psi.ArgumentList.Add("-c:v");
            psi.ArgumentList.Add("libx264");
            psi.ArgumentList.Add("-pix_fmt");
            psi.ArgumentList.Add("yuv420p");
            if (hasAudio)
            {
                psi.ArgumentList.Add("-c:a");
                psi.ArgumentList.Add("aac");
            }
        }
        else if (operation.Id == "convert:avi")
        {
            psi.ArgumentList.Add("-c:v");
            psi.ArgumentList.Add("mpeg4");
            psi.ArgumentList.Add("-qscale:v");
            psi.ArgumentList.Add("3");
            if (hasAudio)
            {
                psi.ArgumentList.Add("-c:a");
                psi.ArgumentList.Add("mp3");
            }
        }
        else if (operation.Id is "convert:gif" or "tool:to_gif")
        {
            int fps = parameters?["fps"]?.GetValue<int>() ?? 15;
            int width = parameters?["width"]?.GetValue<int>() ?? 480;
            psi.ArgumentList.Add("-vf");
            psi.ArgumentList.Add($"fps={fps},scale={width}:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse");
        }
        else if (operation.Id == "tool:thumbnail")
        {
            psi.ArgumentList.Add("-vframes");
            psi.ArgumentList.Add("1");
        }
        else if (operation.Id == "tool:compress")
        {
            if (operation.Category == OperationCategory.Video)
            {
                psi.ArgumentList.Add("-c:v");
                psi.ArgumentList.Add("libx264");
                psi.ArgumentList.Add("-crf");
                psi.ArgumentList.Add(parameters?["crf"]?.GetValue<string>() ?? "28");
                psi.ArgumentList.Add("-preset");
                psi.ArgumentList.Add("fast");
                if (hasAudio)
                {
                    psi.ArgumentList.Add("-c:a");
                    psi.ArgumentList.Add("aac");
                    psi.ArgumentList.Add("-b:a");
                    psi.ArgumentList.Add("128k");
                }
            }
            else
            {
                string targetExt = Path.GetExtension(stagingPath).ToLowerInvariant();
                string audioCodec = targetExt switch
                {
                    ".mp3" => "libmp3lame",
                    ".aac" or ".m4a" => "aac",
                    ".ogg" => "libvorbis",
                    ".opus" => "libopus",
                    _ => "libmp3lame"
                };
                psi.ArgumentList.Add("-c:a");
                psi.ArgumentList.Add(audioCodec);
                psi.ArgumentList.Add("-b:a");
                psi.ArgumentList.Add(parameters?["bitrate"]?.GetValue<string>() ?? "128k");
            }
        }
        else if (operation.Id == "tool:speed")
        {
            double speed = parameters?["speed"]?.GetValue<double>() ?? 1.5;
            double pts = 1.0 / speed;

            if (hasAudio)
            {
                psi.ArgumentList.Add("-filter_complex");
                psi.ArgumentList.Add(FormattableString.Invariant($"[0:v]setpts={pts:F4}*PTS[v];[0:a]atempo={speed:F2}[a]"));
                psi.ArgumentList.Add("-map");
                psi.ArgumentList.Add("[v]");
                psi.ArgumentList.Add("-map");
                psi.ArgumentList.Add("[a]");
            }
            else
            {
                psi.ArgumentList.Add("-vf");
                psi.ArgumentList.Add(FormattableString.Invariant($"setpts={pts:F4}*PTS"));
            }
        }
        else if (operation.Id == "tool:normalize")
        {
            psi.ArgumentList.Add("-af");
            psi.ArgumentList.Add("loudnorm=I=-16:TP=-1.5:LRA=11");
        }
        else if (operation.Id is "convert:mp3" or "tool:extract_audio")
        {
            psi.ArgumentList.Add("-vn");
            psi.ArgumentList.Add("-c:a");
            psi.ArgumentList.Add("libmp3lame");
            psi.ArgumentList.Add("-q:a");
            psi.ArgumentList.Add("2");
        }
        else if (operation.Id == "convert:wav")
        {
            psi.ArgumentList.Add("-vn");
            psi.ArgumentList.Add("-c:a");
            psi.ArgumentList.Add("pcm_s16le");
        }
        else if (operation.Id == "convert:flac")
        {
            psi.ArgumentList.Add("-vn");
            psi.ArgumentList.Add("-c:a");
            psi.ArgumentList.Add("flac");
        }
        else if (operation.Id == "convert:aac")
        {
            psi.ArgumentList.Add("-vn");
            psi.ArgumentList.Add("-c:a");
            psi.ArgumentList.Add("aac");
            psi.ArgumentList.Add("-b:a");
            psi.ArgumentList.Add("192k");
        }
        else if (operation.Id == "convert:ogg")
        {
            psi.ArgumentList.Add("-vn");
            psi.ArgumentList.Add("-c:a");
            psi.ArgumentList.Add("libvorbis");
            psi.ArgumentList.Add("-q:a");
            psi.ArgumentList.Add("4");
        }
        else if (operation.Id == "convert:opus")
        {
            psi.ArgumentList.Add("-vn");
            psi.ArgumentList.Add("-c:a");
            psi.ArgumentList.Add("libopus");
            psi.ArgumentList.Add("-b:a");
            psi.ArgumentList.Add("128k");
        }
        else if (operation.Id == "convert:webp")
        {
            psi.ArgumentList.Add("-c:v");
            psi.ArgumentList.Add("libwebp");
            psi.ArgumentList.Add("-quality");
            psi.ArgumentList.Add(parameters?["quality"]?.GetValue<string>() ?? "85");
        }

        // Output destination
        psi.ArgumentList.Add(stagingPath);

        using var process = new Process { StartInfo = psi };
        var errorBuilder = new System.Text.StringBuilder();

        process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            lock (errorBuilder)
            {
                if (errorBuilder.Length < 4096)
                    errorBuilder.AppendLine(e.Data);
            }

            // Parse time progress: time=00:01:23.45
            var match = TimeRegex().Match(e.Data);
            if (match.Success)
            {
                string timeStr = match.Groups[1].Value;
                if (TimeSpan.TryParse(timeStr, CultureInfo.InvariantCulture, out var currentPos))
                {
                    double currentSec = currentPos.TotalSeconds;
                    double itemRatio = Math.Min(0.99, currentSec / Math.Max(1.0, totalSeconds));
                    double aggregate = ((double)itemIndex / totalItems) + (itemRatio / totalItems);
                    progress.Report(new JobProgress($"Transcoding {Path.GetFileName(sourcePath)} ({timeStr})", aggregate, itemIndex, totalItems, Path.GetFileName(sourcePath)));
                }
            }
        };

        process.Start();
        process.BeginErrorReadLine();

        using var reg = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Process already terminated
            }
        });

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            string errSummary = errorBuilder.ToString().Trim();
            string msg = string.IsNullOrEmpty(errSummary) ? $"exit code {process.ExitCode}" : errSummary;
            throw new InvalidOperationException($"FFmpeg conversion failed: {msg}");
        }
    }

    private static string? LocateBinary(string binaryName)
    {
        // 1. App-local engines directory (packaged mode)
        string localPath = Path.Combine(AppContext.BaseDirectory, "engines", "media", binaryName);
        if (File.Exists(localPath))
            return localPath;

        // 2. PATH search (developer / system-wide mode)
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (string part in pathEnv.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                string candidate = Path.Combine(part.Trim(), binaryName);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    [GeneratedRegex(@"time=(\d{2}:\d{2}:\d{2}\.\d+)")]
    private static partial Regex TimeRegex();
}
