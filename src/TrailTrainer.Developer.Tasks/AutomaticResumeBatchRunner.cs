using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class AutomaticResumeBatchRunner : IAutomaticResumeBatchRunner
{
    private readonly IAutomaticResumeBatchStep batchStep;

    public AutomaticResumeBatchRunner(IAutomaticResumeBatchStep batchStep)
    {
        this.batchStep = batchStep ?? throw new ArgumentNullException(nameof(batchStep));
    }

    public async Task<AutomaticResumeBatchRunResult> RunAsync(
        AutomaticResumeBatchRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var steps = new List<AutomaticResumeBatchStepResult>(request.MaximumSteps);
        while (steps.Count < request.MaximumSteps)
        {
            var step = await batchStep.ExecuteAsync(request.StepRequest, cancellationToken);
            steps.Add(step);

            if (step.State == AutomaticResumeBatchStepState.Empty)
            {
                return new AutomaticResumeBatchRunResult(
                    AutomaticResumeBatchRunState.Empty, steps, step.MoreWork);
            }

            if (step.State == AutomaticResumeBatchStepState.Pending)
            {
                return new AutomaticResumeBatchRunResult(
                    AutomaticResumeBatchRunState.Pending, steps, step.MoreWork);
            }

            if (step.State == AutomaticResumeBatchStepState.Failed)
            {
                return new AutomaticResumeBatchRunResult(
                    AutomaticResumeBatchRunState.Failed, steps, step.MoreWork);
            }

            if (step.State != AutomaticResumeBatchStepState.Completed)
            {
                throw new InvalidOperationException("The automatic resume batch step returned an unsupported state.");
            }

            if (!step.MoreWork)
            {
                return new AutomaticResumeBatchRunResult(
                    AutomaticResumeBatchRunState.Completed, steps, false);
            }
        }

        return new AutomaticResumeBatchRunResult(
            AutomaticResumeBatchRunState.LimitReached, steps, true);
    }
}
