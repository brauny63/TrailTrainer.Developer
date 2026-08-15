using System.Collections.ObjectModel;

namespace TrailTrainer.Developer.Core;

public sealed record AutomaticResumeRunResult
{
    public AutomaticResumeRunResult(
        AutomaticResumeRunState state,
        IEnumerable<AutomaticResumeBatchRunResult> batchRuns,
        IEnumerable<AutomaticResumeSchedulingDecision> decisions,
        bool shouldRunAgain,
        bool immediate)
    {
        ArgumentNullException.ThrowIfNull(batchRuns);
        ArgumentNullException.ThrowIfNull(decisions);
        var batchSnapshot = batchRuns.ToArray();
        var decisionSnapshot = decisions.ToArray();
        if (batchSnapshot.Length == 0 || batchSnapshot.Any(batchRun => batchRun is null))
        {
            throw new ArgumentException("A successful orchestration requires at least one non-null batch run.", nameof(batchRuns));
        }

        if (decisionSnapshot.Length == 0 || decisionSnapshot.Any(decision => decision is null))
        {
            throw new ArgumentException("A successful orchestration requires at least one non-null decision.", nameof(decisions));
        }

        if (batchSnapshot.Length != decisionSnapshot.Length)
        {
            throw new ArgumentException("Batch run and decision counts must match.");
        }

        for (var index = 0; index < batchSnapshot.Length; index++)
        {
            if (!ReferenceEquals(batchSnapshot[index], decisionSnapshot[index].BatchRun))
            {
                throw new ArgumentException("Each decision must reference its corresponding exact batch result.");
            }
        }

        if (decisionSnapshot[..^1].Any(decision =>
                decision.State != AutomaticResumeSchedulingDecisionState.ContinueImmediately))
        {
            throw new ArgumentException("Every non-terminal decision must be ContinueImmediately.", nameof(decisions));
        }

        var finalDecision = decisionSnapshot[^1];
        if (shouldRunAgain != finalDecision.ShouldRunAgain || immediate != finalDecision.Immediate)
        {
            throw new ArgumentException("Run flags must match the final scheduling decision.");
        }

        var valid = state switch
        {
            AutomaticResumeRunState.Finished =>
                finalDecision.State == AutomaticResumeSchedulingDecisionState.Finished &&
                !shouldRunAgain && !immediate,
            AutomaticResumeRunState.ResumeLater =>
                finalDecision.State == AutomaticResumeSchedulingDecisionState.ResumeLater &&
                shouldRunAgain && !immediate,
            AutomaticResumeRunState.Failed =>
                finalDecision.State == AutomaticResumeSchedulingDecisionState.StopFailed &&
                !shouldRunAgain && !immediate,
            AutomaticResumeRunState.LimitReached =>
                finalDecision.State == AutomaticResumeSchedulingDecisionState.ContinueImmediately &&
                shouldRunAgain && immediate,
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };
        if (!valid)
        {
            throw new ArgumentException("The final scheduling decision is inconsistent with the run state.");
        }

        State = state;
        BatchRuns = new ReadOnlyCollection<AutomaticResumeBatchRunResult>(batchSnapshot);
        Decisions = new ReadOnlyCollection<AutomaticResumeSchedulingDecision>(decisionSnapshot);
        ShouldRunAgain = shouldRunAgain;
        Immediate = immediate;
    }

    public AutomaticResumeRunState State { get; }
    public IReadOnlyList<AutomaticResumeBatchRunResult> BatchRuns { get; }
    public IReadOnlyList<AutomaticResumeSchedulingDecision> Decisions { get; }
    public bool ShouldRunAgain { get; }
    public bool Immediate { get; }
}
