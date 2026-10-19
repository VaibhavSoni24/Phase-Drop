using System.IO;
using System.Text.Json;
using PhaseDrop.Core.Models;
using PhaseDrop.Engines.Documents;

namespace PhaseDrop.Engines.Tests;

public class DocumentConversionEngineTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _sampleCsv;
    private readonly DocumentConversionEngine _engine = new();

    public DocumentConversionEngineTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"PhaseDrop_DocTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _sampleCsv = Path.Combine(_testDir, "data.csv");
        File.WriteAllText(_sampleCsv, "Name,Age,Role\nAlice,30,Developer\nBob,25,Designer\n");
    }

    [Fact]
    public async Task Convert_CsvToJson_ProducesValidJsonArray()
    {
        var op = new FileOperation("convert:json", "JSON", ".json", OperationCategory.Document);
        var job = new ConversionJob([_sampleCsv], op, _testDir);

        var result = await _engine.ExecuteAsync(job, new Progress<JobProgress>(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.OutputFiles);
        string jsonFile = result.OutputFiles[0];
        Assert.True(File.Exists(jsonFile));

        string jsonContent = File.ReadAllText(jsonFile);
        using var doc = JsonDocument.Parse(jsonContent);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(2, doc.RootElement.GetArrayLength());
        Assert.Equal("Alice", doc.RootElement[0].GetProperty("Name").GetString());
        Assert.Equal("30", doc.RootElement[0].GetProperty("Age").GetString());
    }

    [Fact]
    public async Task Convert_CsvToHtml_ContainsTableAndHeaders()
    {
        var op = new FileOperation("convert:html", "HTML", ".html", OperationCategory.Document);
        var job = new ConversionJob([_sampleCsv], op, _testDir);

        var result = await _engine.ExecuteAsync(job, new Progress<JobProgress>(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.OutputFiles);
        string htmlFile = result.OutputFiles[0];
        Assert.True(File.Exists(htmlFile));

        string html = File.ReadAllText(htmlFile);
        Assert.Contains("<table>", html);
        Assert.Contains("<th>Name</th>", html);
        Assert.Contains("<td>Alice</td>", html);
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
