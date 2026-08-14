using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class DeveloperTaskCompleterTests
{
    [Fact]
    public async Task CompleteAsync_ValidWorkflow_ReturnsResultsAndCallsDependenciesInOrder()
    {
        var calls = new List<string>();
        var task = CreateTask();
        using var cancellationSource = new CancellationTokenSource();
        var parser = new FakeTaskParser(task, calls);
        var status = new FakeStatusProvider(ValidStatus(), calls);
        var stager = new FakeStager(new GitStageResult("C:\\repository", true), calls);
        var committer = new FakeCommitter(
            new GitCommitResult("C:\\repository", "created-sha", "Commit result message"),
            calls);
        var pusher = new FakePusher(
            new GitPushResult("C:\\repository", "result-remote", task.ExpectedBranch, true),
            calls);
        var completer = new DeveloperTaskCompleter(parser, status, stager, committer, pusher);

        var result = await completer.CompleteAsync(
            task.FilePath,
            "C:\\repository\\nested",
            task.Repository,
            "Exact supplied message",
            "exact-remote",
            setUpstream: false,
            cancellationSource.Token);

        Assert.Equal(task.Id, result.TaskId);
        Assert.Equal(task.Title, result.TaskTitle);
        Assert.Equal("C:\\repository", result.RepositoryRoot);
        Assert.Equal(task.ExpectedBranch, result.BranchName);
        Assert.Equal("created-sha", result.CommitSha);
        Assert.Equal("Commit result message", result.CommitMessage);
        Assert.Equal("result-remote", result.RemoteName);
        Assert.True(result.SetUpstream);
        Assert.Equal(task.FilePath, result.TaskFilePath);
        Assert.Equal(task.ReviewReportPath, result.ReviewReportPath);
        Assert.Equal(["parse", "status", "stage", "commit", "push"], calls);
        Assert.Equal(1, committer.CallCount);
        Assert.Equal("Exact supplied message", committer.CommitMessage);
        Assert.Equal(1, pusher.CallCount);
        Assert.Equal("exact-remote", pusher.RemoteName);
        Assert.False(pusher.SetUpstream);
        Assert.Equal(cancellationSource.Token, parser.CancellationToken);
        Assert.Equal(cancellationSource.Token, status.CancellationToken);
        Assert.Equal(cancellationSource.Token, stager.CancellationToken);
        Assert.Equal(cancellationSource.Token, committer.CancellationToken);
        Assert.Equal(cancellationSource.Token, pusher.CancellationToken);
    }

    public static TheoryData<string?, string?, string?> InvalidInputs => new()
    {
        { null, "message", "origin" },
        { "   ", "message", "origin" },
        { "repository", null, "origin" },
        { "repository", "   ", "origin" },
        { "repository", "message", null },
        { "repository", "message", "   " }
    };

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public async Task CompleteAsync_InvalidSimpleInput_ThrowsBeforeParsing(
        string? repositoryName,
        string? commitMessage,
        string? remoteName)
    {
        var dependencies = CreateDependencies();
        var completer = dependencies.CreateCompleter();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => completer.CompleteAsync(
            "task.md",
            "repository",
            repositoryName!,
            commitMessage!,
            remoteName!,
            false));

        Assert.Equal(0, dependencies.Parser.CallCount);
        AssertNoMutation(dependencies);
    }

    [Fact]
    public async Task CompleteAsync_RepositoryMetadataMismatch_StopsBeforeMutation()
    {
        var dependencies = CreateDependencies(task: CreateTask(repository: "Actual.Repository"));
        var completer = dependencies.CreateCompleter();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => completer.CompleteAsync(
            "task.md", "repository", "Expected.Repository", "message", "origin", false));

        Assert.Contains("Actual.Repository", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Expected.Repository", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, dependencies.Status.CallCount);
        AssertNoMutation(dependencies);
    }

    public static TheoryData<GitRepositoryStatus> InvalidRepositoryStatuses => new()
    {
        GitRepositoryStatus.NotRepository,
        new GitRepositoryStatus(true, "C:\\repository", null, true),
        new GitRepositoryStatus(true, "C:\\repository", "feature/other", true),
        new GitRepositoryStatus(true, "C:\\repository", "feature/Exact-Task-Branch", false)
    };

    [Theory]
    [MemberData(nameof(InvalidRepositoryStatuses))]
    public async Task CompleteAsync_InvalidRepositoryPrecondition_StopsBeforeStaging(
        GitRepositoryStatus repositoryStatus)
    {
        var dependencies = CreateDependencies(repositoryStatus: repositoryStatus);
        var completer = dependencies.CreateCompleter();

        await Assert.ThrowsAsync<InvalidOperationException>(() => completer.CompleteAsync(
            "task.md", "repository", "TrailTrainer.Developer", "message", "origin", false));

        Assert.Equal(0, dependencies.Stager.CallCount);
        Assert.Equal(0, dependencies.Committer.CallCount);
        Assert.Equal(0, dependencies.Pusher.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_NoStagedChanges_StopsBeforeCommit()
    {
        var dependencies = CreateDependencies(
            stageResult: new GitStageResult("C:\\repository", false));
        var completer = dependencies.CreateCompleter();

        await Assert.ThrowsAsync<InvalidOperationException>(() => completer.CompleteAsync(
            "task.md", "repository", "TrailTrainer.Developer", "message", "origin", false));

        Assert.Equal(1, dependencies.Stager.CallCount);
        Assert.Equal(0, dependencies.Committer.CallCount);
        Assert.Equal(0, dependencies.Pusher.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_StagingFails_StopsBeforeCommitAndPush()
    {
        var dependencies = CreateDependencies();
        dependencies.Stager.Exception = new InvalidOperationException("Stage failed");
        var completer = dependencies.CreateCompleter();

        await Assert.ThrowsAsync<InvalidOperationException>(() => completer.CompleteAsync(
            "task.md", "repository", "TrailTrainer.Developer", "message", "origin", false));

        Assert.Equal(0, dependencies.Committer.CallCount);
        Assert.Equal(0, dependencies.Pusher.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_CommitFails_StopsBeforePush()
    {
        var dependencies = CreateDependencies();
        dependencies.Committer.Exception = new InvalidOperationException("Commit failed");
        var completer = dependencies.CreateCompleter();

        await Assert.ThrowsAsync<InvalidOperationException>(() => completer.CompleteAsync(
            "task.md", "repository", "TrailTrainer.Developer", "message", "origin", false));

        Assert.Equal(1, dependencies.Committer.CallCount);
        Assert.Equal(0, dependencies.Pusher.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_PushFails_PropagatesFailure()
    {
        var dependencies = CreateDependencies();
        dependencies.Pusher.Exception = new InvalidOperationException("Push failed");
        var completer = dependencies.CreateCompleter();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => completer.CompleteAsync(
            "task.md", "repository", "TrailTrainer.Developer", "message", "origin", false));

        Assert.Equal("Push failed", exception.Message);
        Assert.Equal(1, dependencies.Pusher.CallCount);
    }

    private static TestDependencies CreateDependencies(
        DeveloperTaskDocument? task = null,
        GitRepositoryStatus? repositoryStatus = null,
        GitStageResult? stageResult = null)
    {
        task ??= CreateTask();
        return new TestDependencies(
            new FakeTaskParser(task),
            new FakeStatusProvider(repositoryStatus ?? ValidStatus()),
            new FakeStager(stageResult ?? new GitStageResult("C:\\repository", true)),
            new FakeCommitter(new GitCommitResult("C:\\repository", "sha", "message")),
            new FakePusher(new GitPushResult(
                "C:\\repository", "origin", task.ExpectedBranch, false)));
    }

    private static void AssertNoMutation(TestDependencies dependencies)
    {
        Assert.Equal(0, dependencies.Stager.CallCount);
        Assert.Equal(0, dependencies.Committer.CallCount);
        Assert.Equal(0, dependencies.Pusher.CallCount);
    }

    private static DeveloperTaskDocument CreateTask(string repository = "TrailTrainer.Developer") => new(
        new DeveloperTaskId(8),
        "Complete Developer Task Workflow",
        "C:\\tasks\\DEV-0008-Task.md",
        repository,
        "feature/Exact-Task-Branch",
        "docs/developer-reviews/REVIEW-0008.md");

    private static GitRepositoryStatus ValidStatus() => new(
        true, "C:\\repository", "feature/Exact-Task-Branch", true);

    private sealed record TestDependencies(
        FakeTaskParser Parser,
        FakeStatusProvider Status,
        FakeStager Stager,
        FakeCommitter Committer,
        FakePusher Pusher)
    {
        public DeveloperTaskCompleter CreateCompleter() => new(
            Parser, Status, Stager, Committer, Pusher);
    }

    private sealed class FakeTaskParser : IDeveloperTaskParser
    {
        private readonly DeveloperTaskDocument result;
        private readonly IList<string>? calls;
        public FakeTaskParser(DeveloperTaskDocument result, IList<string>? calls = null)
            => (this.result, this.calls) = (result, calls);
        public int CallCount { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public Task<DeveloperTaskDocument> ParseAsync(string path, CancellationToken token = default)
        {
            CallCount++; CancellationToken = token; calls?.Add("parse"); return Task.FromResult(result);
        }
    }

    private sealed class FakeStatusProvider : IGitRepositoryStatusProvider
    {
        private readonly GitRepositoryStatus result;
        private readonly IList<string>? calls;
        public FakeStatusProvider(GitRepositoryStatus result, IList<string>? calls = null)
            => (this.result, this.calls) = (result, calls);
        public int CallCount { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public Task<GitRepositoryStatus> GetStatusAsync(string path, CancellationToken token = default)
        {
            CallCount++; CancellationToken = token; calls?.Add("status"); return Task.FromResult(result);
        }
    }

    private sealed class FakeStager : IGitStager
    {
        private readonly GitStageResult result;
        private readonly IList<string>? calls;
        public FakeStager(GitStageResult result, IList<string>? calls = null)
            => (this.result, this.calls) = (result, calls);
        public Exception? Exception { get; set; }
        public int CallCount { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public Task<GitStageResult> StageAllAsync(string path, CancellationToken token = default)
        {
            CallCount++; CancellationToken = token; calls?.Add("stage");
            return Exception is null ? Task.FromResult(result) : Task.FromException<GitStageResult>(Exception);
        }
    }

    private sealed class FakeCommitter : IGitCommitter
    {
        private readonly GitCommitResult result;
        private readonly IList<string>? calls;
        public FakeCommitter(GitCommitResult result, IList<string>? calls = null)
            => (this.result, this.calls) = (result, calls);
        public Exception? Exception { get; set; }
        public int CallCount { get; private set; }
        public string? CommitMessage { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public Task<GitCommitResult> CommitAsync(string path, string message, CancellationToken token = default)
        {
            CallCount++; CommitMessage = message; CancellationToken = token; calls?.Add("commit");
            return Exception is null ? Task.FromResult(result) : Task.FromException<GitCommitResult>(Exception);
        }
    }

    private sealed class FakePusher : IGitPusher
    {
        private readonly GitPushResult result;
        private readonly IList<string>? calls;
        public FakePusher(GitPushResult result, IList<string>? calls = null)
            => (this.result, this.calls) = (result, calls);
        public Exception? Exception { get; set; }
        public int CallCount { get; private set; }
        public string? RemoteName { get; private set; }
        public bool SetUpstream { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public Task<GitPushResult> PushAsync(
            string path, string remoteName, bool setUpstream, CancellationToken token = default)
        {
            CallCount++; RemoteName = remoteName; SetUpstream = setUpstream;
            CancellationToken = token; calls?.Add("push");
            return Exception is null ? Task.FromResult(result) : Task.FromException<GitPushResult>(Exception);
        }
    }
}
