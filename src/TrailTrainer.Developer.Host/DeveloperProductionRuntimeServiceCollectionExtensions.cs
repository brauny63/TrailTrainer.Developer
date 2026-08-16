using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
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
        services.AddOptions<CodexExecutionOptions>()
            .Bind(configuration.GetSection(CodexExecutionOptions.SectionName))
            .Validate(static options => !string.IsNullOrWhiteSpace(options.ExecutablePath), "CodexExecution:ExecutablePath is required.")
            .Validate(static options => options.Timeout > TimeSpan.Zero, "CodexExecution:Timeout must be positive.")
            .Validate(static options => options.CompatibilityProbeTimeout > TimeSpan.Zero, "CodexExecution:CompatibilityProbeTimeout must be positive.")
            .Validate(static options => options.SandboxMode is "read-only" or "workspace-write" or "danger-full-access", "CodexExecution:SandboxMode must be read-only, workspace-write, or danger-full-access.")
            .Validate(static options => options.ApprovalPolicy is "untrusted" or "on-request" or "never", "CodexExecution:ApprovalPolicy must be untrusted, on-request, or never.")
            .Validate(static options => options.MaximumDiagnosticCharacters > 0, "CodexExecution:MaximumDiagnosticCharacters must be positive.")
            .ValidateOnStart();
        services.AddOptions<GitHubApiOptions>()
            .Bind(configuration.GetSection(GitHubApiOptions.SectionName))
            .Validate(
                static options => !string.IsNullOrWhiteSpace(options.Token),
                "GitHub:Token is required. Inject it through environment configuration or a secret store.")
            .Validate(
                static options => options.Token.All(character => !char.IsWhiteSpace(character)),
                "GitHub:Token must not contain whitespace.")
            .ValidateOnStart();

        services.AddLogging();

        services.TryAddSingleton<HttpClient>(serviceProvider =>
        {
            var token = serviceProvider.GetRequiredService<IOptions<GitHubApiOptions>>().Value.Token;
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        });
        services.TryAddSingleton<ICodexTaskExecutor, CodexCliTaskExecutor>();
        services.TryAddSingleton<ICodexCompatibilityProbe>(serviceProvider =>
            (CodexCliTaskExecutor)serviceProvider.GetRequiredService<ICodexTaskExecutor>());

        services.TryAddSingleton<IGitRepositoryStatusProvider, LocalGitRepositoryStatusProvider>();
        services.TryAddSingleton<IGitBranchCreator, LocalGitBranchCreator>();
        services.TryAddSingleton<IGitStager, LocalGitStager>();
        services.TryAddSingleton<IGitCommitter, LocalGitCommitter>();
        services.TryAddSingleton<IGitPusher, LocalGitPusher>();
        services.TryAddSingleton<IPostMergeCleaner, LocalPostMergeCleaner>();

        services.TryAddSingleton<IPullRequestService, GitHubPullRequestService>();
        services.TryAddSingleton<IGitHubRepositoryProbe>(serviceProvider =>
            (GitHubPullRequestService)serviceProvider.GetRequiredService<IPullRequestService>());
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
        services.TryAddSingleton<ICodexExecutionStateStore>(serviceProvider =>
            new LocalJsonCodexExecutionStateStore(GetStorageDirectory(serviceProvider)));
        services.TryAddSingleton<IPersistedDeveloperLifecycle, PersistedDeveloperLifecycle>();
        services.TryAddSingleton<IAutomaticResumeCandidateSelector, AutomaticResumeCandidateSelector>();
        services.TryAddSingleton<IInitialDeveloperTaskIntake, InitialDeveloperTaskIntake>();
        services.TryAddSingleton<IStrandedCodexStateRecovery, StrandedCodexStateRecovery>();
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
