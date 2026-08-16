using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class RepeatedDelayedAutomaticResumeExecutor : IRepeatedDelayedAutomaticResumeExecutor
{
    private readonly IAutomaticResumeRunOrchestrator orchestrator;
    private readonly IAsyncDelay delay;

    public RepeatedDelayedAutomaticResumeExecutor(
        IAutomaticResumeRunOrchestrator orchestrator,
        IAsyncDelay delay)
    {
        this.orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        this.delay = delay ?? throw new ArgumentNullException(nameof(delay));
    }

    public async Task<RepeatedDelayedAutomaticResumeResult> ExecuteAsync(
        RepeatedDelayedAutomaticResumeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var runs = new List<AutomaticResumeRunResult>(request.MaximumRuns);
        var delayCount = 0;
        while (runs.Count < request.MaximumRuns)
        {
            var run = await orchestrator.RunAsync(request.RunRequest, cancellationToken);
            runs.Add(run);
            switch (run.State)
            {
                case AutomaticResumeRunState.Finished:
                    return new RepeatedDelayedAutomaticResumeResult(
                        RepeatedDelayedAutomaticResumeState.Finished,
                        runs,
                        delayCount,
                        false,
                        false);
                case AutomaticResumeRunState.Failed:
                    return new RepeatedDelayedAutomaticResumeResult(
                        RepeatedDelayedAutomaticResumeState.Failed,
                        runs,
                        delayCount,
                        false,
                        false);
                case AutomaticResumeRunState.LimitReached:
                    return new RepeatedDelayedAutomaticResumeResult(
                        RepeatedDelayedAutomaticResumeState.ImmediateWorkRemaining,
                        runs,
                        delayCount,
                        true,
                        true);
                case AutomaticResumeRunState.ResumeLater when runs.Count == request.MaximumRuns:
                    return new RepeatedDelayedAutomaticResumeResult(
                        RepeatedDelayedAutomaticResumeState.RunLimitReached,
                        runs,
                        delayCount,
                        true,
                        false);
                case AutomaticResumeRunState.ResumeLater:
                    await delay.DelayAsync(request.ResumeDelay, cancellationToken);
                    delayCount++;
                    break;
                default:
                    throw new InvalidOperationException("The automatic resume run returned an unsupported state.");
            }
        }

        throw new InvalidOperationException("The repeated delayed execution reached an inconsistent state.");
    }
}
