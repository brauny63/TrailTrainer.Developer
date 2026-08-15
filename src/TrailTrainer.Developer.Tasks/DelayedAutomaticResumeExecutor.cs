using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class DelayedAutomaticResumeExecutor : IDelayedAutomaticResumeExecutor
{
    private readonly IAutomaticResumeRunOrchestrator orchestrator;
    private readonly IAsyncDelay delay;

    public DelayedAutomaticResumeExecutor(
        IAutomaticResumeRunOrchestrator orchestrator,
        IAsyncDelay delay)
    {
        this.orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        this.delay = delay ?? throw new ArgumentNullException(nameof(delay));
    }

    public async Task<DelayedAutomaticResumeResult> ExecuteAsync(
        DelayedAutomaticResumeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var initialRun = await orchestrator.RunAsync(request.RunRequest, cancellationToken);
        switch (initialRun.State)
        {
            case AutomaticResumeRunState.Finished:
                return new DelayedAutomaticResumeResult(
                    DelayedAutomaticResumeState.Finished, initialRun, null, false);
            case AutomaticResumeRunState.Failed:
                return new DelayedAutomaticResumeResult(
                    DelayedAutomaticResumeState.Failed, initialRun, null, false);
            case AutomaticResumeRunState.LimitReached:
                return new DelayedAutomaticResumeResult(
                    DelayedAutomaticResumeState.ImmediateWorkRemaining, initialRun, null, false);
            case AutomaticResumeRunState.ResumeLater:
                await delay.DelayAsync(request.ResumeDelay, cancellationToken);
                var delayedRun = await orchestrator.RunAsync(request.RunRequest, cancellationToken);
                return new DelayedAutomaticResumeResult(
                    DelayedAutomaticResumeState.DelayedRunCompleted,
                    initialRun,
                    delayedRun,
                    true);
            default:
                throw new InvalidOperationException("The automatic resume run returned an unsupported state.");
        }
    }
}
