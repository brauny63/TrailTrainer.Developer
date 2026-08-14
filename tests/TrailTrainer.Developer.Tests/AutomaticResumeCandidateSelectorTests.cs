using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class AutomaticResumeCandidateSelectorTests
{
    [Fact]
    public void CandidateResult_FoundRequiresStateAndTarget()
    {
        var state = State("DEV-0024");
        var target = new PersistedLifecycleResumeTarget(state.TaskId, state);

        Assert.Throws<ArgumentException>(() => new AutomaticResumeCandidateResult(
            AutomaticResumeCandidateState.Found));
        Assert.Throws<ArgumentException>(() => new AutomaticResumeCandidateResult(
            AutomaticResumeCandidateState.Found, state));
        Assert.Throws<ArgumentException>(() => new AutomaticResumeCandidateResult(
            AutomaticResumeCandidateState.Found, null, target));
    }

    [Fact]
    public void CandidateResult_FoundRequiresExactTargetStateIdentity()
    {
        var selected = State("DEV-0024");
        var equivalentButDistinct = State("DEV-0024");
        var wrongTarget = new PersistedLifecycleResumeTarget(
            equivalentButDistinct.TaskId,
            equivalentButDistinct);

        Assert.Throws<ArgumentException>(() => new AutomaticResumeCandidateResult(
            AutomaticResumeCandidateState.Found,
            selected,
            wrongTarget));
        var exactTarget = new PersistedLifecycleResumeTarget(selected.TaskId, selected);
        var result = new AutomaticResumeCandidateResult(
            AutomaticResumeCandidateState.Found,
            selected,
            exactTarget);

        Assert.Same(selected, result.PersistedState);
        Assert.Same(exactTarget, result.ResumeTarget);
        Assert.Same(selected, result.ResumeTarget!.PersistedState);
        Assert.Equal(selected.TaskId, result.ResumeTarget.TaskId);
    }

    [Fact]
    public void CandidateResult_NotFoundRejectsStateOrTarget()
    {
        var state = State("DEV-0024");
        var target = new PersistedLifecycleResumeTarget(state.TaskId, state);

        Assert.Throws<ArgumentException>(() => new AutomaticResumeCandidateResult(
            AutomaticResumeCandidateState.NotFound,
            state));
        Assert.Throws<ArgumentException>(() => new AutomaticResumeCandidateResult(
            AutomaticResumeCandidateState.NotFound,
            null,
            target));
        var result = new AutomaticResumeCandidateResult(AutomaticResumeCandidateState.NotFound);

        Assert.Null(result.PersistedState);
        Assert.Null(result.ResumeTarget);
    }

    [Fact]
    public void CandidateResult_UnsupportedStateRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AutomaticResumeCandidateResult(
            (AutomaticResumeCandidateState)99));
    }

    [Fact]
    public async Task SelectAsync_CallsDiscoveryExactlyOnceWithExactCancellationToken()
    {
        using var source = new CancellationTokenSource();
        var discovery = new FakeDiscovery([State("DEV-0024")]);

        await new AutomaticResumeCandidateSelector(discovery).SelectAsync(source.Token);

        Assert.Equal(1, discovery.CallCount);
        Assert.Equal(source.Token, discovery.CancellationToken);
    }

    [Fact]
    public async Task SelectAsync_DiscoveryFailurePropagatesWithoutRetry()
    {
        var expected = new InvalidDataException("discovery failed");
        var discovery = new FakeDiscovery([]) { Exception = expected };

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new AutomaticResumeCandidateSelector(discovery).SelectAsync());

        Assert.Same(expected, exception);
        Assert.Equal(1, discovery.CallCount);
    }

    [Fact]
    public async Task SelectAsync_NullDiscoveryCollectionFailsClearly()
    {
        var discovery = new FakeDiscovery(null!);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AutomaticResumeCandidateSelector(discovery).SelectAsync());

        Assert.Contains("null collection", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, discovery.CallCount);
    }

    [Fact]
    public async Task SelectAsync_EmptyDiscoveryReturnsNotFoundWithNullValues()
    {
        var result = await new AutomaticResumeCandidateSelector(new FakeDiscovery([])).SelectAsync();

        Assert.Equal(AutomaticResumeCandidateState.NotFound, result.State);
        Assert.Null(result.PersistedState);
        Assert.Null(result.ResumeTarget);
    }

    [Fact]
    public async Task SelectAsync_OneStatePreservesExactIdentityAndCreatesMatchingTarget()
    {
        var state = State("DEV-0024");

        var result = await new AutomaticResumeCandidateSelector(
            new FakeDiscovery([state])).SelectAsync();

        Assert.Equal(AutomaticResumeCandidateState.Found, result.State);
        Assert.Same(state, result.PersistedState);
        Assert.NotNull(result.ResumeTarget);
        Assert.Equal("DEV-0024", result.ResumeTarget.TaskId);
        Assert.Same(state, result.ResumeTarget.PersistedState);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectAsync_SelectsOldestThenLowestOrdinalTaskIdIndependentOfOrder(bool reverse)
    {
        var timestamp = UtcTimestamp();
        var selected = State("A-oldest", timestamp);
        var states = new[]
        {
            State("later", timestamp.AddMinutes(1)),
            State("z-oldest", timestamp),
            State("a-oldest", timestamp),
            selected
        };
        var input = reverse ? states.Reverse().ToArray() : states;

        var result = await new AutomaticResumeCandidateSelector(
            new FakeDiscovery(input)).SelectAsync();

        Assert.Same(selected, result.PersistedState);
        Assert.Same(selected, result.ResumeTarget!.PersistedState);
    }

    [Fact]
    public async Task SelectAsync_OrdinalTieBreakKeepsCaseDistinctTaskIds()
    {
        var timestamp = UtcTimestamp();
        var upper = State("Task", timestamp);
        var lower = State("task", timestamp);

        var result = await new AutomaticResumeCandidateSelector(
            new FakeDiscovery([lower, upper])).SelectAsync();

        Assert.Same(upper, result.PersistedState);
    }

    [Fact]
    public async Task SelectAsync_PreCancelledDiscoveryPropagatesInsteadOfNotFound()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var discovery = new FakeDiscovery([], honorCancellation: true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new AutomaticResumeCandidateSelector(discovery).SelectAsync(source.Token));

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
                24,
                "feature/automatic-selection",
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
