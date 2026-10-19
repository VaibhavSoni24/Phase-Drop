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
}
