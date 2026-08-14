using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class PersistedLifecycleSelectorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SelectionRequest_ExactRequiresNonEmptyTaskId(string? taskId)
    {
        Assert.ThrowsAny<ArgumentException>(() => new PersistedLifecycleSelectionRequest(
            PersistedLifecycleSelectionMode.ExactTaskId,
            taskId));
    }

    [Theory]
    [InlineData(PersistedLifecycleSelectionMode.Oldest)]
    [InlineData(PersistedLifecycleSelectionMode.Newest)]
    public void SelectionRequest_NonExactRejectsNonNullTaskId(PersistedLifecycleSelectionMode mode)
    {
        Assert.Throws<ArgumentException>(() => new PersistedLifecycleSelectionRequest(mode, "DEV-0022"));
        Assert.Null(new PersistedLifecycleSelectionRequest(mode).TaskId);
    }

    [Fact]
    public void SelectionRequest_UnsupportedModeRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PersistedLifecycleSelectionRequest(
            (PersistedLifecycleSelectionMode)99));
    }

    [Fact]
    public async Task SelectAsync_NullRequestRejectedBeforeDiscovery()
    {
        var discovery = new FakeDiscovery([]);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            new PersistedLifecycleSelector(discovery).SelectAsync(null!));

        Assert.Equal(0, discovery.CallCount);
    }

    [Fact]
    public void SelectionResult_EnforcesFoundAndNotFoundInvariants()
    {
        var state = State("DEV-0022");

        Assert.Throws<ArgumentException>(() => new PersistedLifecycleSelectionResult(
            PersistedLifecycleSelectionState.Found));
        Assert.Throws<ArgumentException>(() => new PersistedLifecycleSelectionResult(
            PersistedLifecycleSelectionState.NotFound,
            state));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PersistedLifecycleSelectionResult(
            (PersistedLifecycleSelectionState)99));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResumeTarget_RequiresNonEmptyMatchingTaskId(string? taskId)
    {
        Assert.ThrowsAny<ArgumentException>(() => new PersistedLifecycleResumeTarget(
            taskId!, State("DEV-0022")));
    }

    [Fact]
    public void ResumeTarget_RequiresStateAndOrdinalTaskIdMatch()
    {
        Assert.Throws<ArgumentNullException>(() => new PersistedLifecycleResumeTarget("DEV-0022", null!));
        Assert.Throws<ArgumentException>(() => new PersistedLifecycleResumeTarget(
            "dev-0022", State("DEV-0022")));
        var state = State("DEV-0022");

        var target = new PersistedLifecycleResumeTarget("DEV-0022", state);

        Assert.Equal("DEV-0022", target.TaskId);
        Assert.Same(state, target.PersistedState);
    }

    [Fact]
    public async Task SelectAsync_CallsDiscoveryExactlyOnceWithExactCancellationToken()
    {
        using var source = new CancellationTokenSource();
        var discovery = new FakeDiscovery([State("DEV-0022")]);

        await new PersistedLifecycleSelector(discovery).SelectAsync(
            new PersistedLifecycleSelectionRequest(PersistedLifecycleSelectionMode.Oldest),
            source.Token);

        Assert.Equal(1, discovery.CallCount);
        Assert.Equal(source.Token, discovery.CancellationToken);
    }

    [Fact]
    public async Task SelectAsync_DiscoveryExceptionPropagatesWithoutRetry()
    {
        var expected = new InvalidDataException("discovery failed");
        var discovery = new FakeDiscovery([]) { Exception = expected };

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new PersistedLifecycleSelector(discovery).SelectAsync(
                new PersistedLifecycleSelectionRequest(PersistedLifecycleSelectionMode.Oldest)));

        Assert.Same(expected, exception);
        Assert.Equal(1, discovery.CallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectAsync_ExactMatchIsOrdinalOrderIndependentAndPreservesIdentity(bool reverse)
    {
        var exact = State("Task-ID");
        var states = new[] { State("task-id"), exact, State("other") };
        var discovery = new FakeDiscovery(reverse ? states.Reverse().ToArray() : states);

        var result = await new PersistedLifecycleSelector(discovery).SelectAsync(
            new PersistedLifecycleSelectionRequest(
                PersistedLifecycleSelectionMode.ExactTaskId,
                "Task-ID"));

        Assert.Equal(PersistedLifecycleSelectionState.Found, result.State);
        Assert.Same(exact, result.PersistedState);
    }

    [Fact]
    public async Task SelectAsync_MissingExactTaskIdReturnsNotFoundIncludingCaseDistinctOnly()
    {
        var discovery = new FakeDiscovery([State("DEV-0022")]);

        var result = await new PersistedLifecycleSelector(discovery).SelectAsync(
            new PersistedLifecycleSelectionRequest(
                PersistedLifecycleSelectionMode.ExactTaskId,
                "dev-0022"));

        Assert.Equal(PersistedLifecycleSelectionState.NotFound, result.State);
        Assert.Null(result.PersistedState);
    }

    [Fact]
    public async Task SelectAsync_DuplicateExactMatchesFailClearly()
    {
        var discovery = new FakeDiscovery([State("duplicate"), State("duplicate")]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PersistedLifecycleSelector(discovery).SelectAsync(
                new PersistedLifecycleSelectionRequest(
                    PersistedLifecycleSelectionMode.ExactTaskId,
                    "duplicate")));

        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, discovery.CallCount);
    }

    [Theory]
    [InlineData(PersistedLifecycleSelectionMode.Oldest)]
    [InlineData(PersistedLifecycleSelectionMode.Newest)]
    public async Task SelectAsync_EmptyDiscoveryReturnsNotFound(PersistedLifecycleSelectionMode mode)
    {
        var result = await new PersistedLifecycleSelector(new FakeDiscovery([])).SelectAsync(
            new PersistedLifecycleSelectionRequest(mode));

        Assert.Equal(PersistedLifecycleSelectionState.NotFound, result.State);
        Assert.Null(result.PersistedState);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectAsync_OldestUsesEarliestTimestampThenLowestOrdinalTaskId(bool reverse)
    {
        var timestamp = UtcTimestamp();
        var selected = State("A-oldest", timestamp);
        var states = new[]
        {
            State("later", timestamp.AddMinutes(1)),
            State("a-oldest", timestamp),
            selected,
            State("z-oldest", timestamp)
        };
        var discovery = new FakeDiscovery(reverse ? states.Reverse().ToArray() : states);

        var result = await new PersistedLifecycleSelector(discovery).SelectAsync(
            new PersistedLifecycleSelectionRequest(PersistedLifecycleSelectionMode.Oldest));

        Assert.Equal(PersistedLifecycleSelectionState.Found, result.State);
        Assert.Same(selected, result.PersistedState);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectAsync_NewestUsesLatestTimestampThenHighestOrdinalTaskId(bool reverse)
    {
        var timestamp = UtcTimestamp();
        var selected = State("z-newest", timestamp.AddMinutes(1));
        var states = new[]
        {
            State("older", timestamp),
            State("A-newest", timestamp.AddMinutes(1)),
            State("a-newest", timestamp.AddMinutes(1)),
            selected
        };
        var discovery = new FakeDiscovery(reverse ? states.Reverse().ToArray() : states);

        var result = await new PersistedLifecycleSelector(discovery).SelectAsync(
            new PersistedLifecycleSelectionRequest(PersistedLifecycleSelectionMode.Newest));

        Assert.Equal(PersistedLifecycleSelectionState.Found, result.State);
        Assert.Same(selected, result.PersistedState);
    }

    [Fact]
    public async Task SelectAsync_PreCancelledDiscoveryPropagatesCancellationNotNotFound()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var discovery = new FakeDiscovery([], honorCancellation: true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new PersistedLifecycleSelector(discovery).SelectAsync(
                new PersistedLifecycleSelectionRequest(PersistedLifecycleSelectionMode.Oldest),
                source.Token));

        Assert.Equal(1, discovery.CallCount);
    }

    private static DateTimeOffset UtcTimestamp() =>
        new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

    private static DeveloperLifecyclePersistedState State(
        string taskId,
        DateTimeOffset? savedAtUtc = null) => new(
            taskId,
            null,
            new DeveloperLifecycleResumeContext(
                "repository",
                new GitHubRepositoryIdentity("owner", "repository"),
                22,
                "feature/selection",
                "main",
                "origin"),
            savedAtUtc ?? UtcTimestamp());

    private sealed class FakeDiscovery(
        IReadOnlyList<DeveloperLifecyclePersistedState> states,
        bool honorCancellation = false) : IDeveloperLifecycleStateDiscovery
    {
        public Exception? Exception { get; init; }
        public int CallCount { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<IReadOnlyList<DeveloperLifecyclePersistedState>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            CancellationToken = cancellationToken;
            if (honorCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return Exception is null
                ? Task.FromResult(states)
                : Task.FromException<IReadOnlyList<DeveloperLifecyclePersistedState>>(Exception);
        }
    }
}
