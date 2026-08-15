namespace TrailTrainer.Developer.Core;

public interface IAutomaticResumeBatchStep
{
    Task<AutomaticResumeBatchStepResult> ExecuteAsync(
        AutomaticResumeBatchStepRequest request,
        CancellationToken cancellationToken = default);
}
