using Microsoft.Extensions.Hosting;
using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class HostedAutomaticResumeService : IHostedService
{
    private readonly IAutomaticResumeWorker worker;
    private readonly IAutomaticResumeWorkerRequestProvider requestProvider;

    public HostedAutomaticResumeService(
        IAutomaticResumeWorker worker,
        IAutomaticResumeWorkerRequestProvider requestProvider)
    {
        this.worker = worker ?? throw new ArgumentNullException(nameof(worker));
        this.requestProvider = requestProvider ?? throw new ArgumentNullException(nameof(requestProvider));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var request = requestProvider.GetRequest()
            ?? throw new InvalidOperationException("The automatic resume worker request provider returned null.");
        await worker.RunAsync(request, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
