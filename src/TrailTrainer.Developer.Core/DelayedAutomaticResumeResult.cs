namespace TrailTrainer.Developer.Core;

public sealed record DelayedAutomaticResumeResult
{
    public DelayedAutomaticResumeResult(
        DelayedAutomaticResumeState state,
        AutomaticResumeRunResult initialRun,
        AutomaticResumeRunResult? delayedRun,
        bool delayExecuted)
    {
        ArgumentNullException.ThrowIfNull(initialRun);
        var valid = state switch
        {
            DelayedAutomaticResumeState.Finished =>
                initialRun.State == AutomaticResumeRunState.Finished &&
                delayedRun is null && !delayExecuted,
            DelayedAutomaticResumeState.Failed =>
                initialRun.State == AutomaticResumeRunState.Failed &&
                delayedRun is null && !delayExecuted,
            DelayedAutomaticResumeState.ImmediateWorkRemaining =>
                initialRun.State == AutomaticResumeRunState.LimitReached &&
                delayedRun is null && !delayExecuted,
            DelayedAutomaticResumeState.ResumeLater =>
                initialRun.State == AutomaticResumeRunState.ResumeLater &&
                delayedRun is null && !delayExecuted,
            DelayedAutomaticResumeState.DelayedRunCompleted =>
                initialRun.State == AutomaticResumeRunState.ResumeLater &&
                delayedRun is not null && delayExecuted,
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };
        if (!valid)
        {
            throw new ArgumentException("The delayed execution state, run results, and delay flag are inconsistent.");
        }

        State = state;
        InitialRun = initialRun;
        DelayedRun = delayedRun;
        DelayExecuted = delayExecuted;
    }

    public DelayedAutomaticResumeState State { get; }
    public AutomaticResumeRunResult InitialRun { get; }
    public AutomaticResumeRunResult? DelayedRun { get; }
    public bool DelayExecuted { get; }
}
