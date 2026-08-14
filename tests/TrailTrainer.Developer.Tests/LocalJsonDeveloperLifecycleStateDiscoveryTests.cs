using System.Text.Json.Nodes;
using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Persistence;

namespace TrailTrainer.Developer.Tests;

public sealed class LocalJsonDeveloperLifecycleStateDiscoveryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyStorageDirectoryRejected(string? directory)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new LocalJsonDeveloperLifecycleStateDiscovery(directory!));
    }

    [Fact]
    public async Task ListAsync_MissingDirectoryReturnsEmptyWithoutCreatingIt()
    {
        using var parent = TemporaryDirectory.Create();
        var missing = Path.Combine(parent.Path, "missing", "states");
        var discovery = new LocalJsonDeveloperLifecycleStateDiscovery(missing);

        var result = await discovery.ListAsync();

        Assert.Empty(result);
        Assert.False(Directory.Exists(missing));
    }

    [Fact]
    public async Task ListAsync_ExistingEmptyDirectoryReturnsReadOnlyEmptyCollection()
    {
        using var storage = TemporaryDirectory.Create();

        var result = await new LocalJsonDeveloperLifecycleStateDiscovery(storage.Path).ListAsync();

        Assert.Empty(result);
        Assert.True(((ICollection<DeveloperLifecyclePersistedState>)result).IsReadOnly);
        Assert.Empty(Directory.GetFiles(storage.Path));
    }

    [Fact]
    public async Task ListAsync_OneStoredStatePreservesEveryLogicalValueExactly()
    {
        using var storage = TemporaryDirectory.Create();
        var state = State("Mixed-Task/Ä", "Exact/Task File.md");
        await Store(storage).SaveAsync(state);

        var result = await Discovery(storage).ListAsync();

        var loaded = Assert.Single(result);
        Assert.Equal(state.TaskId, loaded.TaskId);
        Assert.Equal(state.TaskFilePath, loaded.TaskFilePath);
        Assert.Equal(state.SavedAtUtc, loaded.SavedAtUtc);
        Assert.Equal(state.ResumeContext.RepositoryDirectory, loaded.ResumeContext.RepositoryDirectory);
        Assert.Equal(state.ResumeContext.Repository, loaded.ResumeContext.Repository);
        Assert.Equal(state.ResumeContext.PullRequestNumber, loaded.ResumeContext.PullRequestNumber);
        Assert.Equal(state.ResumeContext.FeatureBranch, loaded.ResumeContext.FeatureBranch);
        Assert.Equal(state.ResumeContext.BaseBranch, loaded.ResumeContext.BaseBranch);
        Assert.Equal(state.ResumeContext.GitRemoteName, loaded.ResumeContext.GitRemoteName);
        Assert.True(((ICollection<DeveloperLifecyclePersistedState>)result).IsReadOnly);
    }

    [Fact]
    public async Task ListAsync_OrdersByTimestampThenOrdinalTaskIdIndependentOfCreationOrder()
    {
        using var storage = TemporaryDirectory.Create();
        var store = Store(storage);
        var timestamp = UtcTimestamp();
        await store.SaveAsync(State("z-last-time", savedAtUtc: timestamp.AddMinutes(1)));
        await store.SaveAsync(State("b-equal", savedAtUtc: timestamp));
        await store.SaveAsync(State("A-equal", savedAtUtc: timestamp));
        await store.SaveAsync(State("a-equal", savedAtUtc: timestamp));

        var result = await Discovery(storage).ListAsync();

        Assert.Equal(["A-equal", "a-equal", "b-equal", "z-last-time"],
            result.Select(state => state.TaskId));
    }

    [Fact]
    public async Task ListAsync_IgnoresUnrelatedFilesTemporaryFilesAndNestedDirectoriesWithoutMutation()
    {
        using var storage = TemporaryDirectory.Create();
        await Store(storage).SaveAsync(State("DEV-0021"));
        var unrelated = Path.Combine(storage.Path, "unrelated.txt");
        var unrelatedJson = Path.Combine(storage.Path, "unrelated.json");
        var temporary = Path.Combine(storage.Path,
            ".0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef.lifecycle.json.id.tmp");
        var backup = Path.Combine(storage.Path,
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef.lifecycle.json.bak");
        var nested = Path.Combine(storage.Path,
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef.lifecycle.json");
        await File.WriteAllTextAsync(unrelated, "keep");
        await File.WriteAllTextAsync(unrelatedJson, "{invalid}");
        await File.WriteAllTextAsync(temporary, "partial");
        await File.WriteAllTextAsync(backup, "backup");
        Directory.CreateDirectory(nested);
        await File.WriteAllTextAsync(Path.Combine(nested, "nested.lifecycle.json"), "invalid");
        var before = Directory.GetFileSystemEntries(storage.Path).Order().ToArray();

        var result = await Discovery(storage).ListAsync();

        Assert.Single(result);
        Assert.Equal(before, Directory.GetFileSystemEntries(storage.Path).Order());
        Assert.Equal("keep", await File.ReadAllTextAsync(unrelated));
        Assert.Equal("partial", await File.ReadAllTextAsync(temporary));
    }

    [Fact]
    public async Task ListAsync_MalformedCandidateFailsClearlyWithoutModifyingIt()
    {
        using var storage = TemporaryDirectory.Create();
        await Store(storage).SaveAsync(State("DEV-0021"));
        var path = Assert.Single(StateFiles(storage.Path));
        await File.WriteAllTextAsync(path, "not-json");

        await Assert.ThrowsAsync<InvalidDataException>(() => Discovery(storage).ListAsync());

        Assert.True(File.Exists(path));
        Assert.Equal("not-json", await File.ReadAllTextAsync(path));
    }

    public static TheoryData<string> InvalidDomainFields => new()
    {
        { "missing-task-id" },
        { "invalid-pr" },
        { "equal-branches" },
        { "non-utc-time" }
    };

    [Theory]
    [MemberData(nameof(InvalidDomainFields))]
    public async Task ListAsync_InvalidDiscoveredStateFailsWithoutRewriting(string scenario)
    {
        using var storage = TemporaryDirectory.Create();
        await Store(storage).SaveAsync(State("DEV-0021"));
        var path = Assert.Single(StateFiles(storage.Path));
        var json = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
        var context = json["resumeContext"]!.AsObject();
        switch (scenario)
        {
            case "missing-task-id":
                json.Remove("taskId");
                break;
            case "invalid-pr":
                context["pullRequestNumber"] = 0;
                break;
            case "equal-branches":
                context["featureBranch"] = "main";
                context["baseBranch"] = "main";
                break;
            default:
                json["savedAtUtc"] = "2026-01-01T01:00:00.0000000+01:00";
                break;
        }

        var corrupted = json.ToJsonString();
        await File.WriteAllTextAsync(path, corrupted);

        await Assert.ThrowsAsync<InvalidDataException>(() => Discovery(storage).ListAsync());

        Assert.Equal(corrupted, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task ListAsync_FilenameTaskIdMismatchFailsWithoutRenameOrDelete()
    {
        using var storage = TemporaryDirectory.Create();
        var store = Store(storage);
        await store.SaveAsync(State("Exact-Casing"));
        var exactPath = Assert.Single(StateFiles(storage.Path));
        await store.SaveAsync(State("exact-casing"));
        var otherPath = StateFiles(storage.Path).Single(path => path != exactPath);
        File.Delete(otherPath);
        File.Move(exactPath, otherPath);

        await Assert.ThrowsAsync<InvalidDataException>(() => Discovery(storage).ListAsync());

        Assert.True(File.Exists(otherPath));
        Assert.False(File.Exists(exactPath));
    }

    [Fact]
    public async Task ListAsync_CorruptionPreventsAnyPartialSuccessfulResult()
    {
        using var storage = TemporaryDirectory.Create();
        var store = Store(storage);
        await store.SaveAsync(State("valid"));
        await store.SaveAsync(State("corrupt"));
        var corruptPath = StateFiles(storage.Path).Single(asyncPath =>
            File.ReadAllText(asyncPath).Contains("corrupt", StringComparison.Ordinal));
        await File.WriteAllTextAsync(corruptPath, "invalid");

        IReadOnlyList<DeveloperLifecyclePersistedState>? result = null;
        await Assert.ThrowsAsync<InvalidDataException>(async () => result = await Discovery(storage).ListAsync());

        Assert.Null(result);
    }

    [Fact]
    public async Task ListAsync_DistinctTaskIdsRemainDistinctAndExact()
    {
        using var storage = TemporaryDirectory.Create();
        var store = Store(storage);
        await store.SaveAsync(State("Task"));
        await store.SaveAsync(State("task"));

        var result = await Discovery(storage).ListAsync();

        Assert.Equal(["Task", "task"], result.Select(state => state.TaskId));
    }

    [Fact]
    public async Task ListAsync_DoesNotModifyFinalStateFiles()
    {
        using var storage = TemporaryDirectory.Create();
        await Store(storage).SaveAsync(State("DEV-0021"));
        var path = Assert.Single(StateFiles(storage.Path));
        var before = await File.ReadAllBytesAsync(path);

        await Discovery(storage).ListAsync();

        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        Assert.Single(StateFiles(storage.Path));
    }

    [Fact]
    public async Task ListAsync_PreCancelledTokenPropagatesWithoutReturningPartialCollection()
    {
        using var storage = TemporaryDirectory.Create();
        await Store(storage).SaveAsync(State("DEV-0021"));
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Discovery(storage).ListAsync(source.Token));
    }

    [Fact]
    public async Task ListAsync_StoreReplaceAppearsOnceWithLatestValues()
    {
        using var storage = TemporaryDirectory.Create();
        var store = Store(storage);
        await store.SaveAsync(State("DEV-0021", "old.md"));
        await store.SaveAsync(State("DEV-0021", "latest.md", UtcTimestamp().AddHours(1)));

        var result = await Discovery(storage).ListAsync();

        var state = Assert.Single(result);
        Assert.Equal("latest.md", state.TaskFilePath);
        Assert.Equal(UtcTimestamp().AddHours(1), state.SavedAtUtc);
    }

    [Fact]
    public async Task ListAsync_StoreDeletedStateIsNoLongerDiscovered()
    {
        using var storage = TemporaryDirectory.Create();
        var store = Store(storage);
        await store.SaveAsync(State("DEV-0021"));
        await store.DeleteAsync("DEV-0021");

        var result = await Discovery(storage).ListAsync();

        Assert.Empty(result);
    }

    private static LocalJsonDeveloperLifecycleStateStore Store(TemporaryDirectory storage) =>
        new(storage.Path);

    private static LocalJsonDeveloperLifecycleStateDiscovery Discovery(TemporaryDirectory storage) =>
        new(storage.Path);

    private static DateTimeOffset UtcTimestamp() =>
        new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

    private static DeveloperLifecyclePersistedState State(
        string taskId,
        string? taskFilePath = null,
        DateTimeOffset? savedAtUtc = null) => new(
            taskId,
            taskFilePath,
            new DeveloperLifecycleResumeContext(
                $"Repository/{taskId}",
                new GitHubRepositoryIdentity($"Owner-{taskId}", $"Repository-{taskId}"),
                21,
                $"feature/{taskId}",
                "main",
                $"remote-{taskId}"),
            savedAtUtc ?? UtcTimestamp());

    private static string[] StateFiles(string directory) =>
        Directory.GetFiles(directory, "*.lifecycle.json", SearchOption.TopDirectoryOnly);
}
