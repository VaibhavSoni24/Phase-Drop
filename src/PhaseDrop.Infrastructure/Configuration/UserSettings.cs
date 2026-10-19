using System.Text.Json;

namespace PhaseDrop.Infrastructure.Configuration;

public sealed class UserSettings
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PhaseDrop");

    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string Theme { get; set; } = "System"; // "System", "Light", "Dark"
    public string? CustomOutputDirectory { get; set; }
    public int MaxConcurrency { get; set; } = 2;
    public bool LaunchAtStartup { get; set; } = false;
    public bool ShowNotifications { get; set; } = true;
    public bool KeepOriginalsSafe { get; set; } = true;
    public int DragHorizontalThreshold { get; set; } = 4;
    public int DragVerticalThreshold { get; set; } = 4;
    public bool ShowWelcomeOnStartup { get; set; } = false;

    public static UserSettings Load(string? customPath = null)
    {
        string filePath = customPath ?? SettingsFilePath;
        try
        {
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                var settings = JsonSerializer.Deserialize<UserSettings>(json, JsonOptions);
                if (settings != null)
                    return settings;
            }
        }
        catch (Exception ex)
        {
            Logging.AppLogger.Warning($"Failed to deserialize user settings from {filePath}: {ex.Message}. Falling back to defaults.");
            try
            {
                string targetDir = Path.GetDirectoryName(filePath) ?? SettingsDirectory;
                string corruptedPath = Path.Combine(targetDir, "settings.corrupted.json");
                if (File.Exists(filePath))
                {
                    File.Copy(filePath, corruptedPath, overwrite: true);
                }
            }
            catch
            {
                // Ignore backup copy failure
            }
        }

        return new UserSettings();
    }

    public void Save(string? customPath = null)
    {
        string filePath = customPath ?? SettingsFilePath;
        string dir = Path.GetDirectoryName(filePath) ?? SettingsDirectory;
        string tempFilePath = Path.Combine(dir, $"{Path.GetFileName(filePath)}.tmp.{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(dir);
            string json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(tempFilePath, json);
            File.Move(tempFilePath, filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Logging.AppLogger.Error($"Failed to persist user settings to {filePath}: {ex.Message}");
            try
            {
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
