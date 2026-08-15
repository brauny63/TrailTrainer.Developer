namespace TrailTrainer.Developer.Core;

public interface IDelayedAutomaticResumeExecutor
{
    Task<DelayedAutomaticResumeResult> ExecuteAsync(
        DelayedAutomaticResumeRequest request,
        CancellationToken cancellationToken = default);
}
