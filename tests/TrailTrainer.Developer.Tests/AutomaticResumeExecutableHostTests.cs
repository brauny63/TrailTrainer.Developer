using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Host;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class AutomaticResumeExecutableHostTests
{
    [Fact]
    public void HostOptions_ExposeOnlyRequiredRequestValues()
    {
        var names = typeof(AutomaticResumeHostOptions)
            .GetProperties()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal([
            nameof(AutomaticResumeHostOptions.DeleteRemoteBranch),
            nameof(AutomaticResumeHostOptions.MaximumBatchRuns),
            nameof(AutomaticResumeHostOptions.MaximumRuns),
            nameof(AutomaticResumeHostOptions.MaximumSteps),
            nameof(AutomaticResumeHostOptions.MergeCommitMessage),
            nameof(AutomaticResumeHostOptions.MergeCommitTitle),
            nameof(AutomaticResumeHostOptions.MergeMethod),
            nameof(AutomaticResumeHostOptions.ResumeDelay)
        ], names);
        Assert.Equal("AutomaticResume", AutomaticResumeHostOptions.SectionName);
    }

    [Fact]
    public void RequestProvider_ConstructsValidGraphAndPreservesConfiguredValues()
    {
        var configured = new AutomaticResumeHostOptions
        {
            MergeMethod = PullRequestMergeMethod.Rebase,
            MergeCommitTitle = "Exact title",
            MergeCommitMessage = "Exact message",
            DeleteRemoteBranch = true,
            MaximumSteps = 2,
            MaximumBatchRuns = 3,
            ResumeDelay = TimeSpan.FromMinutes(17),
            MaximumRuns = 4
        };

        var request = new ConfiguredAutomaticResumeWorkerRequestProvider(
            Options.Create(configured)).GetRequest();

        Assert.NotNull(request);
        Assert.Equal(4, request.ExecutionRequest.MaximumRuns);
        Assert.Equal(TimeSpan.FromMinutes(17), request.ExecutionRequest.ResumeDelay);
        Assert.Equal(3, request.ExecutionRequest.RunRequest.MaximumBatchRuns);
        var batch = request.ExecutionRequest.RunRequest.BatchRunRequest;
        Assert.Equal(2, batch.MaximumSteps);
        Assert.Equal(PullRequestMergeMethod.Rebase, batch.StepRequest.MergeMethod);
        Assert.Equal("Exact title", batch.StepRequest.MergeCommitTitle);
        Assert.Equal("Exact message", batch.StepRequest.MergeCommitMessage);
        Assert.True(batch.StepRequest.DeleteRemoteBranch);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(-1, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, -1, 1)]
    [InlineData(1, 1, 0)]
    [InlineData(1, 1, -1)]
    public void RequestProvider_InvalidRunBoundsFailClearly(
        int maximumSteps,
        int maximumBatchRuns,
        int maximumRuns)
    {
        var options = ValidOptions();
        options.MaximumSteps = maximumSteps;
        options.MaximumBatchRuns = maximumBatchRuns;
        options.MaximumRuns = maximumRuns;
        var provider = new ConfiguredAutomaticResumeWorkerRequestProvider(Options.Create(options));

        Assert.Throws<ArgumentOutOfRangeException>(() => provider.GetRequest());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RequestProvider_InvalidDelayFailsClearly(long ticks)
    {
        var options = ValidOptions();
        options.ResumeDelay = TimeSpan.FromTicks(ticks);
        var provider = new ConfiguredAutomaticResumeWorkerRequestProvider(Options.Create(options));

        Assert.Throws<ArgumentOutOfRangeException>(() => provider.GetRequest());
    }

    [Fact]
    public void RequestProvider_ImplementsBoundaryAndHasOnlyOptionsDependency()
    {
        var type = typeof(ConfiguredAutomaticResumeWorkerRequestProvider);
        var parameters = Assert.Single(type.GetConstructors()).GetParameters();

        Assert.Contains(typeof(IAutomaticResumeWorkerRequestProvider), type.GetInterfaces());
        Assert.Single(parameters);
        Assert.Equal(typeof(IOptions<AutomaticResumeHostOptions>), parameters[0].ParameterType);
    }

    [Fact]
    public void HostComposition_WithRuntimeTestDoublesResolvesPipelineAndSingleHostedAdapter()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Services.AddAutomaticResumePipeline();
        builder.Services.Configure<AutomaticResumeHostOptions>(_ => { });
        builder.Services.AddSingleton<
            IAutomaticResumeWorkerRequestProvider,
            ConfiguredAutomaticResumeWorkerRequestProvider>();
        builder.Services.AddSingleton<IDeveloperLifecycleStateDiscovery, FakeDiscovery>();
        builder.Services.AddSingleton<IPersistedDeveloperLifecycle, FakePersistedLifecycle>();
        using var host = builder.Build();

        Assert.IsType<AutomaticResumeWorker>(host.Services.GetRequiredService<IAutomaticResumeWorker>());
        Assert.IsType<ConfiguredAutomaticResumeWorkerRequestProvider>(
            host.Services.GetRequiredService<IAutomaticResumeWorkerRequestProvider>());
        Assert.IsType<HostedAutomaticResumeService>(
            Assert.Single(host.Services.GetServices<IHostedService>()));
    }

    [Fact]
    public async Task HostStartup_InvokesHostedAdapterAndAwaitsWorker()
    {
        var completion = new TaskCompletionSource<AutomaticResumeWorkerResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new FakeWorker { PendingResult = completion.Task };
        using var host = BuildHostWithWorker(worker);

        var startup = host.StartAsync();

        Assert.False(startup.IsCompleted);
        Assert.Equal(1, worker.CallCount);
        completion.SetResult(WorkerResult());
        await startup;
        Assert.Equal(1, worker.CallCount);
        await host.StopAsync();
    }

    [Fact]
    public async Task HostStartup_WorkerFailureSurfacesWithoutRetry()
    {
        var expected = new IOException("startup failed");
        var worker = new FakeWorker { Exception = expected };
        using var host = BuildHostWithWorker(worker);

        var exception = await Assert.ThrowsAsync<IOException>(() => host.StartAsync());

        Assert.Same(expected, exception);
        Assert.Equal(1, worker.CallCount);
    }

    [Fact]
    public async Task HostStartup_CancellationIsNotConvertedToSuccess()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var worker = new FakeWorker { HonorCancellation = true };
        using var host = BuildHostWithWorker(worker);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => host.StartAsync(source.Token));

        Assert.Equal(0, worker.CallCount);
    }

    private static IHost BuildHostWithWorker(FakeWorker worker)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IAutomaticResumeWorker>(worker);
        builder.Services.AddAutomaticResumePipeline();
        builder.Services.AddSingleton<IAutomaticResumeWorkerRequestProvider>(
            new FixedRequestProvider(WorkerRequest()));
        return builder.Build();
    }

    private static AutomaticResumeHostOptions ValidOptions() => new()
    {
        MaximumSteps = 1,
        MaximumBatchRuns = 1,
        ResumeDelay = TimeSpan.FromMinutes(1),
        MaximumRuns = 1
    };

    private static AutomaticResumeWorkerRequest WorkerRequest() =>
        new ConfiguredAutomaticResumeWorkerRequestProvider(
            Options.Create(ValidOptions())).GetRequest();

    private static AutomaticResumeWorkerResult WorkerResult() =>
        new(RepeatedResult());

    private static RepeatedDelayedAutomaticResumeResult RepeatedResult()
    {
        var persisted = new DeveloperLifecyclePersistedState(
            "DEV-0035",
            null,
            new DeveloperLifecycleResumeContext(
                "repository",
                new GitHubRepositoryIdentity("owner", "repository"),
                35,
                "feature/host",
                "main",
                "origin"),
            new DateTimeOffset(2026, 8, 16, 12, 0, 0, TimeSpan.Zero));
        var status = new PullRequestStatusGateResult(35, "head", PullRequestGateState.Successful, []);
        var lifecycle = new DeveloperLifecycleResumeResult(
            DeveloperLifecycleState.Completed,
            persisted.ResumeContext,
            status,
            new PullRequestGatedMergeResult(
                status,
                new PullRequestMergeResult(35, true, "merge", PullRequestMergeMethod.Squash)),
            new PostMergeCleanupResult("repository", "main", "feature/host", true, true));
        var candidate = new AutomaticResumeCandidateResult(
            AutomaticResumeCandidateState.Found,
            persisted,
            new PersistedLifecycleResumeTarget(persisted.TaskId, persisted));
        var persistedResume = new PersistedDeveloperLifecycleResumeResult(
            PersistedDeveloperLifecycleResumeState.Completed,
            persisted.TaskId,
            persisted,
            lifecycle);
        var automaticResume = new AutomaticPersistedLifecycleResumeResult(
            AutomaticPersistedLifecycleResumeState.Completed,
            candidate,
            persistedResume);
        var step = new AutomaticResumeBatchStepResult(
            AutomaticResumeBatchStepState.Completed,
            automaticResume,
            false);
        var batch = new AutomaticResumeBatchRunResult(
            AutomaticResumeBatchRunState.Completed,
            [step],
            false);
        var decision = new AutomaticResumeSchedulingDecision(
            AutomaticResumeSchedulingDecisionState.Finished,
            batch,
            false,
            false);
        var run = new AutomaticResumeRunResult(
            AutomaticResumeRunState.Finished,
            [batch],
            [decision],
            false,
            false);
        return new RepeatedDelayedAutomaticResumeResult(
            RepeatedDelayedAutomaticResumeState.Finished,
            [run],
            0,
            false,
            false);
    }

    private sealed class FixedRequestProvider(
        AutomaticResumeWorkerRequest request) : IAutomaticResumeWorkerRequestProvider
    {
        public AutomaticResumeWorkerRequest GetRequest() => request;
    }

    private sealed class FakeWorker : IAutomaticResumeWorker
    {
        public Task<AutomaticResumeWorkerResult>? PendingResult { get; init; }
        public Exception? Exception { get; init; }
        public bool HonorCancellation { get; init; }
        public int CallCount { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<AutomaticResumeWorkerResult> RunAsync(
            AutomaticResumeWorkerRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Token = cancellationToken;
            if (HonorCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (Exception is not null)
            {
                return Task.FromException<AutomaticResumeWorkerResult>(Exception);
            }

            return PendingResult ?? Task.FromResult(WorkerResult());
        }
    }

    private sealed class FakeDiscovery : IDeveloperLifecycleStateDiscovery
    {
        public Task<IReadOnlyList<DeveloperLifecyclePersistedState>> ListAsync(
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Host composition must not execute discovery during resolution.");
    }

    private sealed class FakePersistedLifecycle : IPersistedDeveloperLifecycle
    {
        public Task<PersistedDeveloperLifecycleStartResult> StartAsync(
            PersistedDeveloperLifecycleStartRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Host composition must not start lifecycle work during resolution.");

        public Task<PersistedDeveloperLifecycleResumeResult> ResumeAsync(
            PersistedDeveloperLifecycleResumeRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Host composition must not resume lifecycle work during resolution.");
    }
}
