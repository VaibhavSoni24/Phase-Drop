using System.IO;
using PhaseDrop.Core.Models;
using PhaseDrop.Engines.Archives;

namespace PhaseDrop.Engines.Tests;

public class ArchiveConversionEngineTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _sampleFile1;
    private readonly string _sampleFile2;
    private readonly ArchiveConversionEngine _engine = new();

    public ArchiveConversionEngineTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"PhaseDrop_ArchiveTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _sampleFile1 = Path.Combine(_testDir, "document.txt");
        _sampleFile2 = Path.Combine(_testDir, "notes.txt");
        File.WriteAllText(_sampleFile1, "Hello from document 1");
        File.WriteAllText(_sampleFile2, "Hello from document 2");
    }

    [Fact]
    public async Task PackZip_And_Extract_RestoresOriginalContent()
    {
        var packOp = new FileOperation("convert:zip", "ZIP", ".zip", OperationCategory.Archive);
        var packJob = new ConversionJob([_sampleFile1, _sampleFile2], packOp, _testDir);

        var packResult = await _engine.ExecuteAsync(packJob, new Progress<JobProgress>(), CancellationToken.None);

        Assert.True(packResult.Success);
        Assert.Single(packResult.OutputFiles);
        string zipPath = packResult.OutputFiles[0];
        Assert.True(File.Exists(zipPath));

        // Test extraction
        var extractOp = new FileOperation("tool:extract", "Extract", "", OperationCategory.Archive, true);
        var extractJob = new ConversionJob([zipPath], extractOp, _testDir);

        var extractResult = await _engine.ExecuteAsync(extractJob, new Progress<JobProgress>(), CancellationToken.None);

        Assert.True(extractResult.Success);
        Assert.Single(extractResult.OutputFiles);
        string extractedFolder = extractResult.OutputFiles[0];
        Assert.True(Directory.Exists(extractedFolder));

        string extractedDoc1 = Path.Combine(extractedFolder, "document.txt");
        string extractedDoc2 = Path.Combine(extractedFolder, "notes.txt");
        Assert.True(File.Exists(extractedDoc1));
        Assert.True(File.Exists(extractedDoc2));
        Assert.Equal("Hello from document 1", File.ReadAllText(extractedDoc1));
        Assert.Equal("Hello from document 2", File.ReadAllText(extractedDoc2));
    }

    [Fact]
    public async Task PackSevenZip_CreatesValidSevenZipArchive()
    {
        var packOp = new FileOperation("convert:7z", "7-Zip", ".7z", OperationCategory.Archive);
        var packJob = new ConversionJob([_sampleFile1], packOp, _testDir);

        var packResult = await _engine.ExecuteAsync(packJob, new Progress<JobProgress>(), CancellationToken.None);

        Assert.True(packResult.Success);
        Assert.Single(packResult.OutputFiles);
        Assert.True(File.Exists(packResult.OutputFiles[0]));
        Assert.EndsWith(".7z", packResult.OutputFiles[0]);
    }

    [Fact]
    public async Task TestArchive_VerifiesValidArchive()
    {
        var packOp = new FileOperation("convert:zip", "ZIP", ".zip", OperationCategory.Archive);
        var packJob = new ConversionJob([_sampleFile1], packOp, _testDir);
        var packResult = await _engine.ExecuteAsync(packJob, new Progress<JobProgress>(), CancellationToken.None);

        var testOp = new FileOperation("tool:test_archive", "Verify", "", OperationCategory.Archive, true);
        var testJob = new ConversionJob(packResult.OutputFiles, testOp);
        var testResult = await _engine.ExecuteAsync(testJob, new Progress<JobProgress>(), CancellationToken.None);

        Assert.True(testResult.Success);
    }

    [Fact]
    public async Task Extract_HostileZipSlipPath_ThrowsSecurityExceptionAndCleansUp()
    {
        string maliciousZip = Path.Combine(_testDir, "malicious_slip.zip");
        using (var fs = File.Create(maliciousZip))
        using (var archive = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("../../../hostile_escape.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("Hostile content escaping destination");
        }

        var extractOp = new FileOperation("tool:extract", "Extract", "", OperationCategory.Archive, true);
        var extractJob = new ConversionJob([maliciousZip], extractOp, _testDir);

        var ex = await Assert.ThrowsAsync<System.Security.SecurityException>(() =>
            _engine.ExecuteAsync(extractJob, new Progress<JobProgress>(), CancellationToken.None));

        Assert.Contains("Zip Slip", ex.Message);

        // Verify no escaped file was created outside _testDir
        string parentEscaped = Path.Combine(Path.GetDirectoryName(_testDir)!, "hostile_escape.txt");
        Assert.False(File.Exists(parentEscaped));
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
