using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class AutomaticResumeWorker : IAutomaticResumeWorker
{
    private readonly IRepeatedDelayedAutomaticResumeExecutor executor;

    public AutomaticResumeWorker(IRepeatedDelayedAutomaticResumeExecutor executor)
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public async Task<AutomaticResumeWorkerResult> RunAsync(
        AutomaticResumeWorkerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await executor.ExecuteAsync(request.ExecutionRequest, cancellationToken);
        return new AutomaticResumeWorkerResult(result);
    }
}
