namespace TrailTrainer.Developer.Core;

public interface ISelectedPersistedLifecycleResumer
{
    Task<SelectedPersistedLifecycleResumeResult> ResumeAsync(
        SelectedPersistedLifecycleResumeRequest request,
        CancellationToken cancellationToken = default);
}
