using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class HostedAutomaticResumeService : IHostedService
{
    private readonly IAutomaticResumeWorker worker;
    private readonly IAutomaticResumeWorkerRequestProvider requestProvider;
    private readonly IInitialDeveloperTaskIntake? intake;
    private readonly IInitialDeveloperTaskIntakeRequestProvider? intakeRequestProvider;
    private readonly ILogger<HostedAutomaticResumeService>? logger;

    public HostedAutomaticResumeService(
        IAutomaticResumeWorker worker,
        IAutomaticResumeWorkerRequestProvider requestProvider,
        IInitialDeveloperTaskIntake? intake = null,
        IInitialDeveloperTaskIntakeRequestProvider? intakeRequestProvider = null,
        ILogger<HostedAutomaticResumeService>? logger = null)
    {
        this.worker = worker ?? throw new ArgumentNullException(nameof(worker));
        this.requestProvider = requestProvider ?? throw new ArgumentNullException(nameof(requestProvider));
        if ((intake is null) != (intakeRequestProvider is null))
        {
            throw new ArgumentException(
                "Initial intake and its request provider must either both be supplied or both be absent.");
        }

        this.intake = intake;
        this.intakeRequestProvider = intakeRequestProvider;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (intake is not null)
        {
            var intakeRequest = intakeRequestProvider!.GetRequest()
                ?? throw new InvalidOperationException("The initial intake request provider returned null.");
            try
            {
                await intake.ExecuteAsync(intakeRequest, cancellationToken);
            }
            catch (DeveloperTaskExecutionException exception)
            {
                logger?.LogError(
                    exception,
                    "Initial Developer Task execution failed in a controlled manner for repository {RepositoryPath}.",
                    intakeRequest.RepositoryPath);
                return;
            }
        }

        var request = requestProvider.GetRequest()
            ?? throw new InvalidOperationException("The automatic resume worker request provider returned null.");
        try
        {
            await worker.RunAsync(request, cancellationToken);
        }
        catch (DeveloperTaskExecutionException exception)
        {
            logger?.LogError(
                exception,
                "Automatic Developer Task resume failed in a controlled manner.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
