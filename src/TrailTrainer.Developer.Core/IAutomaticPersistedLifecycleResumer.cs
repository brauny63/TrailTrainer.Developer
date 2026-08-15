namespace TrailTrainer.Developer.Core;

public interface IAutomaticPersistedLifecycleResumer
{
    Task<AutomaticPersistedLifecycleResumeResult> ResumeAsync(
        AutomaticPersistedLifecycleResumeRequest request,
        CancellationToken cancellationToken = default);
}
