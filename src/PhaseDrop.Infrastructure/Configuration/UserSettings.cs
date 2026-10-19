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

    public static UserSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<UserSettings>(json, JsonOptions);
                if (settings != null)
                    return settings;
            }
        }
        catch
        {
            // Fallback to defaults on corrupt settings
        }

        return new UserSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            string json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Best effort settings save
        }
    }
}
