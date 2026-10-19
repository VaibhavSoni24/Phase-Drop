namespace PhaseDrop.Core.IO;

public static class CollisionSafeWriter
{
    private static readonly string TempStagingRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PhaseDrop",
        "temp");

    public static string GetSafeOutputPath(string targetDirectory, string baseName, string targetExtension)
    {
        Directory.CreateDirectory(targetDirectory);

        if (!targetExtension.StartsWith('.'))
            targetExtension = "." + targetExtension;

        // Clean baseName from existing extension if matches target
        string cleanBase = Path.GetFileNameWithoutExtension(baseName);
        string candidate = Path.Combine(targetDirectory, $"{cleanBase}{targetExtension}");

        if (!File.Exists(candidate) && !Directory.Exists(candidate))
            return candidate;

        int counter = 1;
        while (true)
        {
            candidate = Path.Combine(targetDirectory, $"{cleanBase} ({counter}){targetExtension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
                return candidate;
            counter++;
        }
    }

    public static string CreateStagingPath(string extension)
    {
        Directory.CreateDirectory(TempStagingRoot);
        if (!extension.StartsWith('.'))
            extension = "." + extension;
        return Path.Combine(TempStagingRoot, $"staging-{Guid.NewGuid():N}{extension}");
    }

    public static void CommitStagingFile(string stagingPath, string finalDestinationPath)
    {
        if (!File.Exists(stagingPath))
            throw new FileNotFoundException("Staging file does not exist", stagingPath);

        string? dir = Path.GetDirectoryName(finalDestinationPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // Atomic move
        File.Move(stagingPath, finalDestinationPath, overwrite: false);
    }

    public static void CleanStagingFile(string? stagingPath)
    {
        if (string.IsNullOrEmpty(stagingPath))
            return;

        try
        {
            if (File.Exists(stagingPath))
                File.Delete(stagingPath);
        }
        catch
        {
            // Best effort cleanup
        }
    }

    public static void PruneOrphanedStagingFiles()
    {
        try
        {
            if (!Directory.Exists(TempStagingRoot))
                return;

            var cutoff = DateTime.UtcNow.AddHours(-12);
            foreach (var file in Directory.EnumerateFiles(TempStagingRoot, "staging-*"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff)
                        File.Delete(file);
                }
                catch
                {
                    // Ignore locked or inaccessible files
                }
            }
        }
        catch
        {
            // Ignore staging root scan errors
        }
    }
}
