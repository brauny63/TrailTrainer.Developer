using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Persistence;

namespace TrailTrainer.Developer.Tests;

public sealed class LocalJsonCodexExecutionStateStoreTests
{
    [Fact]
    public async Task OverlappingAndRapidSaves_ProduceOneCompleteStateWithoutTemporaryFiles()
    {
        using var fixture = new Fixture();
        var store = new LocalJsonCodexExecutionStateStore(fixture.Root);
        var states = Enumerable.Range(0, 40)
            .Select(index => State("DEV-0049", index % 2 == 0 ? CodexExecutionPhase.BranchCreated : CodexExecutionPhase.CodexSucceeded))
            .ToArray();

        await Task.WhenAll(states.Select(state => store.SaveAsync(state)));

        var loaded = await store.LoadAsync("DEV-0049");
        Assert.NotNull(loaded);
        Assert.Contains(loaded, states);
        Assert.Empty(Directory.EnumerateFiles(fixture.Root, "*.tmp"));
        Assert.Single(Directory.EnumerateFiles(fixture.Root, "codex-DEV-0049.json"));
    }

    [Fact]
    public async Task IndependentTaskIds_RemainCompleteAndCorrect()
    {
        using var fixture = new Fixture();
        var store = new LocalJsonCodexExecutionStateStore(fixture.Root);

        await Task.WhenAll(
            store.SaveAsync(State("DEV-0049", CodexExecutionPhase.BranchCreated)),
            store.SaveAsync(State("DEV-0050", CodexExecutionPhase.CodexSucceeded)));

        Assert.Equal("DEV-0049", (await store.LoadAsync("DEV-0049"))!.TaskId);
        Assert.Equal(CodexExecutionPhase.CodexSucceeded, (await store.LoadAsync("DEV-0050"))!.Phase);
    }

    [Fact]
    public async Task CancelledSave_DoesNotReplacePreviousValidState()
    {
        using var fixture = new Fixture();
        var store = new LocalJsonCodexExecutionStateStore(fixture.Root);
        await store.SaveAsync(State("DEV-0049", CodexExecutionPhase.BranchCreated));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.SaveAsync(State("DEV-0049", CodexExecutionPhase.CodexSucceeded), cancellation.Token));

        Assert.Equal(CodexExecutionPhase.BranchCreated, (await store.LoadAsync("DEV-0049"))!.Phase);
        Assert.Empty(Directory.EnumerateFiles(fixture.Root, "*.tmp"));
    }

    [Fact]
    public async Task ReplacementFailure_PreservesPreviousStateAndIncludesTaskAndPathDiagnostics()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new Fixture();
        var store = new LocalJsonCodexExecutionStateStore(fixture.Root);
        await store.SaveAsync(State("DEV-0049", CodexExecutionPhase.BranchCreated));
        var path = Path.Combine(fixture.Root, "codex-DEV-0049.json");
        await using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.SaveAsync(State("DEV-0049", CodexExecutionPhase.CodexSucceeded)));
            Assert.Contains("DEV-0049", exception.Message, StringComparison.Ordinal);
            Assert.Contains(path, exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(CodexExecutionPhase.BranchCreated, (await store.LoadAsync("DEV-0049"))!.Phase);
        Assert.Empty(Directory.EnumerateFiles(fixture.Root, "*.tmp"));
    }

    private static CodexExecutionState State(string taskId, CodexExecutionPhase phase) =>
        new(taskId, "repository", $"{taskId}.md", phase);

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Root = Path.Combine(Path.GetTempPath(), $"trailtrainer-dev-0049-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }
        public string Root { get; }
        public void Dispose() => Directory.Delete(Root, true);
    }
}
