using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class PersistedDeveloperLifecycleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void StartRequest_EmptyTaskIdRejected(string? taskId)
    {
        Assert.ThrowsAny<ArgumentException>(() => StartRequest(taskId!));
    }

    [Fact]
    public void StartRequest_WhitespaceOptionalTaskPathRejectedAndNullAccepted()
    {
        Assert.Throws<ArgumentException>(() => StartRequest(taskFilePath: "   "));
        Assert.Null(StartRequest(taskFilePath: null).TaskFilePath);
    }

    [Fact]
    public async Task StartAsync_NullRequestRejectedBeforeDependencies()
    {
        var fixture = new Fixture();

        await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Service.StartAsync(null!));

        Assert.Empty(fixture.Calls);
    }

    [Fact]
    public async Task StartAsync_PendingDelegatesExactlyDerivesStateThenSavesOnce()
    {
        using var source = new CancellationTokenSource();
        var fixture = new Fixture();
        fixture.Orchestrator.Result = Lifecycle(DeveloperLifecycleState.Pending);
        var request = StartRequest();

        var result = await fixture.Service.StartAsync(request, source.Token);

        Assert.Equal(["start-lifecycle", "save"], fixture.Calls);
        Assert.Equal(1, fixture.Orchestrator.CallCount);
        Assert.Equal(request.DeveloperTaskFilePath, fixture.Orchestrator.TaskFilePath);
        Assert.Equal(request.RepositoryDirectoryPath, fixture.Orchestrator.RepositoryDirectory);
        Assert.Equal(request.ExpectedRepositoryName, fixture.Orchestrator.ExpectedRepositoryName);
        Assert.Equal(request.CommitMessage, fixture.Orchestrator.CommitMessage);
        Assert.Equal(request.GitRemoteName, fixture.Orchestrator.GitRemoteName);
        Assert.Equal(request.SetUpstream, fixture.Orchestrator.SetUpstream);
        Assert.Same(request.GitHubRepository, fixture.Orchestrator.Repository);
        Assert.Equal(request.PullRequestBaseBranch, fixture.Orchestrator.BaseBranch);
        Assert.Equal(request.PullRequestBody, fixture.Orchestrator.PullRequestBody);
        Assert.Equal(request.PullRequestDraft, fixture.Orchestrator.PullRequestDraft);
        Assert.Equal(request.MergeMethod, fixture.Orchestrator.MergeMethod);
        Assert.Equal(request.MergeCommitTitle, fixture.Orchestrator.MergeTitle);
        Assert.Equal(request.MergeCommitMessage, fixture.Orchestrator.MergeMessage);
        Assert.Equal(request.DeleteRemoteBranch, fixture.Orchestrator.DeleteRemoteBranch);
        Assert.Equal(source.Token, fixture.Orchestrator.CancellationToken);

        Assert.Same(fixture.Orchestrator.Result, result.Lifecycle);
        Assert.Same(fixture.Store.SavedState, result.PersistedState);
        var saved = Assert.IsType<DeveloperLifecyclePersistedState>(fixture.Store.SavedState);
        Assert.Equal("Exact-Task-ID", saved.TaskId);
        Assert.Equal("Exact/task-file.md", saved.TaskFilePath);
        Assert.Equal(request.RepositoryDirectoryPath, saved.ResumeContext.RepositoryDirectory);
        Assert.Same(request.GitHubRepository, saved.ResumeContext.Repository);
        Assert.Equal(73, saved.ResumeContext.PullRequestNumber);
        Assert.Equal("workflow/authoritative-feature", saved.ResumeContext.FeatureBranch);
        Assert.Equal(request.PullRequestBaseBranch, saved.ResumeContext.BaseBranch);
        Assert.Equal(request.GitRemoteName, saved.ResumeContext.GitRemoteName);
        Assert.Equal(fixture.Clock.UtcNow, saved.SavedAtUtc);
        Assert.Equal(1, fixture.Store.SaveCount);
        Assert.Equal(source.Token, fixture.Store.SaveCancellationToken);
    }

    [Theory]
    [InlineData(DeveloperLifecycleState.Failed)]
    [InlineData(DeveloperLifecycleState.Completed)]
    public async Task StartAsync_NonPendingReturnsExactLifecycleWithoutStoreMutation(DeveloperLifecycleState state)
    {
        var fixture = new Fixture();
        fixture.Orchestrator.Result = Lifecycle(state);

        var result = await fixture.Service.StartAsync(StartRequest());

        Assert.Same(fixture.Orchestrator.Result, result.Lifecycle);
        Assert.Null(result.PersistedState);
        Assert.Equal(["start-lifecycle"], fixture.Calls);
        Assert.Equal(0, fixture.Store.SaveCount);
        Assert.Equal(0, fixture.Store.DeleteCount);
    }

    [Fact]
    public async Task StartAsync_LifecycleFailurePreventsSave()
    {
        var fixture = new Fixture();
        var expected = new InvalidOperationException("lifecycle failed");
        fixture.Orchestrator.Exception = expected;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.StartAsync(StartRequest()));

        Assert.Same(expected, exception);
        Assert.Equal(["start-lifecycle"], fixture.Calls);
        Assert.Equal(0, fixture.Store.SaveCount);
    }

    [Fact]
    public async Task StartAsync_SaveFailurePropagatesWithoutRetry()
    {
        var fixture = new Fixture();
        fixture.Orchestrator.Result = Lifecycle(DeveloperLifecycleState.Pending);
        var expected = new IOException("save failed");
        fixture.Store.SaveException = expected;

        var exception = await Assert.ThrowsAsync<IOException>(() => fixture.Service.StartAsync(StartRequest()));

        Assert.Same(expected, exception);
        Assert.Equal(["start-lifecycle", "save"], fixture.Calls);
        Assert.Equal(1, fixture.Store.SaveCount);
    }

    [Fact]
    public async Task StartAsync_NonUtcClockFailsBeforeSave()
    {
        var fixture = new Fixture(new DateTimeOffset(2026, 1, 1, 1, 0, 0, TimeSpan.FromHours(1)));
        fixture.Orchestrator.Result = Lifecycle(DeveloperLifecycleState.Pending);

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.StartAsync(StartRequest()));

        Assert.Equal(["start-lifecycle"], fixture.Calls);
        Assert.Equal(0, fixture.Store.SaveCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResumeRequest_EmptyTaskIdRejected(string? taskId)
    {
        Assert.ThrowsAny<ArgumentException>(() => ResumeRequest(taskId!));
    }

    [Fact]
    public async Task ResumeAsync_NullRequestRejectedBeforeLoad()
    {
        var fixture = new Fixture();

        await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Service.ResumeAsync(null!));

        Assert.Empty(fixture.Calls);
    }

    [Fact]
    public async Task ResumeAsync_MissingStateReturnsNotFoundWithoutResumeOrDelete()
    {
        using var source = new CancellationTokenSource();
        var fixture = new Fixture();
        fixture.Store.LoadedState = null;

        var result = await fixture.Service.ResumeAsync(ResumeRequest(), source.Token);

        Assert.Equal(PersistedDeveloperLifecycleResumeState.NotFound, result.State);
        Assert.Equal("Exact-Task-ID", result.TaskId);
        Assert.Null(result.PersistedState);
        Assert.Null(result.Lifecycle);
        Assert.Equal(["load"], fixture.Calls);
        Assert.Equal("Exact-Task-ID", fixture.Store.LoadTaskId);
        Assert.Equal(source.Token, fixture.Store.LoadCancellationToken);
        Assert.Equal(0, fixture.Resumer.CallCount);
        Assert.Equal(0, fixture.Store.DeleteCount);
    }

    [Fact]
    public async Task ResumeAsync_LoadedTaskIdMismatchFailsBeforeResumeOrDelete()
    {
        var fixture = new Fixture();
        fixture.Store.LoadedState = PersistedState("Different-Task-ID");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.ResumeAsync(ResumeRequest()));

        Assert.Contains("does not match", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["load"], fixture.Calls);
        Assert.Equal(0, fixture.Resumer.CallCount);
        Assert.Equal(0, fixture.Store.DeleteCount);
    }

    [Theory]
    [InlineData(DeveloperLifecycleState.Pending, PersistedDeveloperLifecycleResumeState.Pending)]
    [InlineData(DeveloperLifecycleState.Failed, PersistedDeveloperLifecycleResumeState.Failed)]
    public async Task ResumeAsync_NonCompletedRetainsExactStateWithoutSaveOrDelete(
        DeveloperLifecycleState lifecycleState,
        PersistedDeveloperLifecycleResumeState expectedState)
    {
        var fixture = new Fixture();
        fixture.Store.LoadedState = PersistedState();
        fixture.Resumer.Result = ResumeLifecycle(lifecycleState, fixture.Store.LoadedState.ResumeContext);

        var result = await fixture.Service.ResumeAsync(ResumeRequest());

        Assert.Equal(expectedState, result.State);
        Assert.Same(fixture.Store.LoadedState, result.PersistedState);
        Assert.Same(fixture.Resumer.Result, result.Lifecycle);
        Assert.Equal(["load", "resume-lifecycle"], fixture.Calls);
        Assert.Equal(0, fixture.Store.SaveCount);
        Assert.Equal(0, fixture.Store.DeleteCount);
    }

    [Fact]
    public async Task ResumeAsync_DelegatesExactLoadedContextAndMergeInputs()
    {
        using var source = new CancellationTokenSource();
        var fixture = new Fixture();
        fixture.Store.LoadedState = PersistedState();
        fixture.Resumer.Result = ResumeLifecycle(
            DeveloperLifecycleState.Pending,
            fixture.Store.LoadedState.ResumeContext);
        var request = ResumeRequest();

        await fixture.Service.ResumeAsync(request, source.Token);

        Assert.Same(fixture.Store.LoadedState.ResumeContext, fixture.Resumer.Context);
        Assert.Equal(request.MergeMethod, fixture.Resumer.MergeMethod);
        Assert.Equal(request.MergeCommitTitle, fixture.Resumer.MergeTitle);
        Assert.Equal(request.MergeCommitMessage, fixture.Resumer.MergeMessage);
        Assert.Equal(request.DeleteRemoteBranch, fixture.Resumer.DeleteRemoteBranch);
        Assert.Equal(source.Token, fixture.Resumer.CancellationToken);
        Assert.Equal(["load", "resume-lifecycle"], fixture.Calls);
    }

    [Fact]
    public async Task ResumeAsync_CompletedDeletesAfterResumeAndRetainsLoadedStateInResult()
    {
        using var source = new CancellationTokenSource();
        var fixture = new Fixture();
        fixture.Store.LoadedState = PersistedState();
        fixture.Resumer.Result = ResumeLifecycle(
            DeveloperLifecycleState.Completed,
            fixture.Store.LoadedState.ResumeContext);

        var result = await fixture.Service.ResumeAsync(ResumeRequest(), source.Token);

        Assert.Equal(PersistedDeveloperLifecycleResumeState.Completed, result.State);
        Assert.Same(fixture.Store.LoadedState, result.PersistedState);
        Assert.Same(fixture.Resumer.Result, result.Lifecycle);
        Assert.Equal(["load", "resume-lifecycle", "delete"], fixture.Calls);
        Assert.Equal(1, fixture.Resumer.CallCount);
        Assert.Equal(1, fixture.Store.DeleteCount);
        Assert.Equal("Exact-Task-ID", fixture.Store.DeleteTaskId);
        Assert.Equal(source.Token, fixture.Store.DeleteCancellationToken);
        Assert.Equal(0, fixture.Store.SaveCount);
    }

    [Fact]
    public async Task ResumeAsync_LoadFailurePreventsResumeAndDelete()
    {
        var fixture = new Fixture();
        var expected = new IOException("load failed");
        fixture.Store.LoadException = expected;

        var exception = await Assert.ThrowsAsync<IOException>(() => fixture.Service.ResumeAsync(ResumeRequest()));

        Assert.Same(expected, exception);
        Assert.Equal(["load"], fixture.Calls);
        Assert.Equal(0, fixture.Resumer.CallCount);
        Assert.Equal(0, fixture.Store.DeleteCount);
    }

    [Fact]
    public async Task ResumeAsync_ResumerFailurePreventsDelete()
    {
        var fixture = new Fixture();
        fixture.Store.LoadedState = PersistedState();
        var expected = new InvalidOperationException("resume failed");
        fixture.Resumer.Exception = expected;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.ResumeAsync(ResumeRequest()));

        Assert.Same(expected, exception);
        Assert.Equal(["load", "resume-lifecycle"], fixture.Calls);
        Assert.Equal(0, fixture.Store.DeleteCount);
    }

    [Fact]
    public async Task ResumeAsync_DeleteFailurePropagatesWithoutResumeOrDeleteRetry()
    {
        var fixture = new Fixture();
        fixture.Store.LoadedState = PersistedState();
        fixture.Resumer.Result = ResumeLifecycle(
            DeveloperLifecycleState.Completed,
            fixture.Store.LoadedState.ResumeContext);
        var expected = new IOException("delete failed");
        fixture.Store.DeleteException = expected;

        var exception = await Assert.ThrowsAsync<IOException>(() => fixture.Service.ResumeAsync(ResumeRequest()));

        Assert.Same(expected, exception);
        Assert.Equal(["load", "resume-lifecycle", "delete"], fixture.Calls);
        Assert.Equal(1, fixture.Resumer.CallCount);
        Assert.Equal(1, fixture.Store.DeleteCount);
    }

    [Theory]
    [InlineData("start-lifecycle")]
    [InlineData("save")]
    [InlineData("load")]
    [InlineData("resume-lifecycle")]
    [InlineData("delete")]
    public async Task CancellationAtAnyInvokedDependencyPreventsSubsequentOperations(string phase)
    {
        var fixture = new Fixture();
        fixture.Orchestrator.Result = Lifecycle(DeveloperLifecycleState.Pending);
        fixture.Store.LoadedState = PersistedState();
        fixture.Resumer.Result = ResumeLifecycle(
            DeveloperLifecycleState.Completed,
            fixture.Store.LoadedState.ResumeContext);
        fixture.SetException(phase, new OperationCanceledException());

        if (phase is "start-lifecycle" or "save")
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Service.StartAsync(StartRequest()));
        else
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Service.ResumeAsync(ResumeRequest()));

        Assert.Equal(1, phase switch
        {
            "start-lifecycle" => fixture.Orchestrator.CallCount,
            "save" => fixture.Store.SaveCount,
            "load" => fixture.Store.LoadCount,
            "resume-lifecycle" => fixture.Resumer.CallCount,
            _ => fixture.Store.DeleteCount
        });
    }

    [Fact]
    public void StartResult_EnforcesLifecyclePersistenceInvariants()
    {
        var pending = Lifecycle(DeveloperLifecycleState.Pending);
        var failed = Lifecycle(DeveloperLifecycleState.Failed);
        var completed = Lifecycle(DeveloperLifecycleState.Completed);
        var state = PersistedState();

        Assert.Throws<ArgumentException>(() => new PersistedDeveloperLifecycleStartResult(pending));
        Assert.Throws<ArgumentException>(() => new PersistedDeveloperLifecycleStartResult(failed, state));
        Assert.Throws<ArgumentException>(() => new PersistedDeveloperLifecycleStartResult(completed, state));
        Assert.Same(state, new PersistedDeveloperLifecycleStartResult(pending, state).PersistedState);
    }

    [Fact]
    public void ResumeResult_EnforcesAllStateInvariants()
    {
        var state = PersistedState();
        var pending = ResumeLifecycle(DeveloperLifecycleState.Pending, state.ResumeContext);
        var failed = ResumeLifecycle(DeveloperLifecycleState.Failed, state.ResumeContext);
        var completed = ResumeLifecycle(DeveloperLifecycleState.Completed, state.ResumeContext);

        Assert.Throws<ArgumentException>(() => new PersistedDeveloperLifecycleResumeResult(
            PersistedDeveloperLifecycleResumeState.NotFound, "task", state, pending));
        Assert.Throws<ArgumentException>(() => new PersistedDeveloperLifecycleResumeResult(
            PersistedDeveloperLifecycleResumeState.Pending, "task", null, pending));
        Assert.Throws<ArgumentException>(() => new PersistedDeveloperLifecycleResumeResult(
            PersistedDeveloperLifecycleResumeState.Pending, "task", state, failed));
        Assert.Throws<ArgumentException>(() => new PersistedDeveloperLifecycleResumeResult(
            PersistedDeveloperLifecycleResumeState.Failed, "task", state, pending));
        Assert.Throws<ArgumentException>(() => new PersistedDeveloperLifecycleResumeResult(
            PersistedDeveloperLifecycleResumeState.Completed, "task", state, pending));
        Assert.Throws<ArgumentException>(() => new PersistedDeveloperLifecycleResumeResult(
            PersistedDeveloperLifecycleResumeState.Completed, "task", null, completed));
    }

    private static PersistedDeveloperLifecycleStartRequest StartRequest(
        string taskId = "Exact-Task-ID",
        string? taskFilePath = "Exact/task-file.md") => new(
            taskId, taskFilePath, "Exact/developer-task.md", "Exact/repository", "Exact.Repository",
            "Exact commit", "Exact-Remote", true, new GitHubRepositoryIdentity("ExactOwner", "ExactRepository"),
            "Exact-Base", "Exact PR body", true, PullRequestMergeMethod.Rebase,
            "Exact merge title", "Exact merge message", true);

    private static PersistedDeveloperLifecycleResumeRequest ResumeRequest(string taskId = "Exact-Task-ID") =>
        new(taskId, PullRequestMergeMethod.Squash, "Resume title", "Resume message", true);

    private static DeveloperLifecyclePersistedState PersistedState(string taskId = "Exact-Task-ID") => new(
        taskId,
        "Exact/task-file.md",
        new DeveloperLifecycleResumeContext(
            "Exact/repository", new GitHubRepositoryIdentity("ExactOwner", "ExactRepository"),
            73, "workflow/authoritative-feature", "Exact-Base", "Exact-Remote"),
        new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero));

    private static DeveloperLifecycleResult Lifecycle(DeveloperLifecycleState state)
    {
        var workflow = Workflow();
        var status = Status(state switch
        {
            DeveloperLifecycleState.Pending => PullRequestGateState.Pending,
            DeveloperLifecycleState.Failed => PullRequestGateState.Failed,
            _ => PullRequestGateState.Successful
        });
        return state == DeveloperLifecycleState.Completed
            ? new DeveloperLifecycleResult(state, workflow, status, GatedMerge(), Cleanup())
            : new DeveloperLifecycleResult(state, workflow, status);
    }

    private static DeveloperLifecycleResumeResult ResumeLifecycle(
        DeveloperLifecycleState state,
        DeveloperLifecycleResumeContext context)
    {
        var status = Status(state switch
        {
            DeveloperLifecycleState.Pending => PullRequestGateState.Pending,
            DeveloperLifecycleState.Failed => PullRequestGateState.Failed,
            _ => PullRequestGateState.Successful
        });
        return state == DeveloperLifecycleState.Completed
            ? new DeveloperLifecycleResumeResult(state, context, status, GatedMerge(), Cleanup())
            : new DeveloperLifecycleResumeResult(state, context, status);
    }

    private static DeveloperTaskWorkflowResult Workflow()
    {
        var id = new DeveloperTaskId(20);
        var completion = new DeveloperTaskCompletionResult(
            id, "title", "root", "workflow/authoritative-feature", "commit", "message",
            "remote", true, "task.md", "review.md");
        return new DeveloperTaskWorkflowResult(
            id,
            new DeveloperTaskGatedCompletionResult(
                id,
                new DeveloperReviewValidationResult(id, DeveloperReviewStatus.ReadyForReview, [], []),
                completion),
            new PullRequestEnsureResult(
                new PullRequestInfo(73, new Uri("https://example.invalid/pr/73"),
                    "title", "feature", "base", false), true));
    }

    private static PullRequestStatusGateResult Status(PullRequestGateState state) =>
        new(73, "head", state, []);
    private static PullRequestGatedMergeResult GatedMerge() => new(
        Status(PullRequestGateState.Successful),
        new PullRequestMergeResult(73, true, "merge", PullRequestMergeMethod.Squash));
    private static PostMergeCleanupResult Cleanup() => new(
        "root", "base", "feature", true, true);

    private sealed class Fixture
    {
        public Fixture(DateTimeOffset? utcNow = null)
        {
            Clock = new FakeClock(utcNow ?? new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero));
            Orchestrator = new FakeOrchestrator(Calls);
            Resumer = new FakeResumer(Calls);
            Store = new FakeStore(Calls);
            Service = new PersistedDeveloperLifecycle(Orchestrator, Resumer, Store, Clock);
        }

        public List<string> Calls { get; } = [];
        public FakeClock Clock { get; }
        public FakeOrchestrator Orchestrator { get; }
        public FakeResumer Resumer { get; }
        public FakeStore Store { get; }
        public PersistedDeveloperLifecycle Service { get; }

        public void SetException(string phase, Exception exception)
        {
            if (phase == "start-lifecycle") Orchestrator.Exception = exception;
            else if (phase == "save") Store.SaveException = exception;
            else if (phase == "load") Store.LoadException = exception;
            else if (phase == "resume-lifecycle") Resumer.Exception = exception;
            else Store.DeleteException = exception;
        }
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IUtcClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class FakeOrchestrator(IList<string> calls) : IDeveloperLifecycleOrchestrator
    {
        public DeveloperLifecycleResult Result { get; set; } = Lifecycle(DeveloperLifecycleState.Pending);
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
        public PullRequestMergeMethod MergeMethod { get; private set; }
        public string? MergeTitle { get; private set; }
        public string? MergeMessage { get; private set; }
        public bool DeleteRemoteBranch { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<DeveloperLifecycleResult> ExecuteAsync(
            string developerTaskFilePath, string repositoryDirectoryPath, string expectedRepositoryName,
            string commitMessage, string gitRemoteName, bool setUpstream,
            GitHubRepositoryIdentity gitHubRepository, string pullRequestBaseBranch,
            string? pullRequestBody, bool pullRequestDraft, PullRequestMergeMethod mergeMethod,
            string? mergeCommitTitle, string? mergeCommitMessage, bool deleteRemoteBranch,
            CancellationToken cancellationToken = default)
        {
            calls.Add("start-lifecycle"); CallCount++;
            TaskFilePath = developerTaskFilePath; RepositoryDirectory = repositoryDirectoryPath;
            ExpectedRepositoryName = expectedRepositoryName; CommitMessage = commitMessage;
            GitRemoteName = gitRemoteName; SetUpstream = setUpstream; Repository = gitHubRepository;
            BaseBranch = pullRequestBaseBranch; PullRequestBody = pullRequestBody;
            PullRequestDraft = pullRequestDraft; MergeMethod = mergeMethod;
            MergeTitle = mergeCommitTitle; MergeMessage = mergeCommitMessage;
            DeleteRemoteBranch = deleteRemoteBranch; CancellationToken = cancellationToken;
            return Exception is null ? Task.FromResult(Result) : Task.FromException<DeveloperLifecycleResult>(Exception);
        }
    }

    private sealed class FakeResumer(IList<string> calls) : IDeveloperLifecycleResumer
    {
        public DeveloperLifecycleResumeResult Result { get; set; } = ResumeLifecycle(
            DeveloperLifecycleState.Pending, PersistedState().ResumeContext);
        public Exception? Exception { get; set; }
        public int CallCount { get; private set; }
        public DeveloperLifecycleResumeContext? Context { get; private set; }
        public PullRequestMergeMethod MergeMethod { get; private set; }
        public string? MergeTitle { get; private set; }
        public string? MergeMessage { get; private set; }
        public bool DeleteRemoteBranch { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<DeveloperLifecycleResumeResult> ResumeAsync(
            DeveloperLifecycleResumeContext context, PullRequestMergeMethod mergeMethod,
            string? mergeCommitTitle, string? mergeCommitMessage, bool deleteRemoteBranch,
            CancellationToken cancellationToken = default)
        {
            calls.Add("resume-lifecycle"); CallCount++; Context = context; MergeMethod = mergeMethod;
            MergeTitle = mergeCommitTitle; MergeMessage = mergeCommitMessage;
            DeleteRemoteBranch = deleteRemoteBranch; CancellationToken = cancellationToken;
            return Exception is null ? Task.FromResult(Result) : Task.FromException<DeveloperLifecycleResumeResult>(Exception);
        }
    }

    private sealed class FakeStore(IList<string> calls) : IDeveloperLifecycleStateStore
    {
        public DeveloperLifecyclePersistedState? LoadedState { get; set; }
        public DeveloperLifecyclePersistedState? SavedState { get; private set; }
        public Exception? SaveException { get; set; }
        public Exception? LoadException { get; set; }
        public Exception? DeleteException { get; set; }
        public int SaveCount { get; private set; }
        public int LoadCount { get; private set; }
        public int DeleteCount { get; private set; }
        public string? LoadTaskId { get; private set; }
        public string? DeleteTaskId { get; private set; }
        public CancellationToken SaveCancellationToken { get; private set; }
        public CancellationToken LoadCancellationToken { get; private set; }
        public CancellationToken DeleteCancellationToken { get; private set; }

        public Task SaveAsync(DeveloperLifecyclePersistedState state, CancellationToken cancellationToken = default)
        {
            calls.Add("save"); SaveCount++; SavedState = state; SaveCancellationToken = cancellationToken;
            return SaveException is null ? Task.CompletedTask : Task.FromException(SaveException);
        }

        public Task<DeveloperLifecyclePersistedState?> LoadAsync(
            string taskId, CancellationToken cancellationToken = default)
        {
            calls.Add("load"); LoadCount++; LoadTaskId = taskId; LoadCancellationToken = cancellationToken;
            return LoadException is null
                ? Task.FromResult(LoadedState)
                : Task.FromException<DeveloperLifecyclePersistedState?>(LoadException);
        }

        public Task DeleteAsync(string taskId, CancellationToken cancellationToken = default)
        {
            calls.Add("delete"); DeleteCount++; DeleteTaskId = taskId; DeleteCancellationToken = cancellationToken;
            return DeleteException is null ? Task.CompletedTask : Task.FromException(DeleteException);
        }
    }
}
