using Microsoft.Extensions.Configuration;
using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Host;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class DeveloperV1AcceptanceTests
{
    [Fact]
    public async Task SuccessfulV1Workflow_ComposesDiscoveryGitPullRequestMergeAndCleanupInOrder()
    {
        using var repository = new AcceptanceRepository();
        var calls = new List<string>();
        var parser = new DeveloperTaskParser();
        var descriptors = await new DeveloperTaskDiscovery().DiscoverAsync(repository.Path);
        var descriptor = Assert.Single(descriptors);
        var task = await parser.ParseAsync(descriptor.FilePath);
        Assert.Equal(new DeveloperTaskId(46), task.Id);

        var statusProvider = new SequencedStatusProvider(
            Status(repository.Path, "main", false),
            Status(repository.Path, task.ExpectedBranch, true));
        var starter = new DeveloperTaskStarter(
            parser,
            statusProvider,
            new RecordingBranchCreator(calls));
        var started = await starter.StartAsync(
            descriptor.FilePath,
            repository.Path,
            repository.Name);
        Assert.Equal(task.ExpectedBranch, started.CreatedBranch);

        var completer = new DeveloperTaskCompleter(
            parser,
            statusProvider,
            new RecordingStager(calls),
            new RecordingCommitter(calls),
            new RecordingPusher(calls));
        var gatedCompleter = new AcceptanceGatedCompleter(completer);
        var pullRequests = new RecordingPullRequestService(calls);
        var workflow = new DeveloperTaskWorkflow(parser, gatedCompleter, pullRequests);
        var statusGate = new RecordingStatusGate(calls);
        var mergeGate = new RecordingMergeGate(calls);
        var cleaner = new RecordingCleaner(calls);
        var lifecycle = new DeveloperLifecycleOrchestrator(
            workflow,
            statusGate,
            mergeGate,
            cleaner);

        var result = await lifecycle.ExecuteAsync(
            descriptor.FilePath,
            repository.Path,
            repository.Name,
            "Complete DEV-0046",
            "origin",
            true,
            new GitHubRepositoryIdentity("owner", repository.Name),
            "main",
            "Acceptance PR",
            false,
            PullRequestMergeMethod.Squash,
            "merge title",
            "merge message",
            true);

        Assert.Equal(DeveloperLifecycleState.Completed, result.State);
        Assert.NotNull(result.GatedMerge);
        Assert.NotNull(result.Cleanup);
        Assert.Equal(
            ["branch", "stage", "commit", "push", "pull-request", "status", "merge", "cleanup"],
            calls);
    }

    [Fact]
    public async Task ExternalPullRequestFailure_IsSurfacedWithoutAdvancingToStatusMergeOrCleanup()
    {
        using var repository = new AcceptanceRepository();
        var calls = new List<string>();
        var parser = new DeveloperTaskParser();
        var statusProvider = new SequencedStatusProvider(
            Status(repository.Path, "feature/dev-0046-acceptance", true));
        var completer = new DeveloperTaskCompleter(
            parser,
            statusProvider,
            new RecordingStager(calls),
            new RecordingCommitter(calls),
            new RecordingPusher(calls));
        var pullRequests = new RecordingPullRequestService(calls)
        {
            Exception = new HttpRequestException("PR boundary failed")
        };
        var lifecycle = new DeveloperLifecycleOrchestrator(
            new DeveloperTaskWorkflow(parser, new AcceptanceGatedCompleter(completer), pullRequests),
            new RecordingStatusGate(calls),
            new RecordingMergeGate(calls),
            new RecordingCleaner(calls));

        var exception = await Assert.ThrowsAsync<DeveloperTaskExecutionException>(() => lifecycle.ExecuteAsync(
            repository.TaskPath,
            repository.Path,
            repository.Name,
            "commit",
            "origin",
            true,
            new GitHubRepositoryIdentity("owner", repository.Name),
            "main",
            null,
            false,
            PullRequestMergeMethod.Squash,
            null,
            null,
            false));

        Assert.Equal("PR boundary failed", exception.InnerException?.Message);
        Assert.Equal(["stage", "commit", "push", "pull-request"], calls);
    }

    [Fact]
    public async Task PersistedInterruptedWorkflow_IsRediscoveredAndCompletedThroughAutomaticResume()
    {
        var persisted = PersistedState("DEV-0046");
        var discovery = new MutableDiscovery([persisted]);
        var lifecycle = new CompletingPersistedLifecycle(discovery);
        var selector = new AutomaticResumeCandidateSelector(discovery);
        var resumer = new AutomaticPersistedLifecycleResumer(selector, lifecycle);
        var step = new AutomaticResumeBatchStep(resumer, discovery);
        var runner = new AutomaticResumeBatchRunner(step);

        var result = await runner.RunAsync(new AutomaticResumeBatchRunRequest(
            new AutomaticResumeBatchStepRequest(
                PullRequestMergeMethod.Squash,
                "title",
                "message",
                true),
            maximumSteps: 3));

        Assert.Equal(AutomaticResumeBatchRunState.Completed, result.State);
        Assert.Single(result.Steps);
        Assert.Equal(1, lifecycle.ResumeCalls);
        Assert.Empty(await discovery.ListAsync());
    }

    [Fact]
    public async Task AutomaticResume_IsBoundedAndTerminalStateIsNotResumed()
    {
        var limitingStep = new AlwaysMoreWorkStep();
        var limited = await new AutomaticResumeBatchRunner(limitingStep).RunAsync(
            new AutomaticResumeBatchRunRequest(StepRequest(), maximumSteps: 2));
        Assert.Equal(AutomaticResumeBatchRunState.LimitReached, limited.State);
        Assert.Equal(2, limitingStep.Calls);

        var runLimitingStep = new AlwaysMoreWorkStep();
        var runOrchestrator = new AutomaticResumeRunOrchestrator(
            new AutomaticResumeBatchRunner(runLimitingStep),
            new AutomaticResumeSchedulingDecisionService());
        var runLimited = await runOrchestrator.RunAsync(new AutomaticResumeRunRequest(
            new AutomaticResumeBatchRunRequest(StepRequest(), maximumSteps: 1),
            maximumBatchRuns: 2));
        Assert.Equal(AutomaticResumeRunState.LimitReached, runLimited.State);
        Assert.Equal(2, runLimited.BatchRuns.Count);
        Assert.Equal(2, runLimitingStep.Calls);

        var discovery = new MutableDiscovery([]);
        var lifecycle = new CompletingPersistedLifecycle(discovery);
        var emptyStep = new AutomaticResumeBatchStep(
            new AutomaticPersistedLifecycleResumer(
                new AutomaticResumeCandidateSelector(discovery),
                lifecycle),
            discovery);
        var terminal = await new AutomaticResumeBatchRunner(emptyStep).RunAsync(
            new AutomaticResumeBatchRunRequest(StepRequest(), maximumSteps: 2));

        Assert.Equal(AutomaticResumeBatchRunState.Empty, terminal.State);
        Assert.Equal(0, lifecycle.ResumeCalls);
    }

    [Fact]
    public async Task ProductionV1Composition_ResolvesWithoutExternalEffectsOrWorkerExecution()
    {
        var root = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"trailtrainer-dev-0046-{Guid.NewGuid():N}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DeveloperProductionRuntimeOptions.SectionName}:" +
                    nameof(DeveloperProductionRuntimeOptions.LifecycleStateStorageDirectory)] =
                    System.IO.Path.Combine(root, "lifecycle"),
                [$"{CodexExecutionOptions.SectionName}:ExecutablePath"] = "test-codex-never-run",
                [$"{GitHubApiOptions.SectionName}:Token"] = "test-token"
            })
            .Build();
        try
        {
            await new ProductionRuntimeHealthValidator(configuration).ValidateAsync();
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

    private static AutomaticResumeBatchStepRequest StepRequest() =>
        new(PullRequestMergeMethod.Squash, null, null, false);

    private static GitRepositoryStatus Status(string root, string branch, bool changed) =>
        new(true, root, branch, changed);

    private static DeveloperLifecyclePersistedState PersistedState(string taskId) => new(
        taskId,
        null,
        new DeveloperLifecycleResumeContext(
            "repository",
            new GitHubRepositoryIdentity("owner", "repository"),
            46,
            "feature/dev-0046-acceptance",
            "main",
            "origin"),
        new DateTimeOffset(2026, 8, 16, 12, 0, 0, TimeSpan.Zero));

    private sealed class AcceptanceRepository : IDisposable
    {
        public AcceptanceRepository()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"trailtrainer-v1-{Guid.NewGuid():N}");
            Name = new DirectoryInfo(Path).Name;
            var taskDirectory = System.IO.Path.Combine(Path, "docs", "developer-tasks");
            Directory.CreateDirectory(taskDirectory);
            TaskPath = System.IO.Path.Combine(taskDirectory, "DEV-0046-Acceptance.md");
            File.WriteAllText(TaskPath, $$"""
                # DEV-0046 – Acceptance

                ## Metadata
                - Task ID: `DEV-0046`
                - Repository: `{{Name}}`
                - Expected branch: `feature/dev-0046-acceptance`
                - Review report: `docs/developer-reviews/REVIEW-0046.md`
                """);
        }

        public string Path { get; }
        public string Name { get; }
        public string TaskPath { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private sealed class SequencedStatusProvider(params GitRepositoryStatus[] statuses)
        : IGitRepositoryStatusProvider
    {
        private readonly Queue<GitRepositoryStatus> remaining = new(statuses);

        public Task<GitRepositoryStatus> GetStatusAsync(
            string directoryPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(remaining.Dequeue());
    }

    private sealed class RecordingBranchCreator(IList<string> calls) : IGitBranchCreator
    {
        public Task<GitBranchCreationResult> CreateAsync(
            string directoryPath,
            string branchName,
            CancellationToken cancellationToken = default)
        {
            calls.Add("branch");
            return Task.FromResult(new GitBranchCreationResult(directoryPath, branchName));
        }
    }

    private sealed class RecordingStager(IList<string> calls) : IGitStager
    {
        public Task<GitStageResult> StageAllAsync(
            string directoryPath,
            CancellationToken cancellationToken = default)
        {
            calls.Add("stage");
            return Task.FromResult(new GitStageResult(directoryPath, true));
        }
    }

    private sealed class RecordingCommitter(IList<string> calls) : IGitCommitter
    {
        public Task<GitCommitResult> CommitAsync(
            string directoryPath,
            string message,
            CancellationToken cancellationToken = default)
        {
            calls.Add("commit");
            return Task.FromResult(new GitCommitResult(directoryPath, "abc123", message));
        }
    }

    private sealed class RecordingPusher(IList<string> calls) : IGitPusher
    {
        public Task<GitPushResult> PushAsync(
            string directoryPath,
            string remoteName,
            bool setUpstream,
            CancellationToken cancellationToken = default)
        {
            calls.Add("push");
            return Task.FromResult(new GitPushResult(
                directoryPath,
                "feature/dev-0046-acceptance",
                remoteName,
                setUpstream));
        }
    }

    private sealed class AcceptanceGatedCompleter(IDeveloperTaskCompleter completer)
        : IDeveloperTaskGatedCompleter
    {
        public async Task<DeveloperTaskGatedCompletionResult> CompleteAsync(
            string developerTaskFilePath,
            string repositoryDirectoryPath,
            string expectedRepositoryName,
            string commitMessage,
            string remoteName,
            bool setUpstream,
            CancellationToken cancellationToken = default)
        {
            var completion = await completer.CompleteAsync(
                developerTaskFilePath,
                repositoryDirectoryPath,
                expectedRepositoryName,
                commitMessage,
                remoteName,
                setUpstream,
                cancellationToken);
            return new DeveloperTaskGatedCompletionResult(
                completion.TaskId,
                new DeveloperReviewValidationResult(
                    completion.TaskId,
                    DeveloperReviewStatus.ReadyForReview,
                    [],
                    []),
                completion);
        }
    }

    private sealed class RecordingPullRequestService(IList<string> calls) : IPullRequestService
    {
        public Exception? Exception { get; init; }

        public Task<PullRequestEnsureResult> EnsureOpenAsync(
            GitHubRepositoryIdentity repository,
            string headBranch,
            string baseBranch,
            string title,
            string? body = null,
            bool draft = false,
            CancellationToken cancellationToken = default)
        {
            calls.Add("pull-request");
            return Exception is not null
                ? Task.FromException<PullRequestEnsureResult>(Exception)
                : Task.FromResult(new PullRequestEnsureResult(
                    new PullRequestInfo(
                        46,
                        new Uri("https://example.test/pulls/46"),
                        title,
                        headBranch,
                        baseBranch,
                        draft),
                    true));
        }
    }

    private sealed class RecordingStatusGate(IList<string> calls) : IPullRequestStatusGate
    {
        public Task<PullRequestStatusGateResult> EvaluateAsync(
            GitHubRepositoryIdentity repository,
            int pullRequestNumber,
            CancellationToken cancellationToken = default)
        {
            calls.Add("status");
            return Task.FromResult(new PullRequestStatusGateResult(
                pullRequestNumber,
                "head-sha",
                PullRequestGateState.Successful,
                [new PullRequestCheck("build", PullRequestCheckState.Successful)]));
        }
    }

    private sealed class RecordingMergeGate(IList<string> calls) : IPullRequestMergeGate
    {
        public Task<PullRequestGatedMergeResult> MergeAsync(
            GitHubRepositoryIdentity repository,
            int pullRequestNumber,
            PullRequestMergeMethod method,
            string? commitTitle = null,
            string? commitMessage = null,
            CancellationToken cancellationToken = default)
        {
            calls.Add("merge");
            var status = new PullRequestStatusGateResult(
                pullRequestNumber,
                "head-sha",
                PullRequestGateState.Successful,
                [new PullRequestCheck("build", PullRequestCheckState.Successful)]);
            return Task.FromResult(new PullRequestGatedMergeResult(
                status,
                new PullRequestMergeResult(pullRequestNumber, true, "merge-sha", method)));
        }
    }

    private sealed class RecordingCleaner(IList<string> calls) : IPostMergeCleaner
    {
        public Task<PostMergeCleanupResult> CleanupAsync(
            string repositoryDirectory,
            GitHubRepositoryIdentity repository,
            int pullRequestNumber,
            PullRequestMergeResult mergeResult,
            string featureBranch,
            string baseBranch,
            string remoteName,
            bool deleteRemoteBranch,
            CancellationToken cancellationToken = default)
        {
            calls.Add("cleanup");
            return Task.FromResult(new PostMergeCleanupResult(
                repositoryDirectory,
                baseBranch,
                featureBranch,
                true,
                deleteRemoteBranch));
        }
    }

    private sealed class MutableDiscovery(IReadOnlyList<DeveloperLifecyclePersistedState> initial)
        : IDeveloperLifecycleStateDiscovery
    {
        private readonly List<DeveloperLifecyclePersistedState> states = [.. initial];

        public Task<IReadOnlyList<DeveloperLifecyclePersistedState>> ListAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeveloperLifecyclePersistedState>>(states.ToArray());

        public void Complete(string taskId) => states.RemoveAll(state => state.TaskId == taskId);
    }

    private sealed class CompletingPersistedLifecycle(MutableDiscovery discovery)
        : IPersistedDeveloperLifecycle
    {
        public int ResumeCalls { get; private set; }

        public Task<PersistedDeveloperLifecycleStartResult> StartAsync(
            PersistedDeveloperLifecycleStartRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PersistedDeveloperLifecycleResumeResult> ResumeAsync(
            PersistedDeveloperLifecycleResumeRequest request,
            CancellationToken cancellationToken = default)
        {
            ResumeCalls++;
            var persisted = PersistedState(request.TaskId);
            discovery.Complete(request.TaskId);
            var status = new PullRequestStatusGateResult(
                46,
                "head-sha",
                PullRequestGateState.Successful,
                [new PullRequestCheck("build", PullRequestCheckState.Successful)]);
            var merge = new PullRequestGatedMergeResult(
                status,
                new PullRequestMergeResult(46, true, "merge-sha", request.MergeMethod));
            var lifecycle = new DeveloperLifecycleResumeResult(
                DeveloperLifecycleState.Completed,
                persisted.ResumeContext,
                status,
                merge,
                new PostMergeCleanupResult(
                    "repository", "main", "feature/dev-0046-acceptance", true, true));
            return Task.FromResult(new PersistedDeveloperLifecycleResumeResult(
                PersistedDeveloperLifecycleResumeState.Completed,
                request.TaskId,
                persisted,
                lifecycle));
        }
    }

    private sealed class AlwaysMoreWorkStep : IAutomaticResumeBatchStep
    {
        public int Calls { get; private set; }

        public Task<AutomaticResumeBatchStepResult> ExecuteAsync(
            AutomaticResumeBatchStepRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            var persisted = PersistedState($"DEV-{Calls:0000}");
            var candidate = new AutomaticResumeCandidateResult(
                AutomaticResumeCandidateState.Found,
                persisted,
                new PersistedLifecycleResumeTarget(persisted.TaskId, persisted));
            var status = new PullRequestStatusGateResult(
                46, "head", PullRequestGateState.Successful,
                [new PullRequestCheck("build", PullRequestCheckState.Successful)]);
            var merge = new PullRequestGatedMergeResult(
                status,
                new PullRequestMergeResult(46, true, "merge", PullRequestMergeMethod.Squash));
            var lifecycle = new DeveloperLifecycleResumeResult(
                DeveloperLifecycleState.Completed,
                persisted.ResumeContext,
                status,
                merge,
                new PostMergeCleanupResult("repository", "main", "feature", true, false));
            var resumed = new PersistedDeveloperLifecycleResumeResult(
                PersistedDeveloperLifecycleResumeState.Completed,
                persisted.TaskId,
                persisted,
                lifecycle);
            var automatic = new AutomaticPersistedLifecycleResumeResult(
                AutomaticPersistedLifecycleResumeState.Completed,
                candidate,
                resumed);
            return Task.FromResult(new AutomaticResumeBatchStepResult(
                AutomaticResumeBatchStepState.Completed,
                automatic,
                true));
        }
    }
}
