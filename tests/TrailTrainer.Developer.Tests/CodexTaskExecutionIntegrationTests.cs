using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;
using TrailTrainer.Developer.Host;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using TrailTrainer.Developer.FakeCodexCli;
using System.Runtime.InteropServices;

namespace TrailTrainer.Developer.Tests;

public sealed class CodexTaskExecutionIntegrationTests
{
    [Fact]
    public async Task FakeExecutableHost_UsesWorkingDirectoryProfileArgumentAndCapturesOutputAndExitCode()
    {
        var repository = Path.Combine(Path.GetTempPath(), $"codex-host-{Guid.NewGuid():N}");
        var profile = Path.Combine(repository, "profile");
        Directory.CreateDirectory(repository);
        try
        {
            var helper = typeof(Program).Assembly.Location;
            var executor = new CodexCliTaskExecutor(Options.Create(new CodexExecutionOptions
            {
                ExecutablePath = DotNetHostPath,
                AdditionalArguments = [helper],
                UserProfileDirectory = profile
            }));

            var result = await executor.ExecuteAsync(new CodexTaskExecutionRequest(repository, "exit-23"));

            Assert.Equal(23, result.ExitCode);
            Assert.Contains($"cwd={repository}", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("instruction=Work the Developer Task at exit-23 completely.", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"USERPROFILE={Path.GetFullPath(profile)}", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains($"APPDATA={Path.Combine(profile, "AppData", "Roaming")}", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("fake-codex-stderr", result.StandardError, StringComparison.Ordinal);
            Assert.Contains("--sandbox|workspace-write|--ask-for-approval|never|--skip-git-repo-check", result.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public async Task ConfiguredCompatibilityArguments_ArePassedStructurally()
    {
        var repository = Path.Combine(Path.GetTempPath(), $"codex-mode-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repository);
        try
        {
            var executor = new CodexCliTaskExecutor(Options.Create(new CodexExecutionOptions
            {
                ExecutablePath = DotNetHostPath,
                AdditionalArguments = [typeof(Program).Assembly.Location],
                SandboxMode = "danger-full-access",
                ApprovalPolicy = "never",
                UserProfileDirectory = repository
            }));
            var result = await executor.ExecuteAsync(new CodexTaskExecutionRequest(repository, "mode"));
            Assert.Contains("--sandbox|danger-full-access|--ask-for-approval|never", result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("dangerously-bypass-approvals-and-sandbox", result.StandardOutput, StringComparison.Ordinal);
        }
        finally { Directory.Delete(repository, recursive: true); }
    }

    [Fact]
    public async Task RunnerPipeTimeout_IsClassifiedDistinctly()
    {
        var executor = new CodexCliTaskExecutor(Options.Create(new CodexExecutionOptions
        {
            ExecutablePath = DotNetHostPath,
            AdditionalArguments = [typeof(Program).Assembly.Location, "fake-runner-pipe-timeout"],
            UserProfileDirectory = Path.GetTempPath()
        }));
        var result = await executor.ExecuteAsync(new CodexTaskExecutionRequest(Path.GetTempPath(), "pipe"));
        Assert.Equal(CodexExecutionFailureKind.RunnerPipeTimeout, result.FailureKind);
    }

    [Fact]
    public async Task CompatibilityProbe_UsesTemporaryNonRepositoryAndShortIndependentTimeout()
    {
        var executor = new CodexCliTaskExecutor(Options.Create(new CodexExecutionOptions
        {
            ExecutablePath = DotNetHostPath,
            AdditionalArguments = [typeof(Program).Assembly.Location, "fake-probe-timeout"],
            UserProfileDirectory = Path.GetTempPath(),
            Timeout = TimeSpan.FromMinutes(1),
            CompatibilityProbeTimeout = TimeSpan.FromMilliseconds(200)
        }));
        var result = await executor.ProbeAsync();
        Assert.True(result.TimedOut);
    }

    [Fact]
    public async Task Diagnostics_AreBoundedAndDoNotEnumerateSecretLikeEnvironmentValues()
    {
        var repository = Path.Combine(Path.GetTempPath(), $"codex-logs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repository);
        var logs = new ListLogger<CodexCliTaskExecutor>();
        var secretName = $"DEV0051_SECRET_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(secretName, "credential-must-not-be-logged");
        try
        {
            var executor = new CodexCliTaskExecutor(Options.Create(new CodexExecutionOptions
            {
                ExecutablePath = DotNetHostPath,
                AdditionalArguments = [typeof(Program).Assembly.Location],
                UserProfileDirectory = Path.Combine(repository, new string('p', 80)),
                MaximumDiagnosticCharacters = 64
            }), logs);

            await executor.ExecuteAsync(new CodexTaskExecutionRequest(repository, "bounded"));

            var combined = string.Join(Environment.NewLine, logs.Messages);
            Assert.DoesNotContain(secretName, combined, StringComparison.Ordinal);
            Assert.DoesNotContain("credential-must-not-be-logged", combined, StringComparison.Ordinal);
            Assert.All(logs.StateValues.Where(value => value.Key is "Environment" or "StandardOutput" or "StandardError"),
                value => Assert.True(value.Value?.ToString()?.Length <= 64));
        }
        finally
        {
            Environment.SetEnvironmentVariable(secretName, null);
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public async Task Timeout_KillsTheFakeCliProcessTree()
    {
        var repository = Path.Combine(Path.GetTempPath(), $"codex-timeout-{Guid.NewGuid():N}");
        var marker = Path.Combine(repository, "child-marker");
        Directory.CreateDirectory(repository);
        try
        {
            var executor = new CodexCliTaskExecutor(Options.Create(new CodexExecutionOptions
            {
                ExecutablePath = DotNetHostPath,
                AdditionalArguments = [typeof(Program).Assembly.Location],
                UserProfileDirectory = repository,
                Timeout = TimeSpan.FromMilliseconds(300)
            }));

            var result = await executor.ExecuteAsync(
                new CodexTaskExecutionRequest(repository, $"spawn-child:{marker}"));
            await Task.Delay(TimeSpan.FromSeconds(3));

            Assert.True(result.TimedOut);
            Assert.Equal(-1, result.ExitCode);
            Assert.False(File.Exists(marker));
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public async Task Cancellation_KillsTheRunningFakeCliAndPropagatesCancellation()
    {
        var repository = Path.Combine(Path.GetTempPath(), $"codex-cancel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repository);
        try
        {
            var executor = new CodexCliTaskExecutor(Options.Create(new CodexExecutionOptions
            {
                ExecutablePath = DotNetHostPath,
                AdditionalArguments = [typeof(Program).Assembly.Location],
                UserProfileDirectory = repository,
                Timeout = TimeSpan.FromMinutes(1)
            }));
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync(
                new CodexTaskExecutionRequest(repository, $"spawn-child:{Path.Combine(repository, "cancel-marker")}"),
                cancellation.Token));
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

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

        Assert.Equal(["parse", "load", "status", "start", "save:BranchCreated", "codex", "status", "review-parse", "review-validate", "save:CodexSucceeded", "complete", "pull-request", "delete"], fixture.Calls);
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

        var exception = await Assert.ThrowsAsync<DeveloperTaskExecutionException>(() => fixture.ExecuteAsync());

        Assert.Contains("exit code 7", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, fixture.Completer.Calls);
        Assert.Equal(0, fixture.PullRequests.Calls);
        Assert.Equal(CodexExecutionPhase.BranchCreated, fixture.Store.State!.Phase);
    }

    [Fact]
    public async Task RunnerPipeFailure_IsDistinctFromMissingReview()
    {
        var fixture = new Fixture();
        fixture.Executor.Result = new CodexTaskExecutionResult(1, "", "runner pipe connection timed out", false,
            CodexExecutionFailureKind.RunnerPipeTimeout);
        var exception = await Assert.ThrowsAsync<DeveloperTaskExecutionException>(() => fixture.ExecuteAsync());
        Assert.Contains("sandbox runner pipe timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("review", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CodexExecutionPhase.BranchCreated, fixture.Store.State!.Phase);
    }

    [Fact]
    public async Task CodexTimeout_BlocksCompletionAndRetainsRetryablePhase()
    {
        var fixture = new Fixture();
        fixture.Executor.Result = new CodexTaskExecutionResult(-1, "partial", "timeout", TimedOut: true);

        var exception = await Assert.ThrowsAsync<DeveloperTaskExecutionException>(() => fixture.ExecuteAsync());

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
    public async Task Dev0007Regression_ExitZeroWithoutReview_RemainsRetryableAndNeverCompletes()
    {
        var fixture = new Fixture(7);
        fixture.Executor.Result = new CodexTaskExecutionResult(0, "codex useful output", "codex useful error");
        fixture.ReviewParser.Exception = new FileNotFoundException(
            "review missing",
            "docs/developer-reviews/REVIEW-0007.md");

        var exception = await Assert.ThrowsAsync<DeveloperTaskExecutionException>(() => fixture.ExecuteAsync());

        Assert.Contains("DEV-0007", exception.Message, StringComparison.Ordinal);
        Assert.Contains("REVIEW-0007.md", exception.Message, StringComparison.Ordinal);
        Assert.Contains("missing", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("codex useful output", exception.Message, StringComparison.Ordinal);
        Assert.Contains("codex useful error", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, fixture.Executor.Calls);
        Assert.Equal(CodexExecutionPhase.BranchCreated, fixture.Store.State!.Phase);
        Assert.Equal(0, fixture.Completer.Calls);
        Assert.Equal(0, fixture.PullRequests.Calls);
    }

    [Fact]
    public async Task MissingReview_OnStillCleanExpectedBranch_RetriesDeterministically()
    {
        var fixture = new Fixture();
        fixture.ReviewParser.Exception = new FileNotFoundException("review missing", "review.md");

        await Assert.ThrowsAsync<DeveloperTaskExecutionException>(() => fixture.ExecuteAsync());
        fixture.ReviewParser.Exception = null;

        await fixture.ExecuteAsync();

        Assert.Equal(1, fixture.Starter.Calls);
        Assert.Equal(2, fixture.Executor.Calls);
        Assert.Equal(1, fixture.Completer.Calls);
        Assert.Equal(1, fixture.PullRequests.Calls);
    }

    [Fact]
    public async Task ExitZeroWithInvalidReview_DoesNotPersistSuccessOrComplete()
    {
        var fixture = new Fixture();
        fixture.ReviewValidator.Result = new DeveloperReviewValidationResult(
            new DeveloperTaskId(48), DeveloperReviewStatus.Blocked, ["Review status is BLOCKED."], []);

        var exception = await Assert.ThrowsAsync<DeveloperTaskExecutionException>(() => fixture.ExecuteAsync());

        Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CodexExecutionPhase.BranchCreated, fixture.Store.State!.Phase);
        Assert.Equal(0, fixture.Completer.Calls);
        Assert.Equal(0, fixture.PullRequests.Calls);
    }

    [Theory]
    [InlineData("feature/unrelated", false)]
    [InlineData("feature/dev-0048", true)]
    public async Task RetryOnUnexpectedOrDirtyBranch_BlocksWithoutCodexOrCleanup(string branch, bool dirty)
    {
        var fixture = new Fixture();
        fixture.Store.State = new CodexExecutionState("DEV-0048", "repository", "task.md", CodexExecutionPhase.BranchCreated);
        fixture.Status.Result = new GitRepositoryStatus(true, "repository", branch, dirty);

        await Assert.ThrowsAsync<DeveloperTaskExecutionException>(() => fixture.ExecuteAsync());

        Assert.Equal(0, fixture.Starter.Calls);
        Assert.Equal(0, fixture.Executor.Calls);
        Assert.Equal(CodexExecutionPhase.BranchCreated, fixture.Store.State.Phase);
    }

    private sealed class Fixture
    {
        public List<string> Calls { get; } = [];
        public FakeStarter Starter { get; }
        public FakeExecutor Executor { get; }
        public FakeStateStore Store { get; }
        public FakeCompleter Completer { get; }
        public FakePullRequests PullRequests { get; }
        public FakeStatusProvider Status { get; }
        public FakeReviewParser ReviewParser { get; }
        public FakeReviewValidator ReviewValidator { get; }
        private DeveloperTaskWorkflow Workflow { get; }

        public Fixture(int taskNumber = 48)
        {
            var expectedBranch = $"feature/dev-{taskNumber:0000}";
            Starter = new FakeStarter(Calls);
            Executor = new FakeExecutor(Calls);
            Store = new FakeStateStore(Calls);
            Completer = new FakeCompleter(Calls);
            PullRequests = new FakePullRequests(Calls);
            Status = new FakeStatusProvider(Calls, expectedBranch);
            ReviewParser = new FakeReviewParser(Calls, taskNumber);
            ReviewValidator = new FakeReviewValidator(Calls, taskNumber);
            Workflow = new DeveloperTaskWorkflow(
                new FakeParser(Calls, taskNumber), Completer, PullRequests, Starter, Executor, Store, Status,
                ReviewParser, ReviewValidator);
        }

        public Task<DeveloperTaskWorkflowResult> ExecuteAsync() => Workflow.ExecuteAsync(
            "task.md", "repository", "repository", "commit", "origin", true,
            new GitHubRepositoryIdentity("owner", "repository"), "main");
    }

    [Fact]
    public async Task InterruptedStart_OnCleanExpectedBranch_ReconstructsStateWithoutCreatingBranchAgain()
    {
        var fixture = new Fixture();
        fixture.Status.Result = new GitRepositoryStatus(true, "repository", "feature/dev-0048", false);

        await fixture.ExecuteAsync();

        Assert.Equal(0, fixture.Starter.Calls);
        Assert.Equal(1, fixture.Executor.Calls);
        Assert.Contains("save:BranchCreated", fixture.Calls);
        Assert.Equal(1, fixture.Completer.Calls);
    }

    [Fact]
    public async Task Dev0007Regression_InitialSaveFailsThenRestartRecoversWithoutSecondBranchCreation()
    {
        var fixture = new Fixture();
        fixture.Store.SaveExceptionOnce = new InvalidOperationException("initial state move failed");

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ExecuteAsync());
        Assert.Equal(1, fixture.Starter.Calls);
        Assert.Equal(0, fixture.Executor.Calls);
        Assert.Null(fixture.Store.State);

        fixture.Status.Result = new GitRepositoryStatus(true, "repository", "feature/dev-0048", false);
        await fixture.ExecuteAsync();

        Assert.Equal(1, fixture.Starter.Calls);
        Assert.Equal(1, fixture.Executor.Calls);
        Assert.Equal(1, fixture.Completer.Calls);
    }

    [Theory]
    [InlineData("feature/unrelated", false)]
    [InlineData("feature/dev-0048", true)]
    public async Task InterruptedStart_UnexpectedOrDirtyBranch_IsRejected(
        string branch,
        bool dirty)
    {
        var fixture = new Fixture();
        fixture.Status.Result = new GitRepositoryStatus(true, "repository", branch, dirty);
        fixture.Starter.Exception = new InvalidOperationException("starter rejected branch");

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ExecuteAsync());

        Assert.Equal(0, fixture.Executor.Calls);
        Assert.Null(fixture.Store.State);
    }

    private sealed class FakeStatusProvider : IGitRepositoryStatusProvider
    {
        private readonly IList<string> calls;
        private readonly Queue<GitRepositoryStatus> results = new();

        public FakeStatusProvider(IList<string> calls, string expectedBranch)
        {
            this.calls = calls;
            results.Enqueue(new GitRepositoryStatus(true, "repository", "main", false));
            results.Enqueue(new GitRepositoryStatus(true, "repository", expectedBranch, false));
        }

        public GitRepositoryStatus Result
        {
            set
            {
                results.Clear();
                results.Enqueue(value);
            }
        }

        public Task<GitRepositoryStatus> GetStatusAsync(string path, CancellationToken token = default)
        {
            calls.Add("status");
            var result = results.Count > 1 ? results.Dequeue() : results.Peek();
            return Task.FromResult(result);
        }
    }

    private sealed class FakeParser(IList<string> calls, int taskNumber) : IDeveloperTaskParser
    {
        public Task<DeveloperTaskDocument> ParseAsync(string path, CancellationToken cancellationToken = default)
        {
            calls.Add("parse");
            return Task.FromResult(new DeveloperTaskDocument(
                new DeveloperTaskId(taskNumber),
                "Codex",
                path,
                "repository",
                $"feature/dev-{taskNumber:0000}",
                $"docs/developer-reviews/REVIEW-{taskNumber:0000}.md"));
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
        public Exception? SaveExceptionOnce { get; set; }
        public Task<CodexExecutionState?> LoadAsync(string taskId, CancellationToken token = default)
        { calls.Add("load"); return Task.FromResult(State); }
        public Task SaveAsync(CodexExecutionState state, CancellationToken token = default)
        {
            calls.Add($"save:{state.Phase}");
            if (SaveExceptionOnce is not null)
            {
                var exception = SaveExceptionOnce;
                SaveExceptionOnce = null;
                return Task.FromException(exception);
            }
            State = state;
            return Task.CompletedTask;
        }
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

    private sealed class FakeReviewParser(IList<string> calls, int taskNumber) : IDeveloperReviewParser
    {
        public Exception? Exception { get; set; }

        public Task<DeveloperReviewDocument> ParseAsync(string path, CancellationToken token = default)
        {
            calls.Add("review-parse");
            if (Exception is not null) return Task.FromException<DeveloperReviewDocument>(Exception);
            return Task.FromResult(new DeveloperReviewDocument(
                new DeveloperTaskId(taskNumber), "Codex", path, DeveloperReviewStatus.ReadyForReview,
                "summary", [], [], [], [], "notes", [],
                new DeveloperReviewVerification(true, 0, 0, true, 1, 1, 0, true),
                "None", "None", false, false));
        }
    }

    private sealed class FakeReviewValidator(IList<string> calls, int taskNumber) : IDeveloperReviewValidator
    {
        public DeveloperReviewValidationResult Result { get; set; } = new(
            new DeveloperTaskId(taskNumber), DeveloperReviewStatus.ReadyForReview, [], []);

        public Task<DeveloperReviewValidationResult> ValidateAsync(
            DeveloperTaskDocument task,
            DeveloperReviewDocument review,
            CancellationToken token = default)
        {
            calls.Add("review-validate");
            return Task.FromResult(Result);
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

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public List<KeyValuePair<string, object?>> StateValues { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            if (state is IEnumerable<KeyValuePair<string, object?>> values) StateValues.AddRange(values);
        }
    }

    private static string DotNetHostPath => Path.GetFullPath(Path.Combine(
        RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
}
