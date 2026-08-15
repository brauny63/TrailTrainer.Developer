using System.Collections.ObjectModel;

namespace TrailTrainer.Developer.Core;

public sealed record AutomaticResumeBatchRunResult
{
    public AutomaticResumeBatchRunResult(
        AutomaticResumeBatchRunState state,
        IEnumerable<AutomaticResumeBatchStepResult> steps,
        bool moreWork)
    {
        ArgumentNullException.ThrowIfNull(steps);
        var snapshot = steps.ToArray();
        if (snapshot.Length == 0 || snapshot.Any(step => step is null))
        {
            throw new ArgumentException("A successful batch run requires at least one non-null step.", nameof(steps));
        }

        if (snapshot[..^1].Any(step =>
                step.State != AutomaticResumeBatchStepState.Completed || !step.MoreWork))
        {
            throw new ArgumentException(
                "Every non-terminal batch step must be Completed with more work.",
                nameof(steps));
        }

        var last = snapshot[^1];
        if (moreWork != last.MoreWork)
        {
            throw new ArgumentException("Run MoreWork must match the terminal batch step.", nameof(moreWork));
        }

        var valid = state switch
        {
            AutomaticResumeBatchRunState.Empty =>
                last.State == AutomaticResumeBatchStepState.Empty && !moreWork,
            AutomaticResumeBatchRunState.Completed =>
                last.State == AutomaticResumeBatchStepState.Completed && !moreWork,
            AutomaticResumeBatchRunState.Pending =>
                last.State == AutomaticResumeBatchStepState.Pending,
            AutomaticResumeBatchRunState.Failed =>
                last.State == AutomaticResumeBatchStepState.Failed,
            AutomaticResumeBatchRunState.LimitReached =>
                last.State == AutomaticResumeBatchStepState.Completed && moreWork,
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };
        if (!valid)
        {
            throw new ArgumentException("The terminal batch step is inconsistent with the run state.");
        }

        State = state;
        Steps = new ReadOnlyCollection<AutomaticResumeBatchStepResult>(snapshot);
        MoreWork = moreWork;
    }

    public AutomaticResumeBatchRunState State { get; }
    public IReadOnlyList<AutomaticResumeBatchStepResult> Steps { get; }
    public bool MoreWork { get; }
}
