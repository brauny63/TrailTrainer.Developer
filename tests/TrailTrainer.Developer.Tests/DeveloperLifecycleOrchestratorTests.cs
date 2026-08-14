using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class DeveloperLifecycleOrchestratorTests
{
    [Fact]
    public async Task ExecuteAsync_Completed_DelegatesDerivedAndCallerValuesExactlyInOrder()
    {
        using var source = new CancellationTokenSource();
        var fixture = new Fixture(PullRequestGateState.Successful);

        var result = await fixture.ExecuteAsync(source.Token);

        Assert.Equal(DeveloperLifecycleState.Completed, result.State);
        Assert.Same(fixture.WorkflowResult, result.Workflow);
        Assert.Same(fixture.StatusResult, result.StatusGate);
        Assert.Same(fixture.GatedMergeResult, result.GatedMerge);
        Assert.Same(fixture.CleanupResult, result.Cleanup);
        Assert.Equal(["workflow", "status", "merge", "cleanup"], fixture.Calls);

        Assert.Equal(1, fixture.Workflow.CallCount);
        Assert.Equal("Exact/task.md", fixture.Workflow.TaskFilePath);
        Assert.Equal("Exact/repository", fixture.Workflow.RepositoryDirectory);
        Assert.Equal("Exact.Repository", fixture.Workflow.ExpectedRepositoryName);
        Assert.Equal("Exact commit", fixture.Workflow.CommitMessage);
        Assert.Equal("Exact-Remote", fixture.Workflow.GitRemoteName);
        Assert.True(fixture.Workflow.SetUpstream);
        Assert.Same(fixture.Repository, fixture.Workflow.Repository);
        Assert.Equal("Exact-Base", fixture.Workflow.BaseBranch);
        Assert.Equal("Exact PR body", fixture.Workflow.PullRequestBody);
        Assert.True(fixture.Workflow.PullRequestDraft);

        Assert.Equal(1, fixture.StatusGate.CallCount);
        Assert.Same(fixture.Repository, fixture.StatusGate.Repository);
        Assert.Equal(73, fixture.StatusGate.PullRequestNumber);

        Assert.Equal(1, fixture.MergeGate.CallCount);
        Assert.Same(fixture.Repository, fixture.MergeGate.Repository);
        Assert.Equal(73, fixture.MergeGate.PullRequestNumber);
        Assert.Equal(PullRequestMergeMethod.Rebase, fixture.MergeGate.Method);
        Assert.Equal("Exact merge title", fixture.MergeGate.CommitTitle);
        Assert.Equal("Exact merge message", fixture.MergeGate.CommitMessage);

        Assert.Equal(1, fixture.Cleaner.CallCount);
        Assert.Equal("Exact/repository", fixture.Cleaner.RepositoryDirectory);
        Assert.Same(fixture.Repository, fixture.Cleaner.Repository);
        Assert.Equal(73, fixture.Cleaner.PullRequestNumber);
        Assert.Same(fixture.GatedMergeResult.Merge, fixture.Cleaner.MergeResult);
        Assert.Equal("workflow/authoritative-feature", fixture.Cleaner.FeatureBranch);
        Assert.Equal("Exact-Base", fixture.Cleaner.BaseBranch);
        Assert.Equal("Exact-Remote", fixture.Cleaner.RemoteName);
        Assert.True(fixture.Cleaner.DeleteRemoteBranch);

        Assert.Equal(source.Token, fixture.Workflow.CancellationToken);
        Assert.Equal(source.Token, fixture.StatusGate.CancellationToken);
        Assert.Equal(source.Token, fixture.MergeGate.CancellationToken);
        Assert.Equal(source.Token, fixture.Cleaner.CancellationToken);
    }

    [Theory]
    [InlineData(PullRequestGateState.Pending, DeveloperLifecycleState.Pending)]
    [InlineData(PullRequestGateState.Failed, DeveloperLifecycleState.Failed)]
    public async Task ExecuteAsync_NonSuccessfulExplicitStatusReturnsWithoutMergeOrCleanup(
        PullRequestGateState gateState,
        DeveloperLifecycleState lifecycleState)
    {
        var fixture = new Fixture(gateState);

        var result = await fixture.ExecuteAsync();

        Assert.Equal(lifecycleState, result.State);
        Assert.Same(fixture.WorkflowResult, result.Workflow);
        Assert.Same(fixture.StatusResult, result.StatusGate);
        Assert.Null(result.GatedMerge);
        Assert.Null(result.Cleanup);
        Assert.Equal(["workflow", "status"], fixture.Calls);
        Assert.Equal(0, fixture.MergeGate.CallCount);
        Assert.Equal(0, fixture.Cleaner.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WorkflowFailurePreventsAllLaterPhases()
    {
        var fixture = new Fixture(PullRequestGateState.Successful);
        var expected = new InvalidOperationException("workflow failed");
        fixture.Workflow.Exception = expected;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ExecuteAsync());

        Assert.Same(expected, exception);
        Assert.Equal(["workflow"], fixture.Calls);
        Assert.Equal(0, fixture.StatusGate.CallCount);
        Assert.Equal(0, fixture.MergeGate.CallCount);
        Assert.Equal(0, fixture.Cleaner.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_StatusFailurePreventsMergeAndCleanup()
    {
        var fixture = new Fixture(PullRequestGateState.Successful);
        var expected = new HttpRequestException("status failed");
        fixture.StatusGate.Exception = expected;

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => fixture.ExecuteAsync());

        Assert.Same(expected, exception);
        Assert.Equal(["workflow", "status"], fixture.Calls);
        Assert.Equal(0, fixture.MergeGate.CallCount);
        Assert.Equal(0, fixture.Cleaner.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_MergeGateFailureAfterSuccessfulExplicitStatusPreventsCleanupAndDoesNotRetry()
    {
        var fixture = new Fixture(PullRequestGateState.Successful);
        var expected = new InvalidOperationException("fresh gate changed");
        fixture.MergeGate.Exception = expected;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ExecuteAsync());

        Assert.Same(expected, exception);
        Assert.Equal(["workflow", "status", "merge"], fixture.Calls);
        Assert.Equal(1, fixture.StatusGate.CallCount);
        Assert.Equal(1, fixture.MergeGate.CallCount);
        Assert.Equal(0, fixture.Cleaner.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_InconsistentNonMergedResultPreventsCleanup()
    {
        var fixture = new Fixture(PullRequestGateState.Successful);
        fixture.MergeGate.Result = new PullRequestGatedMergeResult(
            fixture.StatusResult,
            new PullRequestMergeResult(73, false, null, PullRequestMergeMethod.Rebase));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ExecuteAsync());

        Assert.Contains("confirmed successful", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["workflow", "status", "merge"], fixture.Calls);
        Assert.Equal(0, fixture.Cleaner.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_CleanupFailurePropagatesWithoutRemergeOrRetry()
    {
        var fixture = new Fixture(PullRequestGateState.Successful);
        var expected = new InvalidOperationException("cleanup failed");
        fixture.Cleaner.Exception = expected;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ExecuteAsync());

        Assert.Same(expected, exception);
        Assert.Equal(["workflow", "status", "merge", "cleanup"], fixture.Calls);
        Assert.Equal(1, fixture.MergeGate.CallCount);
        Assert.Equal(1, fixture.Cleaner.CallCount);
    }

    [Theory]
    [InlineData("workflow")]
    [InlineData("status")]
    [InlineData("merge")]
    [InlineData("cleanup")]
    public async Task ExecuteAsync_CancellationAtPhasePreventsSubsequentPhases(string phase)
    {
        var fixture = new Fixture(PullRequestGateState.Successful);
        var cancellation = new OperationCanceledException();
        fixture.SetException(phase, cancellation);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.ExecuteAsync());

        var phaseIndex = Array.IndexOf(["workflow", "status", "merge", "cleanup"], phase);
        Assert.Equal(phaseIndex + 1, fixture.Calls.Count);
        Assert.Equal(Enumerable.Range(0, phaseIndex + 1)
            .Select(index => new[] { "workflow", "status", "merge", "cleanup" }[index]), fixture.Calls);
    }

    [Fact]
    public void LifecycleResult_EnforcesPendingAndFailedInvariants()
    {
        var workflow = Fixture.CreateWorkflowResult();
        var pending = Fixture.CreateStatusResult(PullRequestGateState.Pending);
        var failed = Fixture.CreateStatusResult(PullRequestGateState.Failed);
        var merge = Fixture.CreateGatedMergeResult();
        var cleanup = Fixture.CreateCleanupResult();

        Assert.Throws<ArgumentException>(() => new DeveloperLifecycleResult(
            DeveloperLifecycleState.Pending, workflow, pending, merge, cleanup));
        Assert.Throws<ArgumentException>(() => new DeveloperLifecycleResult(
            DeveloperLifecycleState.Failed, workflow, failed, merge, cleanup));
        Assert.Throws<ArgumentException>(() => new DeveloperLifecycleResult(
            DeveloperLifecycleState.Pending, workflow, failed));
        Assert.Throws<ArgumentException>(() => new DeveloperLifecycleResult(
            DeveloperLifecycleState.Failed, workflow, pending));
    }

    [Fact]
    public void LifecycleResult_CompletedRequiresSuccessfulStatusConfirmedMergeAndCleanup()
    {
        var workflow = Fixture.CreateWorkflowResult();
        var successful = Fixture.CreateStatusResult(PullRequestGateState.Successful);
        var pending = Fixture.CreateStatusResult(PullRequestGateState.Pending);
        var merge = Fixture.CreateGatedMergeResult();
        var nonMerge = new PullRequestGatedMergeResult(
            successful,
            new PullRequestMergeResult(73, false, null, PullRequestMergeMethod.Rebase));
        var cleanup = Fixture.CreateCleanupResult();

        Assert.Throws<ArgumentException>(() => new DeveloperLifecycleResult(
            DeveloperLifecycleState.Completed, workflow, successful, null, cleanup));
        Assert.Throws<ArgumentException>(() => new DeveloperLifecycleResult(
            DeveloperLifecycleState.Completed, workflow, successful, merge, null));
        Assert.Throws<ArgumentException>(() => new DeveloperLifecycleResult(
            DeveloperLifecycleState.Completed, workflow, successful, nonMerge, cleanup));
        Assert.Throws<ArgumentException>(() => new DeveloperLifecycleResult(
            DeveloperLifecycleState.Completed, workflow, pending, merge, cleanup));
    }

    private sealed class Fixture
    {
        public Fixture(PullRequestGateState state)
        {
            WorkflowResult = CreateWorkflowResult();
            StatusResult = CreateStatusResult(state);
            GatedMergeResult = CreateGatedMergeResult();
            CleanupResult = CreateCleanupResult();
            Workflow = new FakeWorkflow(WorkflowResult, Calls);
            StatusGate = new FakeStatusGate(StatusResult, Calls);
            MergeGate = new FakeMergeGate(GatedMergeResult, Calls);
            Cleaner = new FakeCleaner(CleanupResult, Calls);
            Orchestrator = new DeveloperLifecycleOrchestrator(Workflow, StatusGate, MergeGate, Cleaner);
        }

        public List<string> Calls { get; } = [];
        public GitHubRepositoryIdentity Repository { get; } = new("ExactOwner", "ExactRepository");
        public DeveloperTaskWorkflowResult WorkflowResult { get; }
        public PullRequestStatusGateResult StatusResult { get; }
        public PullRequestGatedMergeResult GatedMergeResult { get; }
        public PostMergeCleanupResult CleanupResult { get; }
        public FakeWorkflow Workflow { get; }
        public FakeStatusGate StatusGate { get; }
        public FakeMergeGate MergeGate { get; }
        public FakeCleaner Cleaner { get; }
        public DeveloperLifecycleOrchestrator Orchestrator { get; }

        public Task<DeveloperLifecycleResult> ExecuteAsync(CancellationToken cancellationToken = default) =>
            Orchestrator.ExecuteAsync(
                "Exact/task.md", "Exact/repository", "Exact.Repository", "Exact commit",
                "Exact-Remote", true, Repository, "Exact-Base", "Exact PR body", true,
                PullRequestMergeMethod.Rebase, "Exact merge title", "Exact merge message", true,
                cancellationToken);

        public void SetException(string phase, Exception exception)
        {
            switch (phase)
            {
                case "workflow": Workflow.Exception = exception; break;
                case "status": StatusGate.Exception = exception; break;
                case "merge": MergeGate.Exception = exception; break;
                case "cleanup": Cleaner.Exception = exception; break;
            }
        }

        public static DeveloperTaskWorkflowResult CreateWorkflowResult()
        {
            var taskId = new DeveloperTaskId(17);
            var completion = new DeveloperTaskCompletionResult(
                taskId, "title", "root", "workflow/authoritative-feature", "commit-sha",
                "message", "remote", true, "task.md", "review.md");
            var validation = new DeveloperReviewValidationResult(
                taskId, DeveloperReviewStatus.ReadyForReview, [], []);
            return new DeveloperTaskWorkflowResult(
                taskId,
                new DeveloperTaskGatedCompletionResult(taskId, validation, completion),
                new PullRequestEnsureResult(
                    new PullRequestInfo(73, new Uri("https://example.invalid/pr/73"),
                        "title", "different-pr-head", "main", false),
                    true));
        }

        public static PullRequestStatusGateResult CreateStatusResult(PullRequestGateState state) =>
            new(73, "explicit-status-sha", state, []);

        public static PullRequestGatedMergeResult CreateGatedMergeResult() => new(
            CreateStatusResult(PullRequestGateState.Successful),
            new PullRequestMergeResult(73, true, "merge-sha", PullRequestMergeMethod.Rebase));

        public static PostMergeCleanupResult CreateCleanupResult() =>
            new("root", "main", "workflow/authoritative-feature", true, true);
    }

    private sealed class FakeWorkflow(DeveloperTaskWorkflowResult result, IList<string> calls)
        : IDeveloperTaskWorkflow
    {
        public Exception? Exception { get; set; }
        public int CallCount { get; private set; }
        public string? TaskFilePath { get; private set; }
        public string? RepositoryDirectory { get; private set; }
        public string? ExpectedRepositoryName { get; private set; }
        public string? CommitMessage { get; private set; }
        public string? GitRemoteName { get; private set; }
        public bool SetUpstream { get; private set; }
        public GitHubRepositoryIdentity? Repository { get; private set; }
        public string? BaseBranch { get; private set; }
        public string? PullRequestBody { get; private set; }
        public bool PullRequestDraft { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<DeveloperTaskWorkflowResult> ExecuteAsync(
            string developerTaskFilePath, string repositoryDirectoryPath,
            string expectedRepositoryName, string commitMessage, string gitRemoteName,
            bool setUpstream, GitHubRepositoryIdentity gitHubRepository,
            string pullRequestBaseBranch, string? pullRequestBody = null,
            bool pullRequestDraft = false, CancellationToken cancellationToken = default)
        {
            calls.Add("workflow");
            CallCount++;
            TaskFilePath = developerTaskFilePath;
            RepositoryDirectory = repositoryDirectoryPath;
            ExpectedRepositoryName = expectedRepositoryName;
            CommitMessage = commitMessage;
            GitRemoteName = gitRemoteName;
            SetUpstream = setUpstream;
            Repository = gitHubRepository;
            BaseBranch = pullRequestBaseBranch;
            PullRequestBody = pullRequestBody;
            PullRequestDraft = pullRequestDraft;
            CancellationToken = cancellationToken;
            return Exception is null ? Task.FromResult(result) : Task.FromException<DeveloperTaskWorkflowResult>(Exception);
        }
    }

    private sealed class FakeStatusGate(PullRequestStatusGateResult result, IList<string> calls)
        : IPullRequestStatusGate
    {
        public Exception? Exception { get; set; }
        public int CallCount { get; private set; }
        public GitHubRepositoryIdentity? Repository { get; private set; }
        public int PullRequestNumber { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<PullRequestStatusGateResult> EvaluateAsync(
            GitHubRepositoryIdentity repository, int pullRequestNumber,
            CancellationToken cancellationToken = default)
        {
            calls.Add("status");
            CallCount++;
            Repository = repository;
            PullRequestNumber = pullRequestNumber;
            CancellationToken = cancellationToken;
            return Exception is null ? Task.FromResult(result) : Task.FromException<PullRequestStatusGateResult>(Exception);
        }
    }

    private sealed class FakeMergeGate(PullRequestGatedMergeResult result, IList<string> calls)
        : IPullRequestMergeGate
    {
        public PullRequestGatedMergeResult Result { get; set; } = result;
        public Exception? Exception { get; set; }
        public int CallCount { get; private set; }
        public GitHubRepositoryIdentity? Repository { get; private set; }
        public int PullRequestNumber { get; private set; }
        public PullRequestMergeMethod Method { get; private set; }
        public string? CommitTitle { get; private set; }
        public string? CommitMessage { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<PullRequestGatedMergeResult> MergeAsync(
            GitHubRepositoryIdentity repository, int pullRequestNumber,
            PullRequestMergeMethod method, string? commitTitle = null,
            string? commitMessage = null, CancellationToken cancellationToken = default)
        {
            calls.Add("merge");
            CallCount++;
            Repository = repository;
            PullRequestNumber = pullRequestNumber;
            Method = method;
            CommitTitle = commitTitle;
            CommitMessage = commitMessage;
            CancellationToken = cancellationToken;
            return Exception is null ? Task.FromResult(Result) : Task.FromException<PullRequestGatedMergeResult>(Exception);
        }
    }

    private sealed class FakeCleaner(PostMergeCleanupResult result, IList<string> calls) : IPostMergeCleaner
    {
        public Exception? Exception { get; set; }
        public int CallCount { get; private set; }
        public string? RepositoryDirectory { get; private set; }
        public GitHubRepositoryIdentity? Repository { get; private set; }
        public int PullRequestNumber { get; private set; }
        public PullRequestMergeResult? MergeResult { get; private set; }
        public string? FeatureBranch { get; private set; }
        public string? BaseBranch { get; private set; }
        public string? RemoteName { get; private set; }
        public bool DeleteRemoteBranch { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<PostMergeCleanupResult> CleanupAsync(
            string repositoryDirectory, GitHubRepositoryIdentity repository,
            int pullRequestNumber, PullRequestMergeResult mergeResult,
            string featureBranch, string baseBranch, string remoteName,
            bool deleteRemoteBranch, CancellationToken cancellationToken = default)
        {
            calls.Add("cleanup");
            CallCount++;
            RepositoryDirectory = repositoryDirectory;
            Repository = repository;
            PullRequestNumber = pullRequestNumber;
            MergeResult = mergeResult;
            FeatureBranch = featureBranch;
            BaseBranch = baseBranch;
            RemoteName = remoteName;
            DeleteRemoteBranch = deleteRemoteBranch;
            CancellationToken = cancellationToken;
            return Exception is null ? Task.FromResult(result) : Task.FromException<PostMergeCleanupResult>(Exception);
        }
    }
}
