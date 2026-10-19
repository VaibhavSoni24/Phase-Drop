using System.IO;
using PhaseDrop.Core.Models;
using PhaseDrop.Core.Queue;
using PhaseDrop.Engines.Documents;

namespace PhaseDrop.IntegrationTests;

public class JobQueueIntegrationTests : IDisposable
{
    private readonly string _testDir;

    public JobQueueIntegrationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"PhaseDrop_Integration_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    [Fact]
    public async Task JobQueue_ProcessesJob_AndReportsCompletion()
    {
        string sampleCsv = Path.Combine(_testDir, "test.csv");
        File.WriteAllText(sampleCsv, "ColA,ColB\n1,2\n3,4\n");

        var docEngine = new DocumentConversionEngine();
        using var queue = new JobQueue([docEngine], maxConcurrency: 2);

        var tcs = new TaskCompletionSource<JobResult>();
        queue.JobCompleted += (job, result) =>
        {
            tcs.TrySetResult(result);
        };

        var op = new FileOperation("convert:json", "JSON", ".json", OperationCategory.Document);
        var job = new ConversionJob([sampleCsv], op, _testDir);

        bool enqueued = queue.Enqueue(job);
        Assert.True(enqueued);

        var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(result.Success);
        Assert.Single(result.OutputFiles);
        Assert.True(File.Exists(result.OutputFiles[0]));
        Assert.EndsWith(".json", result.OutputFiles[0]);
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
