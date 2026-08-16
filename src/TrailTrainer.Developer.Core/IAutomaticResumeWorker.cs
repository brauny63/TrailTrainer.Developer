namespace TrailTrainer.Developer.Core;

public interface IAutomaticResumeWorker
{
    Task<AutomaticResumeWorkerResult> RunAsync(
        AutomaticResumeWorkerRequest request,
        CancellationToken cancellationToken = default);
}
