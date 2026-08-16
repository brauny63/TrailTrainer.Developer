using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class StrandedCodexStateRecoveryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "trailtrainer-recovery-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ExactLegacyState_IsAdoptedWithoutChangingImplementationBytes()
    {
        var fixture = CreateFixture();
        var implementation = Path.Combine(root, "implementation.cs");
        await File.WriteAllBytesAsync(implementation, [0, 1, 2, 3, 255]);
        var before = await File.ReadAllBytesAsync(implementation);

        var result = await fixture.Recovery.TryRecoverAsync(fixture.Request);

        Assert.True(result.Recovered);
        Assert.Equal("DEV-0007", result.TaskId);
        Assert.Equal(before, await File.ReadAllBytesAsync(implementation));
        Assert.Equal(CodexExecutionPhase.ReviewRepairRequired, fixture.Codex.Saved!.Phase);
        Assert.NotNull(fixture.Lifecycle.Saved?.RecoveryStartRequest);
        Assert.Equal("DEV-0007", fixture.Lifecycle.Saved!.TaskId);
        var candidate = await new AutomaticResumeCandidateSelector(fixture.Lifecycle).SelectAsync();
        Assert.Equal(AutomaticResumeCandidateState.Found, candidate.State);
    }

    [Theory]
    [InlineData("wrong-branch")]
    [InlineData("wrong-repository")]
    [InlineData("wrong-task-id")]
    [InlineData("wrong-task-file")]
    [InlineData("no-review")]
    [InlineData("clean")]
    [InlineData("conflicting-lifecycle")]
    [InlineData("valid-review")]
    [InlineData("missing-codex")]
    public async Task NonExactLegacyState_IsRejected(string mismatch)
    {
        var fixture = CreateFixture(mismatch);

        var result = await fixture.Recovery.TryRecoverAsync(fixture.Request);

        Assert.False(result.Recovered);
        Assert.Null(fixture.Lifecycle.Saved);
        Assert.Null(fixture.Codex.Saved);
    }

    private Fixture CreateFixture(string? mismatch = null)
    {
        Directory.CreateDirectory(Path.Combine(root, "docs", "developer-tasks"));
        Directory.CreateDirectory(Path.Combine(root, "docs", "developer-reviews"));
        var taskPath = Path.Combine(root, "docs", "developer-tasks", "DEV-0007-task.md");
        File.WriteAllText(taskPath, "task");
        var reviewPath = Path.Combine(root, "docs", "developer-reviews", "REVIEW-0007.md");
        if (mismatch != "no-review") File.WriteAllText(reviewPath, "## Architecture Notes\nlegacy");
        var descriptor = new DeveloperTaskDescriptor(new DeveloperTaskId(7), taskPath, Path.GetFileName(taskPath));
        var task = new DeveloperTaskDocument(
            descriptor.Id, "legacy", taskPath,
            mismatch == "wrong-repository" ? "other" : "TrailTrainer.Developer",
            "feature/dev-0007-implement-valueobject", "docs/developer-reviews/REVIEW-0007.md");
        var state = mismatch == "missing-codex" ? null : new CodexExecutionState(
            mismatch == "wrong-task-id" ? "DEV-0008" : "DEV-0007", root,
            mismatch == "wrong-task-file" ? taskPath + ".other" : taskPath,
            CodexExecutionPhase.BranchCreated);
        var codex = new MemoryCodexStore(state);
        var lifecycle = new MemoryLifecycleStore();
        if (mismatch == "conflicting-lifecycle") lifecycle.States.Add(LegacyLifecycle());
        var reviewParser = new FakeReviewParser(mismatch == "valid-review", reviewPath);
        var recovery = new StrandedCodexStateRecovery(
            new FakeDiscovery(descriptor), new FakeTaskParser(task),
            new FakeStatus(root, mismatch == "wrong-branch" ? "other" : task.ExpectedBranch, mismatch != "clean"),
            codex, lifecycle, lifecycle, reviewParser, new DeveloperReviewValidator(), new FakeClock());
        var request = new InitialDeveloperTaskIntakeRequest(
            true, root, "TrailTrainer.Developer", "owner", "main", "origin",
            PullRequestMergeMethod.Squash, null, null, false);
        return new Fixture(recovery, request, codex, lifecycle);
    }

    private static DeveloperLifecyclePersistedState LegacyLifecycle() => new(
        "DEV-9999", "task.md",
        new DeveloperLifecycleResumeContext("repo", new GitHubRepositoryIdentity("owner", "repo"), 1, "feature", "main", "origin"),
        DateTimeOffset.UnixEpoch);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private sealed record Fixture(StrandedCodexStateRecovery Recovery, InitialDeveloperTaskIntakeRequest Request, MemoryCodexStore Codex, MemoryLifecycleStore Lifecycle);
    private sealed class FakeDiscovery(DeveloperTaskDescriptor descriptor) : IDeveloperTaskDiscovery
    {
        public Task<IReadOnlyList<DeveloperTaskDescriptor>> DiscoverAsync(string repositoryRoot, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DeveloperTaskDescriptor>>([descriptor]);
    }
    private sealed class FakeTaskParser(DeveloperTaskDocument task) : IDeveloperTaskParser
    {
        public Task<DeveloperTaskDocument> ParseAsync(string taskFilePath, CancellationToken cancellationToken = default) => Task.FromResult(task);
    }
    private sealed class FakeStatus(string root, string branch, bool dirty) : IGitRepositoryStatusProvider
    {
        public Task<GitRepositoryStatus> GetStatusAsync(string repositoryDirectoryPath, CancellationToken cancellationToken = default) => Task.FromResult(new GitRepositoryStatus(true, root, branch, dirty));
    }
    private sealed class MemoryCodexStore(CodexExecutionState? loaded) : ICodexExecutionStateStore
    {
        public CodexExecutionState? Saved { get; private set; }
        public Task<CodexExecutionState?> LoadAsync(string taskId, CancellationToken cancellationToken = default) => Task.FromResult(loaded);
        public Task SaveAsync(CodexExecutionState state, CancellationToken cancellationToken = default) { Saved = state; return Task.CompletedTask; }
        public Task DeleteAsync(string taskId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class MemoryLifecycleStore : IDeveloperLifecycleStateStore, IDeveloperLifecycleStateDiscovery
    {
        public List<DeveloperLifecyclePersistedState> States { get; } = [];
        public DeveloperLifecyclePersistedState? Saved { get; private set; }
        public Task<IReadOnlyList<DeveloperLifecyclePersistedState>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DeveloperLifecyclePersistedState>>(Saved is null ? States : [.. States, Saved]);
        public Task SaveAsync(DeveloperLifecyclePersistedState state, CancellationToken cancellationToken = default) { Saved = state; return Task.CompletedTask; }
        public Task<DeveloperLifecyclePersistedState?> LoadAsync(string taskId, CancellationToken cancellationToken = default) => Task.FromResult(Saved);
        public Task DeleteAsync(string taskId, CancellationToken cancellationToken = default) { Saved = null; return Task.CompletedTask; }
    }
    private sealed class FakeReviewParser(bool valid, string path) : IDeveloperReviewParser
    {
        public Task<DeveloperReviewDocument> ParseAsync(string reviewFilePath, CancellationToken cancellationToken = default)
        {
            if (!valid) throw new InvalidDataException("legacy Architecture Notes heading");
            return Task.FromResult(new DeveloperReviewDocument(new DeveloperTaskId(7), "valid", path,
                DeveloperReviewStatus.ReadyForReview, "summary", [], [], [], [], "notes", [],
                new DeveloperReviewVerification(true, 0, 0, true, 1, 0, 0, true), "None.", "None.", false, false));
        }
    }
    private sealed class FakeClock : IUtcClock { public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch; }
}
