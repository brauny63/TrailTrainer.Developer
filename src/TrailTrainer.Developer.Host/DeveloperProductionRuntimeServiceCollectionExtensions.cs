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
        services.Configure<AutomaticResumeHostOptions>(
            configuration.GetSection(AutomaticResumeHostOptions.SectionName));
        services.AddOptions<InitialTaskIntakeOptions>()
            .Bind(configuration.GetSection(InitialTaskIntakeOptions.SectionName))
            .Validate(
                static options => !options.Enabled ||
                    (!string.IsNullOrWhiteSpace(options.RepositoryPath) &&
                     !string.IsNullOrWhiteSpace(options.RepositoryName) &&
                     !string.IsNullOrWhiteSpace(options.GitHubOwner) &&
                     !string.IsNullOrWhiteSpace(options.BaseBranch) &&
                     !string.IsNullOrWhiteSpace(options.RemoteName)),
                "Enabled initial task intake requires repository path, repository name, GitHub owner, base branch, and remote name.")
            .ValidateOnStart();

        services.AddLogging();

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
        services.TryAddSingleton<IDeveloperTaskDiscovery, DeveloperTaskDiscovery>();
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
        services.TryAddSingleton<IAutomaticResumeCandidateSelector, AutomaticResumeCandidateSelector>();
        services.TryAddSingleton<IInitialDeveloperTaskIntake, InitialDeveloperTaskIntake>();
        services.TryAddSingleton<
            IInitialDeveloperTaskIntakeRequestProvider,
            ConfiguredInitialDeveloperTaskIntakeRequestProvider>();

        return services;
    }

    private static string GetStorageDirectory(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<IOptions<DeveloperProductionRuntimeOptions>>()
            .Value
            .LifecycleStateStorageDirectory;
}
