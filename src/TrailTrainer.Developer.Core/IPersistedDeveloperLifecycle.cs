namespace TrailTrainer.Developer.Core;

public interface IPersistedDeveloperLifecycle
{
    Task<PersistedDeveloperLifecycleStartResult> StartAsync(
        PersistedDeveloperLifecycleStartRequest request,
        CancellationToken cancellationToken = default);

    Task<PersistedDeveloperLifecycleResumeResult> ResumeAsync(
        PersistedDeveloperLifecycleResumeRequest request,
        CancellationToken cancellationToken = default);
}
