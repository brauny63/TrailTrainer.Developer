namespace TrailTrainer.Developer.Core;

public interface IRepeatedDelayedAutomaticResumeExecutor
{
    Task<RepeatedDelayedAutomaticResumeResult> ExecuteAsync(
        RepeatedDelayedAutomaticResumeRequest request,
        CancellationToken cancellationToken = default);
}
