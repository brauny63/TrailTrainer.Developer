namespace TrailTrainer.Developer.Core;

public sealed record AutomaticResumeWorkerRequest
{
    public AutomaticResumeWorkerRequest(RepeatedDelayedAutomaticResumeRequest executionRequest)
    {
        ArgumentNullException.ThrowIfNull(executionRequest);
        ExecutionRequest = executionRequest;
    }

    public RepeatedDelayedAutomaticResumeRequest ExecutionRequest { get; }
}
