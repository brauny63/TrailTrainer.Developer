using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Host;

public sealed class ProductionRuntimeHealthValidator : IProductionRuntimeHealthValidator
{
    private readonly IConfiguration configuration;

    public ProductionRuntimeHealthValidator(IConfiguration configuration)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var services = new ServiceCollection();
        services.AddDeveloperProductionRuntime(configuration);
        services.AddAutomaticResumePipeline();
        services.AddSingleton<
            IAutomaticResumeWorkerRequestProvider,
            ConfiguredAutomaticResumeWorkerRequestProvider>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        _ = provider.GetRequiredService<IDeveloperLifecycleStateDiscovery>();
        _ = provider.GetRequiredService<IPersistedDeveloperLifecycle>();
        _ = provider.GetRequiredService<IAutomaticResumeWorker>();
        _ = provider.GetRequiredService<IAutomaticResumeWorkerRequestProvider>();
        _ = provider.GetRequiredService<IInitialDeveloperTaskIntake>();
        _ = provider.GetRequiredService<IInitialDeveloperTaskIntakeRequestProvider>().GetRequest();
        _ = provider.GetRequiredService<ICodexTaskExecutor>();
        _ = provider.GetRequiredService<ICodexExecutionStateStore>();
        return Task.CompletedTask;
    }
}
