namespace PhaseDrop.Core.Models;

public enum OperationCategory
{
    Image,
    Video,
    Audio,
    Pdf,
    Archive,
    Document,
    Unknown
}

public enum JobStatus
{
    Queued,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled
}

public sealed record FileOperation(
    string Id,
    string DisplayName,
    string TargetExtension,
    OperationCategory Category,
    bool IsTool = false,
    string IconKey = "",
    string Description = "",
    bool IsLossless = false,
    bool RequiresParameters = false);
