namespace TrailTrainer.Developer.Core;

public sealed record AutomaticResumeSchedulingDecision
{
    public AutomaticResumeSchedulingDecision(
        AutomaticResumeSchedulingDecisionState state,
        AutomaticResumeBatchRunResult batchRun,
        bool shouldRunAgain,
        bool immediate)
    {
        ArgumentNullException.ThrowIfNull(batchRun);
        var valid = state switch
        {
            AutomaticResumeSchedulingDecisionState.Finished =>
                (batchRun.State is AutomaticResumeBatchRunState.Empty or AutomaticResumeBatchRunState.Completed) &&
                !shouldRunAgain && !immediate,
            AutomaticResumeSchedulingDecisionState.ContinueImmediately =>
                batchRun.State == AutomaticResumeBatchRunState.LimitReached &&
                shouldRunAgain && immediate,
            AutomaticResumeSchedulingDecisionState.ResumeLater =>
                batchRun.State == AutomaticResumeBatchRunState.Pending &&
                shouldRunAgain && !immediate,
            AutomaticResumeSchedulingDecisionState.StopFailed =>
                batchRun.State == AutomaticResumeBatchRunState.Failed &&
                !shouldRunAgain && !immediate,
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };
        if (!valid)
        {
            throw new ArgumentException(
                "The scheduling decision state, batch run, and flags are inconsistent.");
        }

        State = state;
        BatchRun = batchRun;
        ShouldRunAgain = shouldRunAgain;
        Immediate = immediate;
    }

    public AutomaticResumeSchedulingDecisionState State { get; }
    public AutomaticResumeBatchRunResult BatchRun { get; }
    public bool ShouldRunAgain { get; }
    public bool Immediate { get; }
}
