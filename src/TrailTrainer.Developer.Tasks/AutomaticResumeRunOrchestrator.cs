using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class AutomaticResumeRunOrchestrator : IAutomaticResumeRunOrchestrator
{
    private readonly IAutomaticResumeBatchRunner batchRunner;
    private readonly IAutomaticResumeSchedulingDecision schedulingDecision;

    public AutomaticResumeRunOrchestrator(
        IAutomaticResumeBatchRunner batchRunner,
        IAutomaticResumeSchedulingDecision schedulingDecision)
    {
        this.batchRunner = batchRunner ?? throw new ArgumentNullException(nameof(batchRunner));
        this.schedulingDecision = schedulingDecision ?? throw new ArgumentNullException(nameof(schedulingDecision));
    }

    public async Task<AutomaticResumeRunResult> RunAsync(
        AutomaticResumeRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var batchRuns = new List<AutomaticResumeBatchRunResult>(request.MaximumBatchRuns);
        var decisions = new List<AutomaticResumeSchedulingDecision>(request.MaximumBatchRuns);
        while (batchRuns.Count < request.MaximumBatchRuns)
        {
            var batchRun = await batchRunner.RunAsync(request.BatchRunRequest, cancellationToken);
            batchRuns.Add(batchRun);
            var decision = schedulingDecision.Decide(batchRun);
            if (!ReferenceEquals(decision.BatchRun, batchRun))
            {
                throw new InvalidOperationException(
                    "The scheduling decision did not preserve the exact batch run result.");
            }

            decisions.Add(decision);
            switch (decision.State)
            {
                case AutomaticResumeSchedulingDecisionState.Finished:
                    return new AutomaticResumeRunResult(
                        AutomaticResumeRunState.Finished,
                        batchRuns,
                        decisions,
                        decision.ShouldRunAgain,
                        decision.Immediate);
                case AutomaticResumeSchedulingDecisionState.ResumeLater:
                    return new AutomaticResumeRunResult(
                        AutomaticResumeRunState.ResumeLater,
                        batchRuns,
                        decisions,
                        decision.ShouldRunAgain,
                        decision.Immediate);
                case AutomaticResumeSchedulingDecisionState.StopFailed:
                    return new AutomaticResumeRunResult(
                        AutomaticResumeRunState.Failed,
                        batchRuns,
                        decisions,
                        decision.ShouldRunAgain,
                        decision.Immediate);
                case AutomaticResumeSchedulingDecisionState.ContinueImmediately:
                    break;
                default:
                    throw new InvalidOperationException("The scheduling decision returned an unsupported state.");
            }
        }

        var finalDecision = decisions[^1];
        return new AutomaticResumeRunResult(
            AutomaticResumeRunState.LimitReached,
            batchRuns,
            decisions,
            finalDecision.ShouldRunAgain,
            finalDecision.Immediate);
    }
}
