using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class AutomaticResumeServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAutomaticResumePipeline_NullCollectionRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AutomaticResumeServiceCollectionExtensions.AddAutomaticResumePipeline(null!));
    }

    [Fact]
    public void AddAutomaticResumePipeline_ReturnsSameCollectionWithoutExecutingWorkflow()
    {
        var services = new ServiceCollection();
        var boundaries = AddRuntimeBoundaries(services);

        var returned = services.AddAutomaticResumePipeline();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        _ = provider.GetRequiredService<IHostedService>();

        Assert.Same(services, returned);
        Assert.Equal(0, boundaries.Discovery.CallCount);
        Assert.Equal(0, boundaries.Lifecycle.CallCount);
        Assert.Equal(0, boundaries.RequestProvider.CallCount);
    }

    [Fact]
    public void AddAutomaticResumePipeline_RegistersRequiredInterfaceToConcreteSingletons()
    {
        var services = new ServiceCollection();

        services.AddAutomaticResumePipeline();

        AssertSingleton<IAsyncDelay, SystemAsyncDelay>(services);
        AssertSingleton<IAutomaticResumeCandidateSelector, AutomaticResumeCandidateSelector>(services);
        AssertSingleton<IAutomaticPersistedLifecycleResumer, AutomaticPersistedLifecycleResumer>(services);
        AssertSingleton<IAutomaticResumeBatchStep, AutomaticResumeBatchStep>(services);
        AssertSingleton<IAutomaticResumeBatchRunner, AutomaticResumeBatchRunner>(services);
        AssertSingleton<IAutomaticResumeSchedulingDecision, AutomaticResumeSchedulingDecisionService>(services);
        AssertSingleton<IAutomaticResumeRunOrchestrator, AutomaticResumeRunOrchestrator>(services);
        AssertSingleton<IRepeatedDelayedAutomaticResumeExecutor, RepeatedDelayedAutomaticResumeExecutor>(services);
        AssertSingleton<IAutomaticResumeWorker, AutomaticResumeWorker>(services);
        AssertSingleton<IHostedService, HostedAutomaticResumeService>(services);
    }

    [Fact]
    public void AddAutomaticResumePipeline_DoesNotInventRuntimeBoundaryImplementations()
    {
        var services = new ServiceCollection();

        services.AddAutomaticResumePipeline();

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IAutomaticResumeWorkerRequestProvider));
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IDeveloperLifecycleStateDiscovery));
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IPersistedDeveloperLifecycle));
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType.Namespace is "TrailTrainer.Developer.Git" or "TrailTrainer.Developer.GitHub");
    }

    [Fact]
    public void AddAutomaticResumePipeline_MissingRuntimeBoundariesSurfaceDuringValidation()
    {
        var services = new ServiceCollection();
        services.AddAutomaticResumePipeline();

        Assert.Throws<AggregateException>(() => services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        }));
    }

    [Fact]
    public void AddAutomaticResumePipeline_TestRuntimeBoundariesAllowCompleteGraphResolution()
    {
        var services = new ServiceCollection();
        AddRuntimeBoundaries(services);
        services.AddAutomaticResumePipeline();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.IsType<SystemAsyncDelay>(provider.GetRequiredService<IAsyncDelay>());
        Assert.IsType<AutomaticResumeBatchStep>(provider.GetRequiredService<IAutomaticResumeBatchStep>());
        Assert.IsType<AutomaticResumeBatchRunner>(provider.GetRequiredService<IAutomaticResumeBatchRunner>());
        Assert.IsType<AutomaticResumeSchedulingDecisionService>(
            provider.GetRequiredService<IAutomaticResumeSchedulingDecision>());
        Assert.IsType<AutomaticResumeRunOrchestrator>(
            provider.GetRequiredService<IAutomaticResumeRunOrchestrator>());
        Assert.IsType<RepeatedDelayedAutomaticResumeExecutor>(
            provider.GetRequiredService<IRepeatedDelayedAutomaticResumeExecutor>());
        Assert.IsType<AutomaticResumeWorker>(provider.GetRequiredService<IAutomaticResumeWorker>());
        Assert.IsType<HostedAutomaticResumeService>(Assert.Single(provider.GetServices<IHostedService>()));
    }

    [Fact]
    public void AddAutomaticResumePipeline_DuplicateCallsRemainIdempotent()
    {
        var services = new ServiceCollection();
        AddRuntimeBoundaries(services);

        services.AddAutomaticResumePipeline();
        services.AddAutomaticResumePipeline();
        using var provider = services.BuildServiceProvider();

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IAsyncDelay));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IAutomaticResumeWorker));
        Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(HostedAutomaticResumeService));
        Assert.Single(provider.GetServices<IHostedService>().OfType<HostedAutomaticResumeService>());
    }

    [Fact]
    public void AddAutomaticResumePipeline_SingletonResolutionsReturnSameInstances()
    {
        var services = new ServiceCollection();
        AddRuntimeBoundaries(services);
        services.AddAutomaticResumePipeline();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });

        Assert.Same(
            provider.GetRequiredService<IAsyncDelay>(),
            provider.GetRequiredService<IAsyncDelay>());
        Assert.Same(
            provider.GetRequiredService<IAutomaticResumeBatchRunner>(),
            provider.GetRequiredService<IAutomaticResumeBatchRunner>());
        Assert.Same(
            provider.GetRequiredService<IAutomaticResumeWorker>(),
            provider.GetRequiredService<IAutomaticResumeWorker>());
        Assert.Same(
            provider.GetRequiredService<IHostedService>(),
            provider.GetRequiredService<IHostedService>());
    }

    private static void AssertSingleton<TService, TImplementation>(IServiceCollection services)
    {
        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(TService));
        Assert.Equal(typeof(TImplementation), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    private static RuntimeBoundaries AddRuntimeBoundaries(IServiceCollection services)
    {
        var discovery = new FakeDiscovery();
        var lifecycle = new FakePersistedLifecycle();
        var requestProvider = new FakeRequestProvider();
        services.AddSingleton<IDeveloperLifecycleStateDiscovery>(discovery);
        services.AddSingleton<IPersistedDeveloperLifecycle>(lifecycle);
        services.AddSingleton<IAutomaticResumeWorkerRequestProvider>(requestProvider);
        return new RuntimeBoundaries(discovery, lifecycle, requestProvider);
    }

    private sealed record RuntimeBoundaries(
        FakeDiscovery Discovery,
        FakePersistedLifecycle Lifecycle,
        FakeRequestProvider RequestProvider);

    private sealed class FakeDiscovery : IDeveloperLifecycleStateDiscovery
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<DeveloperLifecyclePersistedState>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new InvalidOperationException("Registration must not execute discovery.");
        }
    }

    private sealed class FakePersistedLifecycle : IPersistedDeveloperLifecycle
    {
        public int CallCount { get; private set; }

        public Task<PersistedDeveloperLifecycleStartResult> StartAsync(
            PersistedDeveloperLifecycleStartRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new InvalidOperationException("Registration must not start a lifecycle.");
        }

        public Task<PersistedDeveloperLifecycleResumeResult> ResumeAsync(
            PersistedDeveloperLifecycleResumeRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new InvalidOperationException("Registration must not resume a lifecycle.");
        }
    }

    private sealed class FakeRequestProvider : IAutomaticResumeWorkerRequestProvider
    {
        public int CallCount { get; private set; }

        public AutomaticResumeWorkerRequest GetRequest()
        {
            CallCount++;
            throw new InvalidOperationException("Registration must not request runtime configuration.");
        }
    }
}
