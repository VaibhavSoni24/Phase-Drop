using System.Text.Json;

namespace PhaseDrop.Infrastructure.Logging;

public static class AppLogger
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PhaseDrop",
        "logs");

    private static readonly string LogFilePath = Path.Combine(LogDirectory, "events.jsonl");
    private static readonly string BackupLogFilePath = Path.Combine(LogDirectory, "events.1.jsonl");
    private const long MaxLogSizeBytes = 4 * 1024 * 1024; // 4 MB

    private static readonly object SyncLock = new();

    public static void Log(string level, string message, object? context = null)
    {
        lock (SyncLock)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);

                var fileInfo = new FileInfo(LogFilePath);
                if (fileInfo.Exists && fileInfo.Length > MaxLogSizeBytes)
                {
                    File.Move(LogFilePath, BackupLogFilePath, overwrite: true);
                }

                var entry = new
                {
                    timestamp = DateTimeOffset.UtcNow,
                    level,
                    message,
                    context
                };

                string line = JsonSerializer.Serialize(entry) + Environment.NewLine;
                File.AppendAllText(LogFilePath, line);
            }
            catch
            {
                // Never crash application on logging failure
            }
        }
    }

    public static void Info(string message, object? context = null) => Log("INFO", message, context);
    public static void Warning(string message, object? context = null) => Log("WARN", message, context);
    public static void Error(string message, object? context = null) => Log("ERROR", message, context);

    public static void PruneOldLogs()
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
                return;

            var cutoff = DateTime.UtcNow.AddDays(-7);
            foreach (var file in Directory.EnumerateFiles(LogDirectory, "*.jsonl"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff)
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // Ignore locked files
                }
            }
        }
        catch
        {
            // Ignore directory scan errors
        }
    }
}
