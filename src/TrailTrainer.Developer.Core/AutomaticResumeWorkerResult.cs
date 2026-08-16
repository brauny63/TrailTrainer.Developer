namespace TrailTrainer.Developer.Core;

public sealed record AutomaticResumeWorkerResult
{
    public AutomaticResumeWorkerResult(RepeatedDelayedAutomaticResumeResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);
        ExecutionResult = executionResult;
    }

    public RepeatedDelayedAutomaticResumeResult ExecutionResult { get; }
}
