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

    [Fact]
    public async Task Convert_Rfc4180CsvWithEmbeddedNewlinesAndEscapedQuotes_ParsesCorrectly()
    {
        string complexCsvPath = Path.Combine(_testDir, "complex.csv");
        // Row 1: Alice has an embedded newline in Notes, Bob has an escaped quote and comma in Role
        string csvContent = "Name,Role,Notes\n" +
                            "\"Alice\",\"Dev\",\"Line 1\nLine 2\"\n" +
                            "\"Bob\",\"\"\"Senior\"\", Lead\",\"Single line notes\"\n";
        File.WriteAllText(complexCsvPath, csvContent);

        var op = new FileOperation("convert:json", "JSON", ".json", OperationCategory.Document);
        var job = new ConversionJob([complexCsvPath], op, _testDir);

        var result = await _engine.ExecuteAsync(job, new Progress<JobProgress>(), CancellationToken.None);

        Assert.True(result.Success);
        string json = File.ReadAllText(result.OutputFiles[0]);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(2, root.GetArrayLength());
        // Verify Alice's notes preserved embedded newline
        Assert.Equal("Line 1\nLine 2", root[0].GetProperty("Notes").GetString());
        // Verify Bob's role has unescaped quotes and comma
        Assert.Equal("\"Senior\", Lead", root[1].GetProperty("Role").GetString());
    }

    [Fact]
    public async Task Convert_JsonToCsv_PreservesDeterministicHeaderOrder()
    {
        string jsonPath = Path.Combine(_testDir, "deterministic.json");
        string jsonContent = "[{\"Zebra\": 1, \"Alpha\": 2, \"Beta\": 3}, {\"Alpha\": 4, \"Beta\": 5, \"Gamma\": 6}]";
        File.WriteAllText(jsonPath, jsonContent);

        var op = new FileOperation("convert:csv", "CSV", ".csv", OperationCategory.Document);
        var job = new ConversionJob([jsonPath], op, _testDir);

        var result = await _engine.ExecuteAsync(job, new Progress<JobProgress>(), CancellationToken.None);

        Assert.True(result.Success);
        string csv = File.ReadAllText(result.OutputFiles[0]);
        string[] lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        
        // Headers must appear in encounter order: Zebra, Alpha, Beta, Gamma
        Assert.Equal("Zebra,Alpha,Beta,Gamma", lines[0]);
    }

    [Fact]
    public async Task Convert_CsvToHtml_EscapesUntrustedHtmlEntities()
    {
        string maliciousCsvPath = Path.Combine(_testDir, "malicious.csv");
        File.WriteAllText(maliciousCsvPath, "Tag,Payload\nDanger,\"<script>alert('xss')</script> & <b>bold</b>\"\n");

        var op = new FileOperation("convert:html", "HTML", ".html", OperationCategory.Document);
        var job = new ConversionJob([maliciousCsvPath], op, _testDir);

        var result = await _engine.ExecuteAsync(job, new Progress<JobProgress>(), CancellationToken.None);

        Assert.True(result.Success);
        string html = File.ReadAllText(result.OutputFiles[0]);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt; &amp; &lt;b&gt;bold&lt;/b&gt;", html);
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
