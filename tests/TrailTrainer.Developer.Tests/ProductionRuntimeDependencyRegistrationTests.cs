using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Git;
using TrailTrainer.Developer.GitHub;
using TrailTrainer.Developer.Host;
using TrailTrainer.Developer.Persistence;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class ProductionRuntimeDependencyRegistrationTests
{
    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    public void ProductionRuntime_InvalidSandboxModeFailsAtStartup(string mode)
    {
        var configuration = CreateConfiguration("state");
        configuration = new ConfigurationBuilder().AddConfiguration(configuration)
            .AddInMemoryCollection(new Dictionary<string, string?> { [$"{CodexExecutionOptions.SectionName}:SandboxMode"] = mode }).Build();
        var services = new ServiceCollection().AddDeveloperProductionRuntime(configuration);
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<CodexExecutionOptions>>().Value);
    }

    [Fact]
    public void AddDeveloperProductionRuntime_NullCollectionRejected()
    {
        var configuration = CreateConfiguration("state");

        Assert.Throws<ArgumentNullException>(() =>
            DeveloperProductionRuntimeServiceCollectionExtensions.AddDeveloperProductionRuntime(
                null!,
                configuration));
    }

    [Fact]
    public void AddDeveloperProductionRuntime_NullConfigurationRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ServiceCollection().AddDeveloperProductionRuntime(null!));
    }

    [Fact]
    public void AddDeveloperProductionRuntime_ReturnsSameCollectionAndIsIdempotent()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration("state");

        var returned = services.AddDeveloperProductionRuntime(configuration);
        services.AddDeveloperProductionRuntime(configuration);

        Assert.Same(services, returned);
        Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IDeveloperLifecycleStateDiscovery));
        Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IPersistedDeveloperLifecycle));
        Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IDeveloperLifecycleStateStore));
    }

    [Fact]
    public void ProductionRuntime_ResolvesExistingLifecycleImplementations()
    {
        using var provider = CreateValidatedProvider("state");

        Assert.IsType<LocalJsonDeveloperLifecycleStateDiscovery>(
            provider.GetRequiredService<IDeveloperLifecycleStateDiscovery>());
        Assert.IsType<LocalJsonDeveloperLifecycleStateStore>(
            provider.GetRequiredService<IDeveloperLifecycleStateStore>());
        Assert.IsType<PersistedDeveloperLifecycle>(
            provider.GetRequiredService<IPersistedDeveloperLifecycle>());
    }

    [Fact]
    public void ProductionRuntime_ResolvesExistingGitAndGitHubImplementations()
    {
        using var provider = CreateValidatedProvider("state");

        Assert.IsType<LocalGitRepositoryStatusProvider>(
            provider.GetRequiredService<IGitRepositoryStatusProvider>());
        Assert.IsType<LocalPostMergeCleaner>(provider.GetRequiredService<IPostMergeCleaner>());
        Assert.IsType<GitHubPullRequestService>(provider.GetRequiredService<IPullRequestService>());
        Assert.IsType<GitHubPullRequestStatusGate>(provider.GetRequiredService<IPullRequestStatusGate>());
        Assert.IsType<GitHubPullRequestMerger>(provider.GetRequiredService<IPullRequestMerger>());
    }

    [Fact]
    public void ProductionRuntime_WithPipeline_ValidatesAndResolvesCompleteGraph()
    {
        var services = new ServiceCollection();
        services.AddDeveloperProductionRuntime(CreateConfiguration("state"));
        services.AddAutomaticResumePipeline();
        services.Configure<AutomaticResumeHostOptions>(_ => { });
        services.AddSingleton<
            IAutomaticResumeWorkerRequestProvider,
            ConfiguredAutomaticResumeWorkerRequestProvider>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.IsType<AutomaticResumeWorker>(provider.GetRequiredService<IAutomaticResumeWorker>());
        Assert.IsType<ConfiguredAutomaticResumeWorkerRequestProvider>(
            provider.GetRequiredService<IAutomaticResumeWorkerRequestProvider>());
        Assert.IsType<HostedAutomaticResumeService>(
            Assert.Single(provider.GetServices<IHostedService>()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ProductionRuntime_MissingOrInvalidStorageDirectoryFailsClearly(string? storageDirectory)
    {
        var services = new ServiceCollection();
        services.AddDeveloperProductionRuntime(CreateConfiguration(storageDirectory));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IDeveloperLifecycleStateDiscovery>());

        Assert.Contains(
            $"{DeveloperProductionRuntimeOptions.SectionName}:" +
            nameof(DeveloperProductionRuntimeOptions.LifecycleStateStorageDirectory),
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionRuntime_ValidConfigurationIsPreservedWithoutResolutionSideEffects()
    {
        var root = Path.Combine(Path.GetTempPath(), $"trailtrainer-dev-0037-{Guid.NewGuid():N}");
        var storageDirectory = Path.Combine(root, "lifecycle-state");
        try
        {
            using var provider = CreateValidatedProvider(storageDirectory);

            var options = provider.GetRequiredService<IOptions<DeveloperProductionRuntimeOptions>>().Value;
            _ = provider.GetRequiredService<IDeveloperLifecycleStateDiscovery>();
            _ = provider.GetRequiredService<IPersistedDeveloperLifecycle>();

            Assert.Equal(storageDirectory, options.LifecycleStateStorageDirectory);
            Assert.False(Directory.Exists(root));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static ServiceProvider CreateValidatedProvider(string storageDirectory)
    {
        var services = new ServiceCollection();
        services.AddDeveloperProductionRuntime(CreateConfiguration(storageDirectory));
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private static IConfiguration CreateConfiguration(string? storageDirectory)
    {
        var values = new Dictionary<string, string?>();
        values[$"{CodexExecutionOptions.SectionName}:ExecutablePath"] = "test-codex-never-run";
        values[$"{GitHubApiOptions.SectionName}:Token"] = "test-token";
        if (storageDirectory is not null)
        {
            values[$"{DeveloperProductionRuntimeOptions.SectionName}:" +
                nameof(DeveloperProductionRuntimeOptions.LifecycleStateStorageDirectory)] = storageDirectory;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
