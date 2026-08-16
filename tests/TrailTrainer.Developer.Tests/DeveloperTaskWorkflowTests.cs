using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class DeveloperTaskWorkflowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_Success_ReturnsExactResultsAndCallsDependenciesInOrder(bool created)
    {
        var fixture = new WorkflowFixture(pullRequestCreated: created);
        using var cancellationSource = new CancellationTokenSource();

        var result = await fixture.Workflow.ExecuteAsync(
            "Exact Task Path.md",
            "Exact Repository Directory",
            "Exact.Repository",
            "Exact commit message",
            "Exact-Git-Remote",
            setUpstream: false,
            fixture.RepositoryIdentity,
            "Exact-Base",
            "Exact PR body",
            pullRequestDraft: true,
            cancellationSource.Token);

        Assert.Equal(fixture.Task.Id, result.TaskId);
        Assert.Same(fixture.GatedCompletion, result.Completion);
        Assert.Same(fixture.PullRequestResult, result.PullRequest);
        Assert.Equal(created, result.PullRequest.Created);
        Assert.Equal(["parse", "complete", "pull-request"], fixture.Calls);
        Assert.Equal(1, fixture.GatedCompleter.CallCount);
        Assert.Equal("Exact Task Path.md", fixture.GatedCompleter.TaskFilePath);
        Assert.Equal("Exact Repository Directory", fixture.GatedCompleter.RepositoryDirectoryPath);
        Assert.Equal("Exact.Repository", fixture.GatedCompleter.ExpectedRepositoryName);
        Assert.Equal("Exact commit message", fixture.GatedCompleter.CommitMessage);
        Assert.Equal("Exact-Git-Remote", fixture.GatedCompleter.RemoteName);
        Assert.False(fixture.GatedCompleter.SetUpstream);
        Assert.Equal(1, fixture.PullRequestService.CallCount);
        Assert.Same(fixture.RepositoryIdentity, fixture.PullRequestService.Repository);
        Assert.Equal("Exact-Base", fixture.PullRequestService.BaseBranch);
        Assert.Equal("Exact PR body", fixture.PullRequestService.Body);
        Assert.True(fixture.PullRequestService.Draft);
        Assert.Equal("pushed/authoritative-branch", fixture.PullRequestService.HeadBranch);
        Assert.Equal("DEV-0013 – End-to-End Developer Workflow", fixture.PullRequestService.Title);
        Assert.DoesNotContain("Exact commit message", fixture.PullRequestService.Title, StringComparison.Ordinal);
        Assert.Equal(cancellationSource.Token, fixture.TaskParser.CancellationToken);
        Assert.Equal(cancellationSource.Token, fixture.GatedCompleter.CancellationToken);
        Assert.Equal(cancellationSource.Token, fixture.PullRequestService.CancellationToken);
    }

    [Theory]
    [InlineData("DEV-0013 – Existing title", "DEV-0013 – Existing title")]
    [InlineData("DEV-0013 - Existing title", "DEV-0013 - Existing title")]
    [InlineData("Plain title", "DEV-0013 – Plain title")]
    public async Task ExecuteAsync_PullRequestTitle_DoesNotDuplicateExistingTaskId(
        string taskTitle,
        string expectedTitle)
    {
        var fixture = new WorkflowFixture(taskTitle: taskTitle);

        await fixture.ExecuteAsync();

        Assert.Equal(expectedTitle, fixture.PullRequestService.Title);
        Assert.Equal(1, CountOccurrences(expectedTitle, "DEV-0013"));
    }

    [Fact]
    public async Task ExecuteAsync_TaskParserFailure_PreventsCompletionAndPullRequest()
    {
        var fixture = new WorkflowFixture();
        fixture.TaskParser.Exception = new InvalidDataException("Task parse failed");

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.ExecuteAsync());

        Assert.Equal(0, fixture.GatedCompleter.CallCount);
        Assert.Equal(0, fixture.PullRequestService.CallCount);
    }

    [Theory]
    [InlineData("Review gate failed")]
    [InlineData("Push failed")]
    public async Task ExecuteAsync_GatedCompletionFailure_PreventsPullRequest(string failure)
    {
        var fixture = new WorkflowFixture();
        fixture.GatedCompleter.Exception = new InvalidOperationException(failure);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ExecuteAsync());

        Assert.Equal(failure, exception.Message);
        Assert.Equal(1, fixture.GatedCompleter.CallCount);
        Assert.Equal(0, fixture.PullRequestService.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_PullRequestFailure_PropagatesWithoutRetryOrAnotherCompletion()
    {
        var fixture = new WorkflowFixture();
        fixture.PullRequestService.Exception = new HttpRequestException("PR failed");

        var exception = await Assert.ThrowsAsync<DeveloperTaskExecutionException>(() => fixture.ExecuteAsync());

        Assert.Equal("PR failed", exception.InnerException?.Message);
        Assert.Equal(1, fixture.GatedCompleter.CallCount);
        Assert.Equal(1, fixture.PullRequestService.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationDuringCompletion_PreventsPullRequest()
    {
        var fixture = new WorkflowFixture();
        fixture.GatedCompleter.Exception = new OperationCanceledException();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.ExecuteAsync());

        Assert.Equal(0, fixture.PullRequestService.CallCount);
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }

    private sealed class WorkflowFixture
    {
        public WorkflowFixture(bool pullRequestCreated = true, string taskTitle = "End-to-End Developer Workflow")
        {
            Task = new DeveloperTaskDocument(
                new DeveloperTaskId(13), taskTitle, "task.md", "repository",
                "feature/task-expected-branch", "review.md");
            var completion = new DeveloperTaskCompletionResult(
                Task.Id, Task.Title, "repository-root", "pushed/authoritative-branch",
                "sha", "commit", "origin", true, Task.FilePath, Task.ReviewReportPath);
            GatedCompletion = new DeveloperTaskGatedCompletionResult(
                Task.Id,
                new DeveloperReviewValidationResult(
                    Task.Id, DeveloperReviewStatus.ReadyForReview, [], []),
                completion);
            PullRequestResult = new PullRequestEnsureResult(
                new PullRequestInfo(
                    13, new Uri("https://example.test/pulls/13"), "title",
                    completion.BranchName, "main", false),
                pullRequestCreated);
            RepositoryIdentity = new GitHubRepositoryIdentity("owner", "repository");
            TaskParser = new FakeTaskParser(Task, Calls);
            GatedCompleter = new FakeGatedCompleter(GatedCompletion, Calls);
            PullRequestService = new FakePullRequestService(PullRequestResult, Calls);
            Workflow = new DeveloperTaskWorkflow(TaskParser, GatedCompleter, PullRequestService);
        }

        public List<string> Calls { get; } = [];
        public DeveloperTaskDocument Task { get; }
        public DeveloperTaskGatedCompletionResult GatedCompletion { get; }
        public PullRequestEnsureResult PullRequestResult { get; }
        public GitHubRepositoryIdentity RepositoryIdentity { get; }
        public FakeTaskParser TaskParser { get; }
        public FakeGatedCompleter GatedCompleter { get; }
        public FakePullRequestService PullRequestService { get; }
        public DeveloperTaskWorkflow Workflow { get; }

        public Task<DeveloperTaskWorkflowResult> ExecuteAsync() => Workflow.ExecuteAsync(
            "task.md", "repository", "repository", "commit", "origin", true,
            RepositoryIdentity, "main", "body", false);
    }

    private sealed class FakeTaskParser(DeveloperTaskDocument result, IList<string> calls) : IDeveloperTaskParser
    {
        public Exception? Exception { get; set; }
        public CancellationToken CancellationToken { get; private set; }
        public Task<DeveloperTaskDocument> ParseAsync(string path, CancellationToken token = default)
        {
            CancellationToken = token; calls.Add("parse");
            return Exception is null ? Task.FromResult(result) : Task.FromException<DeveloperTaskDocument>(Exception);
        }
    }

    private sealed class FakeGatedCompleter(
        DeveloperTaskGatedCompletionResult result,
        IList<string> calls) : IDeveloperTaskGatedCompleter
    {
        public Exception? Exception { get; set; }
        public int CallCount { get; private set; }
        public string? TaskFilePath { get; private set; }
        public string? RepositoryDirectoryPath { get; private set; }
        public string? ExpectedRepositoryName { get; private set; }
        public string? CommitMessage { get; private set; }
        public string? RemoteName { get; private set; }
        public bool SetUpstream { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public Task<DeveloperTaskGatedCompletionResult> CompleteAsync(
            string taskPath, string repositoryPath, string repositoryName, string commitMessage,
            string remoteName, bool setUpstream, CancellationToken token = default)
        {
            CallCount++; TaskFilePath = taskPath; RepositoryDirectoryPath = repositoryPath;
            ExpectedRepositoryName = repositoryName; CommitMessage = commitMessage;
            RemoteName = remoteName; SetUpstream = setUpstream; CancellationToken = token;
            calls.Add("complete");
            return Exception is null
                ? Task.FromResult(result)
                : Task.FromException<DeveloperTaskGatedCompletionResult>(Exception);
        }
    }

    private sealed class FakePullRequestService(
        PullRequestEnsureResult result,
        IList<string> calls) : IPullRequestService
    {
        public Exception? Exception { get; set; }
        public int CallCount { get; private set; }
        public GitHubRepositoryIdentity? Repository { get; private set; }
        public string? HeadBranch { get; private set; }
        public string? BaseBranch { get; private set; }
        public string? Title { get; private set; }
        public string? Body { get; private set; }
        public bool Draft { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public Task<PullRequestEnsureResult> EnsureOpenAsync(
            GitHubRepositoryIdentity repository, string head, string @base, string title,
            string? body = null, bool draft = false, CancellationToken token = default)
        {
            CallCount++; Repository = repository; HeadBranch = head; BaseBranch = @base;
            Title = title; Body = body; Draft = draft; CancellationToken = token;
            calls.Add("pull-request");
            return Exception is null
                ? Task.FromResult(result)
                : Task.FromException<PullRequestEnsureResult>(Exception);
        }
    }
}
