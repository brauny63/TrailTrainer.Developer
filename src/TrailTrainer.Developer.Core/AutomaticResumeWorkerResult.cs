namespace TrailTrainer.Developer.Core;

public sealed record AutomaticResumeWorkerResult
{
    public AutomaticResumeWorkerResult(RepeatedDelayedAutomaticResumeResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);
        ExecutionResult = executionResult;
    }

    public RepeatedDelayedAutomaticResumeResult ExecutionResult { get; }

    public bool ResumableWorkFound => ExecutionResult.Runs
        .SelectMany(run => run.BatchRuns)
        .SelectMany(batchRun => batchRun.Steps)
        .Any(step => step.Resume.State != AutomaticPersistedLifecycleResumeState.NotFound);
}
