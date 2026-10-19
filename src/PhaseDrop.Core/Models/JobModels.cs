using System.Text.Json.Nodes;

namespace PhaseDrop.Core.Models;

public sealed class ConversionJob
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..12];
    public IReadOnlyList<string> SourcePaths { get; init; }
    public FileOperation Operation { get; init; }
    public string? OutputDirectory { get; init; }
    public JsonObject? Parameters { get; init; }
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public JobStatus Status { get; set; } = JobStatus.Queued;

    public ConversionJob(IReadOnlyList<string> sourcePaths, FileOperation operation, string? outputDirectory = null, JsonObject? parameters = null)
    {
        SourcePaths = sourcePaths ?? throw new ArgumentNullException(nameof(sourcePaths));
        Operation = operation ?? throw new ArgumentNullException(nameof(operation));
        OutputDirectory = outputDirectory;
        Parameters = parameters;
    }
}

public sealed record JobProgress(
    string StatusDescription,
    double ProgressRatio,
    int CompletedItems = 0,
    int TotalItems = 1,
    string? CurrentFileName = null);

public sealed record JobResult(
    bool Success,
    IReadOnlyList<string> OutputFiles,
    string? ErrorMessage = null,
    TimeSpan Elapsed = default);
