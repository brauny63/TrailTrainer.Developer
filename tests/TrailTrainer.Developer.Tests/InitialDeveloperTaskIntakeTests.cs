using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Host;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class InitialDeveloperTaskIntakeTests
{
    [Fact]
    public async Task Disabled_DoesNotInspectRepositoryDiscoverOrStart()
    {
        using var fixture = new Fixture();

        var result = await fixture.Intake.ExecuteAsync(fixture.Request(enabled: false));

        Assert.Equal(InitialDeveloperTaskIntakeState.Disabled, result.State);
        Assert.Equal(0, fixture.Candidates.Calls);
        Assert.Equal(0, fixture.Status.Calls);
        Assert.Equal(0, fixture.Discovery.Calls);
        Assert.Equal(0, fixture.Lifecycle.Calls);
    }

    [Fact]
    public async Task MissingRepository_FailsDeterministicallyWithoutStart()
    {
        using var fixture = new Fixture();
        var missing = Path.Combine(fixture.Root, "missing");

        var exception = await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            fixture.Intake.ExecuteAsync(fixture.Request(repositoryPath: missing)));

        Assert.Contains(Path.GetFullPath(missing), exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, fixture.Status.Calls);
        Assert.Equal(0, fixture.Discovery.Calls);
        Assert.Equal(0, fixture.Lifecycle.Calls);
    }

    [Fact]
    public async Task NonGitRepository_FailsDeterministicallyWithoutDiscoveryOrStart()
    {
        using var fixture = new Fixture();
        fixture.Status.Result = GitRepositoryStatus.NotRepository;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Intake.ExecuteAsync(fixture.Request()));

        Assert.Contains("not inside a Git working tree", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, fixture.Status.Calls);
        Assert.Equal(0, fixture.Discovery.Calls);
        Assert.Equal(0, fixture.Lifecycle.Calls);
    }

    [Fact]
    public async Task DirtyRepository_BlocksWithoutDiscoveryOrStart()
    {
        using var fixture = new Fixture();
        fixture.Status.Result = new GitRepositoryStatus(true, fixture.Root, "main", true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Intake.ExecuteAsync(fixture.Request()));

        Assert.Contains("uncommitted changes", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, fixture.Discovery.Calls);
        Assert.Equal(0, fixture.Lifecycle.Calls);
    }

    [Fact]
    public async Task MultipleTasks_SelectsLowestExistingDiscoveryOrderAndStartsOnlyOne()
    {
        using var fixture = new Fixture();
        var first = fixture.Descriptor(47);
        fixture.Discovery.Tasks = [first, fixture.Descriptor(48), fixture.Descriptor(49)];

        var result = await fixture.Intake.ExecuteAsync(fixture.Request());

        Assert.Equal(InitialDeveloperTaskIntakeState.Started, result.State);
        Assert.Same(first, result.SelectedTask);
        Assert.Equal(1, fixture.Discovery.Calls);
        Assert.Equal(1, fixture.Lifecycle.Calls);
        var request = Assert.IsType<PersistedDeveloperLifecycleStartRequest>(fixture.Lifecycle.Request);
        Assert.Equal("DEV-0047", request.TaskId);
        Assert.Equal(first.FilePath, request.TaskFilePath);
        Assert.Equal(first.FilePath, request.DeveloperTaskFilePath);
        Assert.Equal(fixture.Root, request.RepositoryDirectoryPath);
        Assert.Equal("Target.Repository", request.ExpectedRepositoryName);
        Assert.Equal("Implement DEV-0047", request.CommitMessage);
        Assert.Equal("origin", request.GitRemoteName);
        Assert.True(request.SetUpstream);
        Assert.Equal("owner", request.GitHubRepository.Owner);
        Assert.Equal("Target.Repository", request.GitHubRepository.Repository);
        Assert.Equal("main", request.PullRequestBaseBranch);
        Assert.Equal(PullRequestMergeMethod.Squash, request.MergeMethod);
    }

    [Fact]
    public async Task ExistingResumableLifecycle_HasPriorityAndCannotBeOverwritten()
    {
        using var fixture = new Fixture();
        fixture.Candidates.Result = FoundCandidate(fixture.Root);
        fixture.Discovery.Tasks = [fixture.Descriptor(47)];

        var result = await fixture.Intake.ExecuteAsync(fixture.Request());

        Assert.Equal(InitialDeveloperTaskIntakeState.ResumableWorkFound, result.State);
        Assert.Equal(0, fixture.Status.Calls);
        Assert.Equal(0, fixture.Discovery.Calls);
        Assert.Equal(0, fixture.Lifecycle.Calls);
    }

    [Fact]
    public async Task MalformedTaskFailure_IsSurfacedWithoutRetryOrSecondTask()
    {
        using var fixture = new Fixture();
        fixture.Discovery.Tasks = [fixture.Descriptor(47), fixture.Descriptor(48)];
        var expected = new InvalidDataException("malformed task");
        fixture.Lifecycle.Exception = expected;

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            fixture.Intake.ExecuteAsync(fixture.Request()));

        Assert.Same(expected, exception);
        Assert.Equal(1, fixture.Lifecycle.Calls);
        Assert.Equal("DEV-0047", fixture.Lifecycle.Request!.TaskId);
    }

    [Fact]
    public async Task NoTasks_ReturnsNoTaskFoundWithoutStart()
    {
        using var fixture = new Fixture();

        var result = await fixture.Intake.ExecuteAsync(fixture.Request());

        Assert.Equal(InitialDeveloperTaskIntakeState.NoTaskFound, result.State);
        Assert.Equal(1, fixture.Discovery.Calls);
        Assert.Equal(0, fixture.Lifecycle.Calls);
    }

    [Fact]
    public void ProductionDi_DefaultDisabledAndValidEnabledConfigurationsResolveWithoutEffects()
    {
        using var fixture = new Fixture();
        foreach (var enabled in new[] { false, true })
        {
            var values = new Dictionary<string, string?>
            {
                [$"{DeveloperProductionRuntimeOptions.SectionName}:LifecycleStateStorageDirectory"] =
                    Path.Combine(fixture.Root, enabled ? "enabled-state" : "disabled-state")
            };
            if (enabled)
            {
                values[$"{InitialTaskIntakeOptions.SectionName}:Enabled"] = "true";
                values[$"{InitialTaskIntakeOptions.SectionName}:RepositoryPath"] = fixture.Root;
                values[$"{InitialTaskIntakeOptions.SectionName}:RepositoryName"] = "Target.Repository";
                values[$"{InitialTaskIntakeOptions.SectionName}:GitHubOwner"] = "owner";
            }

            var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            var services = new ServiceCollection();
            services.AddDeveloperProductionRuntime(configuration);
            services.AddAutomaticResumePipeline();
            services.AddSingleton<IAutomaticResumeWorkerRequestProvider,
                ConfiguredAutomaticResumeWorkerRequestProvider>();
            using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

            var intake = provider.GetRequiredService<IInitialDeveloperTaskIntake>();
            var request = provider.GetRequiredService<IInitialDeveloperTaskIntakeRequestProvider>().GetRequest();
            Assert.IsType<InitialDeveloperTaskIntake>(intake);
            Assert.Equal(enabled, request.Enabled);
            Assert.False(Directory.Exists(Path.Combine(fixture.Root, enabled ? "enabled-state" : "disabled-state")));
        }
    }

    private static AutomaticResumeCandidateResult FoundCandidate(string repositoryPath)
    {
        var state = new DeveloperLifecyclePersistedState(
            "DEV-0046",
            "task.md",
            new DeveloperLifecycleResumeContext(
                repositoryPath,
                new GitHubRepositoryIdentity("owner", "repo"),
                1,
                "feature/dev-0046",
                "main",
                "origin"),
            DateTimeOffset.UnixEpoch);
        return new AutomaticResumeCandidateResult(
            AutomaticResumeCandidateState.Found,
            state,
            new PersistedLifecycleResumeTarget(state.TaskId, state));
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Root = Path.Combine(Path.GetTempPath(), $"trailtrainer-dev-0047-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            Candidates = new FakeCandidateSelector();
            Discovery = new FakeDiscovery();
            Status = new FakeStatusProvider
            {
                Result = new GitRepositoryStatus(true, Root, "main", false)
            };
            Lifecycle = new FakePersistedLifecycle();
            Intake = new InitialDeveloperTaskIntake(
                Candidates,
                Discovery,
                Status,
                Lifecycle,
                NullLogger<InitialDeveloperTaskIntake>.Instance);
        }

        public string Root { get; }
        public FakeCandidateSelector Candidates { get; }
        public FakeDiscovery Discovery { get; }
        public FakeStatusProvider Status { get; }
        public FakePersistedLifecycle Lifecycle { get; }
        public InitialDeveloperTaskIntake Intake { get; }

        public InitialDeveloperTaskIntakeRequest Request(
            bool enabled = true,
            string? repositoryPath = null) => new(
                enabled,
                repositoryPath ?? Root,
                "Target.Repository",
                "owner",
                "main",
                "origin",
                PullRequestMergeMethod.Squash,
                null,
                null,
                false);

        public DeveloperTaskDescriptor Descriptor(int number)
        {
            var id = new DeveloperTaskId(number);
            var fileName = $"{id}-Task.md";
            return new DeveloperTaskDescriptor(id, Path.Combine(Root, "docs", "developer-tasks", fileName), fileName);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed class FakeCandidateSelector : IAutomaticResumeCandidateSelector
    {
        public AutomaticResumeCandidateResult Result { get; set; } =
            new(AutomaticResumeCandidateState.NotFound);
        public int Calls { get; private set; }

        public Task<AutomaticResumeCandidateResult> SelectAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeDiscovery : IDeveloperTaskDiscovery
    {
        public IReadOnlyList<DeveloperTaskDescriptor> Tasks { get; set; } = [];
        public int Calls { get; private set; }

        public Task<IReadOnlyList<DeveloperTaskDescriptor>> DiscoverAsync(
            string repositoryRootPath,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Tasks);
        }
    }

    private sealed class FakeStatusProvider : IGitRepositoryStatusProvider
    {
        public GitRepositoryStatus Result { get; set; } = GitRepositoryStatus.NotRepository;
        public int Calls { get; private set; }

        public Task<GitRepositoryStatus> GetStatusAsync(
            string directoryPath,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakePersistedLifecycle : IPersistedDeveloperLifecycle
    {
        public int Calls { get; private set; }
        public PersistedDeveloperLifecycleStartRequest? Request { get; private set; }
        public Exception? Exception { get; set; }

        public Task<PersistedDeveloperLifecycleStartResult> StartAsync(
            PersistedDeveloperLifecycleStartRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            Request = request;
            if (Exception is not null)
            {
                return Task.FromException<PersistedDeveloperLifecycleStartResult>(Exception);
            }

            return Task.FromResult(
                (PersistedDeveloperLifecycleStartResult)RuntimeHelpers.GetUninitializedObject(
                    typeof(PersistedDeveloperLifecycleStartResult)));
        }

        public Task<PersistedDeveloperLifecycleResumeResult> ResumeAsync(
            PersistedDeveloperLifecycleResumeRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
