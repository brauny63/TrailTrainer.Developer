using System.Collections.ObjectModel;

namespace TrailTrainer.Developer.Core;

public sealed record RepeatedDelayedAutomaticResumeResult
{
    public RepeatedDelayedAutomaticResumeResult(
        RepeatedDelayedAutomaticResumeState state,
        IEnumerable<AutomaticResumeRunResult> runs,
        int delayCount,
        bool shouldRunAgain,
        bool immediate)
    {
        ArgumentNullException.ThrowIfNull(runs);
        var snapshot = runs.ToArray();
        if (snapshot.Length == 0 || snapshot.Any(run => run is null))
        {
            throw new ArgumentException("A repeated delayed execution requires at least one non-null run.", nameof(runs));
        }

        if (delayCount < 0 || delayCount > snapshot.Length - 1)
        {
            throw new ArgumentOutOfRangeException(nameof(delayCount));
        }

        if (snapshot[..^1].Any(run => run.State != AutomaticResumeRunState.ResumeLater))
        {
            throw new ArgumentException("Every non-terminal run must request ResumeLater.", nameof(runs));
        }

        var finalRun = snapshot[^1];
        var valid = state switch
        {
            RepeatedDelayedAutomaticResumeState.Finished =>
                finalRun.State == AutomaticResumeRunState.Finished &&
                delayCount == snapshot.Length - 1 && !shouldRunAgain && !immediate,
            RepeatedDelayedAutomaticResumeState.Failed =>
                finalRun.State == AutomaticResumeRunState.Failed &&
                delayCount == snapshot.Length - 1 && !shouldRunAgain && !immediate,
            RepeatedDelayedAutomaticResumeState.ImmediateWorkRemaining =>
                finalRun.State == AutomaticResumeRunState.LimitReached &&
                delayCount == snapshot.Length - 1 && shouldRunAgain && immediate,
            RepeatedDelayedAutomaticResumeState.DelayedWorkRemaining =>
                finalRun.State == AutomaticResumeRunState.ResumeLater &&
                shouldRunAgain && !immediate,
            RepeatedDelayedAutomaticResumeState.RunLimitReached =>
                finalRun.State == AutomaticResumeRunState.ResumeLater &&
                delayCount == snapshot.Length - 1 && shouldRunAgain && !immediate,
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };
        if (!valid)
        {
            throw new ArgumentException("The repeated delayed execution state, runs, delay count, and flags are inconsistent.");
        }

        State = state;
        Runs = new ReadOnlyCollection<AutomaticResumeRunResult>(snapshot);
        DelayCount = delayCount;
        ShouldRunAgain = shouldRunAgain;
        Immediate = immediate;
    }

    public RepeatedDelayedAutomaticResumeState State { get; }
    public IReadOnlyList<AutomaticResumeRunResult> Runs { get; }
    public int DelayCount { get; }
    public bool ShouldRunAgain { get; }
    public bool Immediate { get; }
}
