using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class DeveloperTaskStarterTests
{
    [Fact]
    public async Task StartAsync_ValidTask_ReturnsResultAndCallsDependenciesInOrder()
    {
        var calls = new List<string>();
        var task = CreateTask();
        using var cancellationSource = new CancellationTokenSource();
        var parser = new FakeTaskParser(task, calls);
        var statusProvider = new FakeStatusProvider(CleanMainStatus(), calls);
        var branchCreator = new FakeBranchCreator(
            new GitBranchCreationResult("C:\\repository", task.ExpectedBranch),
            calls);
        var starter = new DeveloperTaskStarter(parser, statusProvider, branchCreator);

        var result = await starter.StartAsync(
            task.FilePath,
            "C:\\repository\\nested",
            "TrailTrainer.Developer",
            cancellationSource.Token);

        Assert.Equal(task.Id, result.TaskId);
        Assert.Equal(task.Title, result.TaskTitle);
        Assert.Equal("C:\\repository", result.RepositoryRoot);
        Assert.Equal("main", result.PreviousBranch);
        Assert.Equal(task.ExpectedBranch, result.CreatedBranch);
        Assert.Equal(task.FilePath, result.TaskFilePath);
        Assert.Equal(task.ReviewReportPath, result.ReviewReportPath);
        Assert.Equal(["parse", "status", "branch"], calls);
        Assert.Equal(1, branchCreator.CallCount);
        Assert.Equal(task.ExpectedBranch, branchCreator.BranchName);
        Assert.Equal("C:\\repository\\nested", branchCreator.DirectoryPath);
        Assert.Equal(cancellationSource.Token, parser.CancellationToken);
        Assert.Equal(cancellationSource.Token, statusProvider.CancellationToken);
        Assert.Equal(cancellationSource.Token, branchCreator.CancellationToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StartAsync_InvalidExpectedRepositoryName_ThrowsBeforeStatusOrBranch(
        string? expectedRepositoryName)
    {
        var parser = new FakeTaskParser(CreateTask());
        var statusProvider = new FakeStatusProvider(CleanMainStatus());
        var branchCreator = new FakeBranchCreator();
        var starter = new DeveloperTaskStarter(parser, statusProvider, branchCreator);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => starter.StartAsync(
            "task.md",
            "repository",
            expectedRepositoryName!));

        Assert.Equal(1, parser.CallCount);
        Assert.Equal(0, statusProvider.CallCount);
        Assert.Equal(0, branchCreator.CallCount);
    }

    [Fact]
    public async Task StartAsync_RepositoryMetadataMismatch_ReportsExpectedAndActualWithoutCreatingBranch()
    {
        var parser = new FakeTaskParser(CreateTask(repository: "Actual.Repository"));
        var statusProvider = new FakeStatusProvider(CleanMainStatus());
        var branchCreator = new FakeBranchCreator();
        var starter = new DeveloperTaskStarter(parser, statusProvider, branchCreator);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => starter.StartAsync(
            "task.md",
            "repository",
            "Expected.Repository"));

        Assert.Contains("Expected.Repository", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Actual.Repository", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, statusProvider.CallCount);
        Assert.Equal(0, branchCreator.CallCount);
    }

    [Fact]
    public async Task StartAsync_ParsingFails_DoesNotReadStatusOrCreateBranch()
    {
        var parser = new FakeTaskParser(new InvalidDataException("Invalid task"));
        var statusProvider = new FakeStatusProvider(CleanMainStatus());
        var branchCreator = new FakeBranchCreator();
        var starter = new DeveloperTaskStarter(parser, statusProvider, branchCreator);

        await Assert.ThrowsAsync<InvalidDataException>(() => starter.StartAsync(
            "task.md",
            "repository",
            "TrailTrainer.Developer"));

        Assert.Equal(0, statusProvider.CallCount);
        Assert.Equal(0, branchCreator.CallCount);
    }

    public static TheoryData<GitRepositoryStatus> InvalidRepositoryStatuses => new()
    {
        GitRepositoryStatus.NotRepository,
        new GitRepositoryStatus(true, "C:\\repository", null, false),
        new GitRepositoryStatus(true, "C:\\repository", "feature/other", false),
        new GitRepositoryStatus(true, "C:\\repository", "main", true)
    };

    [Theory]
    [MemberData(nameof(InvalidRepositoryStatuses))]
    public async Task StartAsync_InvalidRepositoryPrecondition_DoesNotCreateBranch(
        GitRepositoryStatus repositoryStatus)
    {
        var parser = new FakeTaskParser(CreateTask());
        var statusProvider = new FakeStatusProvider(repositoryStatus);
        var branchCreator = new FakeBranchCreator();
        var starter = new DeveloperTaskStarter(parser, statusProvider, branchCreator);

        await Assert.ThrowsAsync<InvalidOperationException>(() => starter.StartAsync(
            "task.md",
            "repository",
            "TrailTrainer.Developer"));

        Assert.Equal(1, statusProvider.CallCount);
        Assert.Equal(0, branchCreator.CallCount);
    }

    private static DeveloperTaskDocument CreateTask(string repository = "TrailTrainer.Developer") => new(
        new DeveloperTaskId(7),
        "Start Developer Task Workflow",
        "C:\\tasks\\DEV-0007-Task.md",
        repository,
        "feature/Exact-Task-Branch",
        "docs/developer-reviews/REVIEW-0007.md");

    private static GitRepositoryStatus CleanMainStatus() => new(
        true,
        "C:\\repository",
        "main",
        false);

    private sealed class FakeTaskParser : IDeveloperTaskParser
    {
        private readonly DeveloperTaskDocument? result;
        private readonly Exception? exception;
        private readonly IList<string>? calls;

        public FakeTaskParser(DeveloperTaskDocument result, IList<string>? calls = null)
        {
            this.result = result;
            this.calls = calls;
        }

        public FakeTaskParser(Exception exception)
        {
            this.exception = exception;
        }

        public int CallCount { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<DeveloperTaskDocument> ParseAsync(
            string developerTaskFilePath,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            CancellationToken = cancellationToken;
            calls?.Add("parse");
            return exception is null
                ? Task.FromResult(result!)
                : Task.FromException<DeveloperTaskDocument>(exception);
        }
    }

    private sealed class FakeStatusProvider : IGitRepositoryStatusProvider
    {
        private readonly GitRepositoryStatus result;
        private readonly IList<string>? calls;

        public FakeStatusProvider(GitRepositoryStatus result, IList<string>? calls = null)
        {
            this.result = result;
            this.calls = calls;
        }

        public int CallCount { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<GitRepositoryStatus> GetStatusAsync(
            string directoryPath,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            CancellationToken = cancellationToken;
            calls?.Add("status");
            return Task.FromResult(result);
        }
    }

    private sealed class FakeBranchCreator : IGitBranchCreator
    {
        private readonly GitBranchCreationResult result;
        private readonly IList<string>? calls;

        public FakeBranchCreator(
            GitBranchCreationResult? result = null,
            IList<string>? calls = null)
        {
            this.result = result ?? new GitBranchCreationResult("C:\\repository", "feature/default");
            this.calls = calls;
        }

        public int CallCount { get; private set; }
        public string? DirectoryPath { get; private set; }
        public string? BranchName { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<GitBranchCreationResult> CreateAsync(
            string directoryPath,
            string branchName,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            DirectoryPath = directoryPath;
            BranchName = branchName;
            CancellationToken = cancellationToken;
            calls?.Add("branch");
            return Task.FromResult(result);
        }
    }
}
