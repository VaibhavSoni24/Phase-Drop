using PhaseDrop.Infrastructure.Configuration;

namespace PhaseDrop.Infrastructure.Tests;

public class UserSettingsTests
{
    [Fact]
    public void UserSettings_HasValidDefaults()
    {
        var settings = new UserSettings();
        Assert.Equal("System", settings.Theme);
        Assert.True(settings.MaxConcurrency >= 1);
        Assert.True(settings.KeepOriginalsSafe);
    }

    [Fact]
    public void SaveAndLoad_PersistsValuesAtomically()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"PhaseDrop_SettingsTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string settingsFile = Path.Combine(tempDir, "settings.json");

        try
        {
            var initial = new UserSettings
            {
                Theme = "Dark",
                MaxConcurrency = 4,
                LaunchAtStartup = false,
                ShowNotifications = false
            };

            initial.Save(settingsFile);
            Assert.True(File.Exists(settingsFile));

            var loaded = UserSettings.Load(settingsFile);
            Assert.Equal("Dark", loaded.Theme);
            Assert.Equal(4, loaded.MaxConcurrency);
            Assert.False(loaded.ShowNotifications);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Load_CorruptedSettings_ReturnsDefaultsAndBacksUpCorruptedFile()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"PhaseDrop_CorruptSettings_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string settingsFile = Path.Combine(tempDir, "settings.json");

        try
        {
            // Write corrupt JSON
            File.WriteAllText(settingsFile, "{ \"Theme\": \"Dark\", \"MaxConcurrency\": INVALID_JSON_VALUE");

            var loaded = UserSettings.Load(settingsFile);
            Assert.NotNull(loaded);
            Assert.Equal("System", loaded.Theme); // default fallback

            // Verify backup copy exists
            string backupFile = Path.Combine(tempDir, "settings.corrupted.json");
            Assert.True(File.Exists(backupFile));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
