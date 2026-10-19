using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using PhaseDrop.Core.Models;

namespace PhaseDrop.Core.Queue;

public sealed class JobQueue : IDisposable
{
    private readonly Channel<ConversionJob> _channel;
    private readonly IEnumerable<IConversionEngine> _engines;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly CancellationTokenSource _queueCts = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeJobCts = new();
    private readonly ConcurrentDictionary<string, ConversionJob> _activeJobs = new();

    public event Action<ConversionJob>? JobEnqueued;
    public event Action<ConversionJob>? JobStarted;
    public event Action<ConversionJob, JobProgress>? JobProgressChanged;
    public event Action<ConversionJob, JobResult>? JobCompleted;

    public int MaxConcurrency { get; }

    public JobQueue(IEnumerable<IConversionEngine> engines, int maxConcurrency = 2)
    {
        _engines = engines ?? throw new ArgumentNullException(nameof(engines));
        MaxConcurrency = Math.Max(1, maxConcurrency);
        _concurrencySemaphore = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);

        var options = new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<ConversionJob>(options);

        // Start worker loop
        for (int i = 0; i < MaxConcurrency; i++)
        {
            Task.Run(ProcessQueueAsync);
        }
    }

    public bool Enqueue(ConversionJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (_queueCts.IsCancellationRequested)
            return false;

        job.Status = JobStatus.Queued;
        bool written = _channel.Writer.TryWrite(job);
        if (written)
        {
            JobEnqueued?.Invoke(job);
        }
        return written;
    }

    public bool CancelJob(string jobId)
    {
        if (_activeJobCts.TryGetValue(jobId, out var cts))
        {
            cts.Cancel();
            return true;
        }

        if (_activeJobs.TryGetValue(jobId, out var job))
        {
            job.Status = JobStatus.Cancelled;
            JobCompleted?.Invoke(job, new JobResult(false, [], "Operation cancelled by user"));
            return true;
        }

        return false;
    }

    private async Task ProcessQueueAsync()
    {
        var reader = _channel.Reader;
        while (await reader.WaitToReadAsync(_queueCts.Token))
        {
            while (reader.TryRead(out var job))
            {
                if (job.Status == JobStatus.Cancelled)
                    continue;

                await _concurrencySemaphore.WaitAsync(_queueCts.Token);
                _ = Task.Run(async () =>
                {
                    var jobCts = CancellationTokenSource.CreateLinkedTokenSource(_queueCts.Token);
                    _activeJobCts[job.Id] = jobCts;
                    _activeJobs[job.Id] = job;

                    job.Status = JobStatus.Running;
                    JobStarted?.Invoke(job);

                    var sw = Stopwatch.StartNew();
                    JobResult result;

                    try
                    {
                        var engine = _engines.FirstOrDefault(e => e.CanHandle(job.Operation));
                        if (engine == null)
                        {
                            result = new JobResult(false, [], $"No compatible engine registered for operation {job.Operation.Id}");
                        }
                        else
                        {
                            var progress = new Progress<JobProgress>(p =>
                            {
                                JobProgressChanged?.Invoke(job, p);
                            });

                            result = await engine.ExecuteAsync(job, progress, jobCts.Token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        result = new JobResult(false, [], "Operation cancelled by user", sw.Elapsed);
                        job.Status = JobStatus.Cancelled;
                    }
                    catch (Exception ex)
                    {
                        result = new JobResult(false, [], ex.Message, sw.Elapsed);
                        job.Status = JobStatus.Failed;
                    }
                    finally
                    {
                        sw.Stop();
                        _activeJobCts.TryRemove(job.Id, out _);
                        _activeJobs.TryRemove(job.Id, out _);
                        _concurrencySemaphore.Release();
                        jobCts.Dispose();
                    }

                    if (job.Status != JobStatus.Cancelled)
                    {
                        job.Status = result.Success ? JobStatus.Completed : JobStatus.Failed;
                    }

                    JobCompleted?.Invoke(job, result);
                }, _queueCts.Token);
            }
        }
    }

    public void Dispose()
    {
        _queueCts.Cancel();
        _queueCts.Dispose();
        _concurrencySemaphore.Dispose();
    }
}
