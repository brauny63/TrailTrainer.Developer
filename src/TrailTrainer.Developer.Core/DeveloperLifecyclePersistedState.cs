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

    public string TaskId { get; }
    public string? TaskFilePath { get; }
    public DeveloperLifecycleResumeContext ResumeContext { get; }
    public DateTimeOffset SavedAtUtc { get; }
}
