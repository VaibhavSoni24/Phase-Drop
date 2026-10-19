using PhaseDrop.Core.Models;

namespace PhaseDrop.Core.Queue;

public interface IConversionEngine
{
    bool CanHandle(FileOperation operation);
    Task<JobResult> ExecuteAsync(ConversionJob job, IProgress<JobProgress> progress, CancellationToken cancellationToken);
}
