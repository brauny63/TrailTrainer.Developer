using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class DeveloperTaskGatedCompleterTests
{
    [Fact]
    public async Task CompleteAsync_ValidReview_CompletesInOrderAndReturnsExactResults()
    {
        var fixture = new GateFixture();
        using var cancellationSource = new CancellationTokenSource();

        var result = await fixture.Gate.CompleteAsync(
            fixture.Task.FilePath,
            fixture.NestedRepositoryDirectory,
            "Exact.Repository",
            "Exact commit message",
            "Exact-Remote",
            setUpstream: false,
            cancellationSource.Token);

        Assert.Equal(fixture.Task.Id, result.TaskId);
        Assert.Same(fixture.Validation, result.ReviewValidation);
        Assert.Same(fixture.Completion, result.Completion);
        Assert.Equal(["task", "review", "validate", "complete"], fixture.Calls);
        Assert.Equal(1, fixture.Completer.CallCount);
        Assert.Equal(fixture.Task.FilePath, fixture.Completer.TaskFilePath);
        Assert.Equal(fixture.NestedRepositoryDirectory, fixture.Completer.RepositoryDirectoryPath);
        Assert.Equal("Exact.Repository", fixture.Completer.ExpectedRepositoryName);
        Assert.Equal("Exact commit message", fixture.Completer.CommitMessage);
        Assert.Equal("Exact-Remote", fixture.Completer.RemoteName);
        Assert.False(fixture.Completer.SetUpstream);
        Assert.Equal(cancellationSource.Token, fixture.TaskParser.CancellationToken);
        Assert.Equal(cancellationSource.Token, fixture.ReviewParser.CancellationToken);
        Assert.Equal(cancellationSource.Token, fixture.Validator.CancellationToken);
        Assert.Equal(cancellationSource.Token, fixture.Completer.CancellationToken);
    }

    [Fact]
    public async Task CompleteAsync_RelativeReviewPathFromNestedDirectory_ResolvesAgainstRepositoryRoot()
    {
        var fixture = new GateFixture();

        await fixture.Gate.CompleteAsync(
            fixture.Task.FilePath,
            fixture.NestedRepositoryDirectory,
            "repository", "message", "origin", true);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(fixture.RepositoryRoot, fixture.Task.ReviewReportPath)),
            fixture.ReviewParser.FilePath);
    }

    [Fact]
    public async Task CompleteAsync_ReviewWarnings_DoNotBlockCompletion()
    {
        var fixture = new GateFixture(validation: new DeveloperReviewValidationResult(
            new DeveloperTaskId(11),
            DeveloperReviewStatus.ReadyForReview,
            [],
            ["Warning one", "Warning two"]));

        var result = await fixture.Gate.CompleteAsync(
            fixture.Task.FilePath, fixture.RepositoryRoot, "repository", "message", "origin", true);

        Assert.True(result.ReviewValidation.IsValid);
        Assert.Equal(2, result.ReviewValidation.Warnings.Count);
        Assert.Equal(1, fixture.Completer.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_InvalidReview_PreventsCompletionAndReportsEveryError()
    {
        var fixture = new GateFixture(validation: new DeveloperReviewValidationResult(
            new DeveloperTaskId(11),
            DeveloperReviewStatus.Blocked,
            ["First validation error", "Second validation error"],
            []));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Gate.CompleteAsync(
            fixture.Task.FilePath, fixture.RepositoryRoot, "repository", "message", "origin", true));

        Assert.Contains("First validation error", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Second validation error", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, fixture.Completer.CallCount);
    }

    [Theory]
    [InlineData("task")]
    [InlineData("review")]
    [InlineData("validator")]
    [InlineData("completer")]
    public async Task CompleteAsync_DependencyFailure_StopsOrPropagatesAtThatStage(string dependency)
    {
        var fixture = new GateFixture();
        fixture.SetFailure(dependency, new InvalidOperationException($"{dependency} failed"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Gate.CompleteAsync(
            fixture.Task.FilePath, fixture.RepositoryRoot, "repository", "message", "origin", true));

        Assert.Equal($"{dependency} failed", exception.Message);
        if (dependency == "task")
        {
            Assert.Equal(0, fixture.ReviewParser.CallCount);
        }
        if (dependency is "task" or "review")
        {
            Assert.Equal(0, fixture.Validator.CallCount);
        }
        if (dependency != "completer")
        {
            Assert.Equal(0, fixture.Completer.CallCount);
        }
    }

    [Theory]
    [InlineData("../../outside/REVIEW-0011.md")]
    [InlineData("../../../REVIEW-0011.md")]
    public async Task CompleteAsync_PathTraversalOutsideRepository_RejectsBeforeReviewParsing(string reviewPath)
    {
        var fixture = new GateFixture(reviewReportPath: reviewPath);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Gate.CompleteAsync(
            fixture.Task.FilePath, fixture.RepositoryRoot, "repository", "message", "origin", true));

        Assert.Equal(0, fixture.ReviewParser.CallCount);
        Assert.Equal(0, fixture.Completer.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_AbsoluteReviewPath_RejectsBeforeReviewParsing()
    {
        var fixture = new GateFixture(reviewReportPath: Path.GetFullPath("outside-review.md"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Gate.CompleteAsync(
            fixture.Task.FilePath, fixture.RepositoryRoot, "repository", "message", "origin", true));

        Assert.Equal(0, fixture.ReviewParser.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_SuppliedDirectoryOutsideTaskRepository_RejectsBeforeReviewParsing()
    {
        var fixture = new GateFixture();
        var outsideDirectory = Path.GetFullPath(Path.Combine(fixture.RepositoryRoot, "..", "other"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Gate.CompleteAsync(
            fixture.Task.FilePath, outsideDirectory, "repository", "message", "origin", true));

        Assert.Equal(0, fixture.ReviewParser.CallCount);
    }

    private sealed class GateFixture
    {
        public GateFixture(
            DeveloperReviewValidationResult? validation = null,
            string reviewReportPath = "docs/developer-reviews/REVIEW-0011.md")
        {
            RepositoryRoot = Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                $"TrailTrainer-Gate-{Guid.NewGuid():N}"));
            NestedRepositoryDirectory = Path.Combine(RepositoryRoot, "src", "nested");
            Task = new DeveloperTaskDocument(
                new DeveloperTaskId(11),
                "Review Completion Gate",
                Path.Combine(RepositoryRoot, "docs", "developer-tasks", "DEV-0011-Task.md"),
                "TrailTrainer.Developer",
                "feature/dev-0011",
                reviewReportPath);
            Review = CreateReview(Task.Id, Path.Combine(RepositoryRoot, "docs", "developer-reviews", "REVIEW-0011.md"));
            Validation = validation ?? new DeveloperReviewValidationResult(
                Task.Id, DeveloperReviewStatus.ReadyForReview, [], []);
            Completion = new DeveloperTaskCompletionResult(
                Task.Id, Task.Title, RepositoryRoot, Task.ExpectedBranch, "sha", "message",
                "origin", true, Task.FilePath, Task.ReviewReportPath);
            TaskParser = new FakeTaskParser(Task, Calls);
            ReviewParser = new FakeReviewParser(Review, Calls);
            Validator = new FakeReviewValidator(Validation, Calls);
            Completer = new FakeTaskCompleter(Completion, Calls);
            Gate = new DeveloperTaskGatedCompleter(TaskParser, ReviewParser, Validator, Completer);
        }

        public List<string> Calls { get; } = [];
        public string RepositoryRoot { get; }
        public string NestedRepositoryDirectory { get; }
        public DeveloperTaskDocument Task { get; }
        public DeveloperReviewDocument Review { get; }
        public DeveloperReviewValidationResult Validation { get; }
        public DeveloperTaskCompletionResult Completion { get; }
        public FakeTaskParser TaskParser { get; }
        public FakeReviewParser ReviewParser { get; }
        public FakeReviewValidator Validator { get; }
        public FakeTaskCompleter Completer { get; }
        public DeveloperTaskGatedCompleter Gate { get; }

        public void SetFailure(string dependency, Exception exception)
        {
            switch (dependency)
            {
                case "task": TaskParser.Exception = exception; break;
                case "review": ReviewParser.Exception = exception; break;
                case "validator": Validator.Exception = exception; break;
                case "completer": Completer.Exception = exception; break;
                default: throw new ArgumentOutOfRangeException(nameof(dependency));
            }
        }

        private static DeveloperReviewDocument CreateReview(DeveloperTaskId id, string path) => new(
            id, "Review", path, DeveloperReviewStatus.ReadyForReview, "Summary",
            [], [], [], [], "Architecture", [],
            new DeveloperReviewVerification(true, 0, 0, true, 1, 0, 0, true),
            "None", "None", false, false);
    }

    private sealed class FakeTaskParser(DeveloperTaskDocument result, IList<string> calls) : IDeveloperTaskParser
    {
        public Exception? Exception { get; set; }
        public CancellationToken CancellationToken { get; private set; }
        public Task<DeveloperTaskDocument> ParseAsync(string path, CancellationToken token = default)
        {
            CancellationToken = token; calls.Add("task");
            return Exception is null ? Task.FromResult(result) : Task.FromException<DeveloperTaskDocument>(Exception);
        }
    }

    private sealed class FakeReviewParser(DeveloperReviewDocument result, IList<string> calls) : IDeveloperReviewParser
    {
        public Exception? Exception { get; set; }
        public int CallCount { get; private set; }
        public string? FilePath { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public Task<DeveloperReviewDocument> ParseAsync(string path, CancellationToken token = default)
        {
            CallCount++; FilePath = path; CancellationToken = token; calls.Add("review");
            return Exception is null ? Task.FromResult(result) : Task.FromException<DeveloperReviewDocument>(Exception);
        }
    }

    private sealed class FakeReviewValidator(DeveloperReviewValidationResult result, IList<string> calls)
        : IDeveloperReviewValidator
    {
        public Exception? Exception { get; set; }
        public int CallCount { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public Task<DeveloperReviewValidationResult> ValidateAsync(
            DeveloperTaskDocument task, DeveloperReviewDocument review, CancellationToken token = default)
        {
            CallCount++; CancellationToken = token; calls.Add("validate");
            return Exception is null
                ? Task.FromResult(result)
                : Task.FromException<DeveloperReviewValidationResult>(Exception);
        }
    }

    private sealed class FakeTaskCompleter(DeveloperTaskCompletionResult result, IList<string> calls)
        : IDeveloperTaskCompleter
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
        public Task<DeveloperTaskCompletionResult> CompleteAsync(
            string taskPath, string repositoryPath, string repositoryName, string message,
            string remoteName, bool setUpstream, CancellationToken token = default)
        {
            CallCount++; TaskFilePath = taskPath; RepositoryDirectoryPath = repositoryPath;
            ExpectedRepositoryName = repositoryName; CommitMessage = message; RemoteName = remoteName;
            SetUpstream = setUpstream; CancellationToken = token; calls.Add("complete");
            return Exception is null
                ? Task.FromResult(result)
                : Task.FromException<DeveloperTaskCompletionResult>(Exception);
        }
    }
}
