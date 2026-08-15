namespace TrailTrainer.Developer.Core;

public interface IAutomaticResumeBatchRunner
{
    Task<AutomaticResumeBatchRunResult> RunAsync(
        AutomaticResumeBatchRunRequest request,
        CancellationToken cancellationToken = default);
}
