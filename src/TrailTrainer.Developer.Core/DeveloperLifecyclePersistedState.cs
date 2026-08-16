namespace TrailTrainer.Developer.Core;

public sealed record DeveloperLifecyclePersistedState
{
    public DeveloperLifecyclePersistedState(
        string taskId,
        string? taskFilePath,
        DeveloperLifecycleResumeContext resumeContext,
        DateTimeOffset savedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        if (taskFilePath is not null && string.IsNullOrWhiteSpace(taskFilePath))
        {
            throw new ArgumentException("Task file path must not be whitespace.", nameof(taskFilePath));
        }

        ArgumentNullException.ThrowIfNull(resumeContext);
        if (savedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Saved timestamp must use UTC offset zero.", nameof(savedAtUtc));
        }

        TaskId = taskId;
        TaskFilePath = taskFilePath;
        ResumeContext = resumeContext;
        SavedAtUtc = savedAtUtc;
    }

    private DeveloperLifecyclePersistedState(
        string taskId,
        string taskFilePath,
        PersistedDeveloperLifecycleStartRequest recoveryStartRequest,
        DateTimeOffset savedAtUtc,
        bool recovery)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskFilePath);
        ArgumentNullException.ThrowIfNull(recoveryStartRequest);
        if (!string.Equals(taskId, recoveryStartRequest.TaskId, StringComparison.Ordinal) ||
            !string.Equals(Path.GetFullPath(taskFilePath), Path.GetFullPath(recoveryStartRequest.TaskFilePath!), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Recovery start request identity must match the persisted lifecycle state.");
        }
        if (savedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Saved timestamp must use UTC offset zero.", nameof(savedAtUtc));
        }

        TaskId = taskId;
        TaskFilePath = taskFilePath;
        ResumeContext = null!;
        RecoveryStartRequest = recoveryStartRequest;
        SavedAtUtc = savedAtUtc;
    }

    public static DeveloperLifecyclePersistedState CreateRecovery(
        string taskId,
        string taskFilePath,
        PersistedDeveloperLifecycleStartRequest recoveryStartRequest,
        DateTimeOffset savedAtUtc) =>
        new(taskId, taskFilePath, recoveryStartRequest, savedAtUtc, true);

    public string TaskId { get; }
    public string? TaskFilePath { get; }
    public DeveloperLifecycleResumeContext ResumeContext { get; }
    public PersistedDeveloperLifecycleStartRequest? RecoveryStartRequest { get; }
    public DateTimeOffset SavedAtUtc { get; }
}
