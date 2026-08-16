using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Git;
using TrailTrainer.Developer.GitHub;
using TrailTrainer.Developer.Persistence;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Host;

public static class DeveloperProductionRuntimeServiceCollectionExtensions
{
    public static IServiceCollection AddDeveloperProductionRuntime(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<DeveloperProductionRuntimeOptions>()
            .Bind(configuration.GetSection(DeveloperProductionRuntimeOptions.SectionName))
            .Validate(
                static options => !string.IsNullOrWhiteSpace(options.LifecycleStateStorageDirectory),
                $"Configuration value '{DeveloperProductionRuntimeOptions.SectionName}:" +
                $"{nameof(DeveloperProductionRuntimeOptions.LifecycleStateStorageDirectory)}' is required.")
            .ValidateOnStart();

        services.TryAddSingleton<HttpClient>();

        services.TryAddSingleton<IGitRepositoryStatusProvider, LocalGitRepositoryStatusProvider>();
        services.TryAddSingleton<IGitBranchCreator, LocalGitBranchCreator>();
        services.TryAddSingleton<IGitStager, LocalGitStager>();
        services.TryAddSingleton<IGitCommitter, LocalGitCommitter>();
        services.TryAddSingleton<IGitPusher, LocalGitPusher>();
        services.TryAddSingleton<IPostMergeCleaner, LocalPostMergeCleaner>();

        services.TryAddSingleton<IPullRequestService, GitHubPullRequestService>();
        services.TryAddSingleton<IPullRequestStatusGate, GitHubPullRequestStatusGate>();
        services.TryAddSingleton<IPullRequestMerger, GitHubPullRequestMerger>();

        services.TryAddSingleton<IDeveloperTaskParser, DeveloperTaskParser>();
        services.TryAddSingleton<IDeveloperReviewParser, DeveloperReviewParser>();
        services.TryAddSingleton<IDeveloperReviewValidator, DeveloperReviewValidator>();
        services.TryAddSingleton<IDeveloperTaskStarter, DeveloperTaskStarter>();
        services.TryAddSingleton<IDeveloperTaskCompleter, DeveloperTaskCompleter>();
        services.TryAddSingleton<IDeveloperTaskGatedCompleter, DeveloperTaskGatedCompleter>();
        services.TryAddSingleton<IDeveloperTaskWorkflow, DeveloperTaskWorkflow>();
        services.TryAddSingleton<IPullRequestMergeGate, PullRequestMergeGate>();
        services.TryAddSingleton<IDeveloperLifecycleOrchestrator, DeveloperLifecycleOrchestrator>();
        services.TryAddSingleton<IDeveloperLifecycleResumer, DeveloperLifecycleResumer>();
        services.TryAddSingleton<IUtcClock, SystemUtcClock>();

        services.TryAddSingleton<IDeveloperLifecycleStateStore>(serviceProvider =>
            new LocalJsonDeveloperLifecycleStateStore(GetStorageDirectory(serviceProvider)));
        services.TryAddSingleton<IDeveloperLifecycleStateDiscovery>(serviceProvider =>
            new LocalJsonDeveloperLifecycleStateDiscovery(GetStorageDirectory(serviceProvider)));
        services.TryAddSingleton<IPersistedDeveloperLifecycle, PersistedDeveloperLifecycle>();

        return services;
    }

    private static string GetStorageDirectory(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<IOptions<DeveloperProductionRuntimeOptions>>()
            .Value
            .LifecycleStateStorageDirectory;
}
