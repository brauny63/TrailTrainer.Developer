using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;
using TrailTrainer.Developer.Host;
using Microsoft.Extensions.Options;

namespace TrailTrainer.Developer.Tests;

public sealed class CodexTaskExecutionIntegrationTests
{
    [Fact]
    public async Task AdapterStartupFailure_IsDeterministicWithoutLaunchingRealCodex()
    {
        var executor = new CodexCliTaskExecutor(Options.Create(new CodexExecutionOptions
        {
            ExecutablePath = Path.Combine(Path.GetTempPath(), $"missing-codex-{Guid.NewGuid():N}.exe")
        }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(
            new CodexTaskExecutionRequest(Path.GetTempPath(), "task.md")));

        Assert.Contains("could not be started", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreCancelledAdapterExecution_DoesNotStartAProcess()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var executor = new CodexCliTaskExecutor(Options.Create(new CodexExecutionOptions
        {
            ExecutablePath = "must-not-be-started"
        }));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync(
            new CodexTaskExecutionRequest(Path.GetTempPath(), "task.md"), cancellation.Token));
    }

    [Fact]
    public async Task FreshExecution_StartsBranchThenRunsCodexExactlyOnceBeforeCompletion()
    {
        var fixture = new Fixture();

        await fixture.ExecuteAsync();

        Assert.Equal(["parse", "load", "start", "save:BranchCreated", "codex", "save:CodexSucceeded", "complete", "pull-request", "delete"], fixture.Calls);
        Assert.Equal(1, fixture.Executor.Calls);
        Assert.Equal("repository", fixture.Executor.Request!.RepositoryPath);
        Assert.Equal("task.md", fixture.Executor.Request.DeveloperTaskFilePath);
        Assert.Contains("Do not commit and do not push", fixture.Executor.Request.Instruction, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CodexFailure_BlocksCompletionPushAndPullRequestAndRetainsRetryablePhase()
    {
        var fixture = new Fixture();
        fixture.Executor.Result = new CodexTaskExecutionResult(7, "output", "failure");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ExecuteAsync());

        Assert.Contains("exit code 7", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, fixture.Completer.Calls);
        Assert.Equal(0, fixture.PullRequests.Calls);
        Assert.Equal(CodexExecutionPhase.BranchCreated, fixture.Store.State!.Phase);
    }

    [Fact]
    public async Task CodexTimeout_BlocksCompletionAndRetainsRetryablePhase()
    {
        var fixture = new Fixture();
        fixture.Executor.Result = new CodexTaskExecutionResult(-1, "partial", "timeout", TimedOut: true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ExecuteAsync());

        Assert.Contains("timed out", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, fixture.Completer.Calls);
        Assert.Equal(0, fixture.PullRequests.Calls);
        Assert.Equal(CodexExecutionPhase.BranchCreated, fixture.Store.State!.Phase);
    }

    [Fact]
    public async Task SuccessfulPersistedCodexPhase_SkipsStarterAndDuplicateCodexExecution()
    {
        var fixture = new Fixture();
        fixture.Store.State = new CodexExecutionState("DEV-0048", "repository", "task.md", CodexExecutionPhase.CodexSucceeded);

        await fixture.ExecuteAsync();

        Assert.Equal(0, fixture.Starter.Calls);
        Assert.Equal(0, fixture.Executor.Calls);
        Assert.Equal(1, fixture.Completer.Calls);
    }

    [Fact]
    public async Task StarterFailure_PreventsCodexAndCompletion()
    {
        var fixture = new Fixture();
        fixture.Starter.Exception = new InvalidOperationException("dirty repository");

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ExecuteAsync());

        Assert.Equal(0, fixture.Executor.Calls);
        Assert.Equal(0, fixture.Completer.Calls);
        Assert.Null(fixture.Store.State);
    }

    [Fact]
    public async Task MissingOrInvalidReviewAfterSuccessfulCodex_IsSurfacedByExistingGate()
    {
        var fixture = new Fixture();
        fixture.Completer.Exception = new FileNotFoundException("review missing");

        await Assert.ThrowsAsync<FileNotFoundException>(() => fixture.ExecuteAsync());

        Assert.Equal(1, fixture.Executor.Calls);
        Assert.Equal(CodexExecutionPhase.CodexSucceeded, fixture.Store.State!.Phase);
        Assert.Equal(0, fixture.PullRequests.Calls);
    }

    private sealed class Fixture
    {
        public List<string> Calls { get; } = [];
        public FakeStarter Starter { get; }
        public FakeExecutor Executor { get; }
        public FakeStateStore Store { get; }
        public FakeCompleter Completer { get; }
        public FakePullRequests PullRequests { get; }
        private DeveloperTaskWorkflow Workflow { get; }

        public Fixture()
        {
            Starter = new FakeStarter(Calls);
            Executor = new FakeExecutor(Calls);
            Store = new FakeStateStore(Calls);
            Completer = new FakeCompleter(Calls);
            PullRequests = new FakePullRequests(Calls);
            Workflow = new DeveloperTaskWorkflow(
                new FakeParser(Calls), Completer, PullRequests, Starter, Executor, Store);
        }

        public Task<DeveloperTaskWorkflowResult> ExecuteAsync() => Workflow.ExecuteAsync(
            "task.md", "repository", "repository", "commit", "origin", true,
            new GitHubRepositoryIdentity("owner", "repository"), "main");
    }

    private sealed class FakeParser(IList<string> calls) : IDeveloperTaskParser
    {
        public Task<DeveloperTaskDocument> ParseAsync(string path, CancellationToken cancellationToken = default)
        {
            calls.Add("parse");
            return Task.FromResult(new DeveloperTaskDocument(new DeveloperTaskId(48), "Codex", path, "repository", "feature/dev-0048", "review.md"));
        }
    }

    private sealed class FakeStarter(IList<string> calls) : IDeveloperTaskStarter
    {
        public int Calls { get; private set; }
        public Exception? Exception { get; set; }
        public Task<DeveloperTaskStartResult> StartAsync(string task, string repository, string name, CancellationToken token = default)
        {
            Calls++; calls.Add("start");
            return Exception is null
                ? Task.FromResult(new DeveloperTaskStartResult(new DeveloperTaskId(48), "Codex", repository, "main", "feature/dev-0048", task, "review.md"))
                : Task.FromException<DeveloperTaskStartResult>(Exception);
        }
    }

    private sealed class FakeExecutor(IList<string> calls) : ICodexTaskExecutor
    {
        public int Calls { get; private set; }
        public CodexTaskExecutionRequest? Request { get; private set; }
        public CodexTaskExecutionResult Result { get; set; } = new(0, "ok", "");
        public Task<CodexTaskExecutionResult> ExecuteAsync(CodexTaskExecutionRequest request, CancellationToken token = default)
        { Calls++; Request = request; calls.Add("codex"); return Task.FromResult(Result); }
    }

    private sealed class FakeStateStore(IList<string> calls) : ICodexExecutionStateStore
    {
        public CodexExecutionState? State { get; set; }
        public Task<CodexExecutionState?> LoadAsync(string taskId, CancellationToken token = default)
        { calls.Add("load"); return Task.FromResult(State); }
        public Task SaveAsync(CodexExecutionState state, CancellationToken token = default)
        { State = state; calls.Add($"save:{state.Phase}"); return Task.CompletedTask; }
        public Task DeleteAsync(string taskId, CancellationToken token = default)
        { State = null; calls.Add("delete"); return Task.CompletedTask; }
    }

    private sealed class FakeCompleter(IList<string> calls) : IDeveloperTaskGatedCompleter
    {
        public int Calls { get; private set; }
        public Exception? Exception { get; set; }
        public Task<DeveloperTaskGatedCompletionResult> CompleteAsync(string task, string repository, string name, string message, string remote, bool upstream, CancellationToken token = default)
        {
            Calls++; calls.Add("complete");
            if (Exception is not null) return Task.FromException<DeveloperTaskGatedCompletionResult>(Exception);
            var id = new DeveloperTaskId(48);
            var completion = new DeveloperTaskCompletionResult(id, "Codex", repository, "feature/dev-0048", "sha", message, remote, upstream, task, "review.md");
            return Task.FromResult(new DeveloperTaskGatedCompletionResult(id, new DeveloperReviewValidationResult(id, DeveloperReviewStatus.ReadyForReview, [], []), completion));
        }
    }

    private sealed class FakePullRequests(IList<string> calls) : IPullRequestService
    {
        public int Calls { get; private set; }
        public Task<PullRequestEnsureResult> EnsureOpenAsync(GitHubRepositoryIdentity repository, string head, string @base, string title, string? body = null, bool draft = false, CancellationToken token = default)
        {
            Calls++; calls.Add("pull-request");
            return Task.FromResult(new PullRequestEnsureResult(new PullRequestInfo(48, new Uri("https://example.test/48"), title, head, @base, draft), true));
        }
    }
}
