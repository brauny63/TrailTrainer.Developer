using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class AutomaticResumeSchedulingDecisionService : IAutomaticResumeSchedulingDecision
{
    public AutomaticResumeSchedulingDecision Decide(AutomaticResumeBatchRunResult batchRun)
    {
        ArgumentNullException.ThrowIfNull(batchRun);
        return batchRun.State switch
        {
            AutomaticResumeBatchRunState.Empty or AutomaticResumeBatchRunState.Completed =>
                new AutomaticResumeSchedulingDecision(
                    AutomaticResumeSchedulingDecisionState.Finished,
                    batchRun,
                    false,
                    false),
            AutomaticResumeBatchRunState.Pending =>
                new AutomaticResumeSchedulingDecision(
                    AutomaticResumeSchedulingDecisionState.ResumeLater,
                    batchRun,
                    true,
                    false),
            AutomaticResumeBatchRunState.Failed =>
                new AutomaticResumeSchedulingDecision(
                    AutomaticResumeSchedulingDecisionState.StopFailed,
                    batchRun,
                    false,
                    false),
            AutomaticResumeBatchRunState.LimitReached =>
                new AutomaticResumeSchedulingDecision(
                    AutomaticResumeSchedulingDecisionState.ContinueImmediately,
                    batchRun,
                    true,
                    true),
            _ => throw new ArgumentOutOfRangeException(nameof(batchRun), "Unsupported batch run state.")
        };
    }
}
