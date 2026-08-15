namespace TrailTrainer.Developer.Core;

public sealed record AutomaticResumeBatchRunRequest
{
    public AutomaticResumeBatchRunRequest(
        AutomaticResumeBatchStepRequest stepRequest,
        int maximumSteps)
    {
        ArgumentNullException.ThrowIfNull(stepRequest);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSteps);
        StepRequest = stepRequest;
        MaximumSteps = maximumSteps;
    }

    public AutomaticResumeBatchStepRequest StepRequest { get; }
    public int MaximumSteps { get; }
}
