namespace TrailTrainer.Developer.Core;

public interface IAutomaticResumeRunOrchestrator
{
    Task<AutomaticResumeRunResult> RunAsync(
        AutomaticResumeRunRequest request,
        CancellationToken cancellationToken = default);
}
