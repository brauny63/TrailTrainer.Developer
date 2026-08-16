using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public static class AutomaticResumeServiceCollectionExtensions
{
    public static IServiceCollection AddAutomaticResumePipeline(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IAsyncDelay, SystemAsyncDelay>();
        services.TryAddSingleton<IAutomaticResumeCandidateSelector, AutomaticResumeCandidateSelector>();
        services.TryAddSingleton<IAutomaticPersistedLifecycleResumer, AutomaticPersistedLifecycleResumer>();
        services.TryAddSingleton<IAutomaticResumeBatchStep, AutomaticResumeBatchStep>();
        services.TryAddSingleton<IAutomaticResumeBatchRunner, AutomaticResumeBatchRunner>();
        services.TryAddSingleton<IAutomaticResumeSchedulingDecision, AutomaticResumeSchedulingDecisionService>();
        services.TryAddSingleton<IAutomaticResumeRunOrchestrator, AutomaticResumeRunOrchestrator>();
        services.TryAddSingleton<IRepeatedDelayedAutomaticResumeExecutor, RepeatedDelayedAutomaticResumeExecutor>();
        services.TryAddSingleton<IAutomaticResumeWorker, AutomaticResumeWorker>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, HostedAutomaticResumeService>());

        return services;
    }
}
