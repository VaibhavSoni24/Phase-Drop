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

    [Fact]
    public async Task Extract_SiblingPrefixAttack_ThrowsSecurityException()
    {
        string maliciousZip = Path.Combine(_testDir, "malicious_sibling.zip");
        using (var fs = File.Create(maliciousZip))
        using (var archive = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create))
        {
            // Sibling prefix attack: attempts to use .. to go to a folder whose name starts with destination name
            var entry = archive.CreateEntry("../sibling_folder/evil.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("Hostile sibling escape");
        }

        var extractOp = new FileOperation("tool:extract", "Extract", "", OperationCategory.Archive, true);
        var extractJob = new ConversionJob([maliciousZip], extractOp, _testDir);

        var ex = await Assert.ThrowsAsync<System.Security.SecurityException>(() =>
            _engine.ExecuteAsync(extractJob, new Progress<JobProgress>(), CancellationToken.None));

        Assert.Contains("traversal", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Extract_RootedPath_ThrowsSecurityException()
    {
        string maliciousZip = Path.Combine(_testDir, "malicious_rooted.zip");
        using (var fs = File.Create(maliciousZip))
        using (var archive = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("/rooted/absolute.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("Hostile rooted file");
        }

        var extractOp = new FileOperation("tool:extract", "Extract", "", OperationCategory.Archive, true);
        var extractJob = new ConversionJob([maliciousZip], extractOp, _testDir);

        var ex = await Assert.ThrowsAsync<System.Security.SecurityException>(() =>
            _engine.ExecuteAsync(extractJob, new Progress<JobProgress>(), CancellationToken.None));

        Assert.Contains("rooted", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Extract_DuplicateEntries_DisambiguatesWithoutOverwrite()
    {
        string dupZip = Path.Combine(_testDir, "duplicates.zip");
        using (var fs = File.Create(dupZip))
        using (var archive = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create))
        {
            var entry1 = archive.CreateEntry("report.txt");
            using (var w1 = new StreamWriter(entry1.Open()))
                w1.Write("First copy");

            var entry2 = archive.CreateEntry("report.txt");
            using (var w2 = new StreamWriter(entry2.Open()))
                w2.Write("Second copy");
        }

        var extractOp = new FileOperation("tool:extract", "Extract", "", OperationCategory.Archive, true);
        var extractJob = new ConversionJob([dupZip], extractOp, _testDir);

        var result = await _engine.ExecuteAsync(extractJob, new Progress<JobProgress>(), CancellationToken.None);

        Assert.True(result.Success);
        string extractedFolder = result.OutputFiles[0];
        string file1 = Path.Combine(extractedFolder, "report.txt");
        string file2 = Path.Combine(extractedFolder, "report (1).txt");

        Assert.True(File.Exists(file1));
        Assert.True(File.Exists(file2));
        Assert.Equal("First copy", File.ReadAllText(file1));
        Assert.Equal("Second copy", File.ReadAllText(file2));
    }

    [Fact]
    public async Task Extract_CaseInsensitiveCollision_DisambiguatesOnWindows()
    {
        string caseZip = Path.Combine(_testDir, "case_collision.zip");
        using (var fs = File.Create(caseZip))
        using (var archive = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create))
        {
            var entry1 = archive.CreateEntry("Readme.txt");
            using (var w1 = new StreamWriter(entry1.Open()))
                w1.Write("Lowercase format");

            var entry2 = archive.CreateEntry("README.TXT");
            using (var w2 = new StreamWriter(entry2.Open()))
                w2.Write("Uppercase format");
        }

        var extractOp = new FileOperation("tool:extract", "Extract", "", OperationCategory.Archive, true);
        var extractJob = new ConversionJob([caseZip], extractOp, _testDir);

        var result = await _engine.ExecuteAsync(extractJob, new Progress<JobProgress>(), CancellationToken.None);

        Assert.True(result.Success);
        string extractedFolder = result.OutputFiles[0];
        string file1 = Path.Combine(extractedFolder, "Readme.txt");
        string file2 = Path.Combine(extractedFolder, "README (1).TXT");

        Assert.True(File.Exists(file1));
        Assert.True(File.Exists(file2));
    }

    [Fact]
    public async Task Extract_CorruptArchiveFile_ThrowsExceptionAndCleansUp()
    {
        string corruptZip = Path.Combine(_testDir, "corrupted.zip");
        File.WriteAllText(corruptZip, "This is not a valid zip archive file contents");

        var extractOp = new FileOperation("tool:extract", "Extract", "", OperationCategory.Archive, true);
        var extractJob = new ConversionJob([corruptZip], extractOp, _testDir);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            _engine.ExecuteAsync(extractJob, new Progress<JobProgress>(), CancellationToken.None));
    }

    [Fact]
    public async Task Extract_InvalidCharactersInEntry_ThrowsSecurityException()
    {
        string invalidZip = Path.Combine(_testDir, "invalid_chars.zip");
        using (var fs = File.Create(invalidZip))
        using (var archive = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("invalid*name?.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("dangerous content");
        }

        var extractOp = new FileOperation("tool:extract", "Extract", "", OperationCategory.Archive, true);
        var extractJob = new ConversionJob([invalidZip], extractOp, _testDir);

        await Assert.ThrowsAsync<System.Security.SecurityException>(() =>
            _engine.ExecuteAsync(extractJob, new Progress<JobProgress>(), CancellationToken.None));
    }

    [Fact]
    public async Task Extract_Failure_DoesNotDeletePreexistingUserFolder()
    {
        // Pre-create an existing user folder
        string preExistingFolder = Path.Combine(_testDir, "malicious_slip");
        Directory.CreateDirectory(preExistingFolder);
        string userFile = Path.Combine(preExistingFolder, "user_data.txt");
        File.WriteAllText(userFile, "Important user document");

        string slipZip = Path.Combine(_testDir, "malicious_slip.zip");
        using (var fs = File.Create(slipZip))
        using (var archive = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("../escaped.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("escape");
        }

        var extractOp = new FileOperation("tool:extract", "Extract", "", OperationCategory.Archive, true);
        var extractJob = new ConversionJob([slipZip], extractOp, _testDir);

        await Assert.ThrowsAsync<System.Security.SecurityException>(() =>
            _engine.ExecuteAsync(extractJob, new Progress<JobProgress>(), CancellationToken.None));

        // Pre-existing user folder and file must remain intact
        Assert.True(Directory.Exists(preExistingFolder));
        Assert.True(File.Exists(userFile));
        Assert.Equal("Important user document", File.ReadAllText(userFile));
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
