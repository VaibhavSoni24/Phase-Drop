using PhaseDrop.Core.IO;

namespace PhaseDrop.Core.Tests;

public class CollisionSafeWriterTests : IDisposable
{
    private readonly string _testDir;

    public CollisionSafeWriterTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"PhaseDrop_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    [Fact]
    public void GetSafeOutputPath_ReturnsOriginal_WhenFileDoesNotExist()
    {
        string path = CollisionSafeWriter.GetSafeOutputPath(_testDir, "sample", ".png");
        Assert.Equal(Path.Combine(_testDir, "sample.png"), path);
    }

    [Fact]
    public void GetSafeOutputPath_AppendsIndex_WhenFileExists()
    {
        string existing = Path.Combine(_testDir, "sample.png");
        File.WriteAllText(existing, "dummy");

        string path = CollisionSafeWriter.GetSafeOutputPath(_testDir, "sample", ".png");
        Assert.Equal(Path.Combine(_testDir, "sample (1).png"), path);

        File.WriteAllText(path, "dummy2");
        string path2 = CollisionSafeWriter.GetSafeOutputPath(_testDir, "sample", ".png");
        Assert.Equal(Path.Combine(_testDir, "sample (2).png"), path2);
    }

    [Fact]
    public void StagingLifecycle_CreatesAndCommitsAtomically()
    {
        string staging = CollisionSafeWriter.CreateStagingPath(".txt");
        Assert.True(Directory.Exists(Path.GetDirectoryName(staging)));

        File.WriteAllText(staging, "Hello Phase Drop");
        string destination = Path.Combine(_testDir, "final.txt");

        CollisionSafeWriter.CommitStagingFile(staging, destination);

        Assert.False(File.Exists(staging));
        Assert.True(File.Exists(destination));
        Assert.Equal("Hello Phase Drop", File.ReadAllText(destination));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }
        catch { }
    }
}
