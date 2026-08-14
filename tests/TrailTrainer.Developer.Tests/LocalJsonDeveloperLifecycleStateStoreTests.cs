using System.Text.Json;
using System.Text.Json.Nodes;
using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Persistence;

namespace TrailTrainer.Developer.Tests;

public sealed class LocalJsonDeveloperLifecycleStateStoreTests
{
    [Fact]
    public void PersistedState_ValidValuesArePreservedExactly()
    {
        var context = Context();
        var timestamp = new DateTimeOffset(2026, 8, 14, 12, 34, 56, TimeSpan.Zero);

        var state = new DeveloperLifecyclePersistedState(
            "Dev-Mixed/0019", "Exact/Task File.md", context, timestamp);

        Assert.Equal("Dev-Mixed/0019", state.TaskId);
        Assert.Equal("Exact/Task File.md", state.TaskFilePath);
        Assert.Same(context, state.ResumeContext);
        Assert.Equal(timestamp, state.SavedAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PersistedState_EmptyTaskIdRejected(string? taskId)
    {
        Assert.ThrowsAny<ArgumentException>(() => new DeveloperLifecyclePersistedState(
            taskId!, null, Context(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void PersistedState_InvalidContextTimestampOrOptionalPathRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new DeveloperLifecyclePersistedState(
            "DEV-0019", null, null!, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new DeveloperLifecyclePersistedState(
            "DEV-0019", "   ", Context(), DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new DeveloperLifecyclePersistedState(
            "DEV-0019", null, Context(), new DateTimeOffset(2026, 1, 1, 1, 0, 0, TimeSpan.FromHours(1))));

        var state = new DeveloperLifecyclePersistedState(
            "DEV-0019", null, Context(), DateTimeOffset.UtcNow);
        Assert.Null(state.TaskFilePath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Store_EmptyStorageDirectoryRejected(string? directory)
    {
        Assert.ThrowsAny<ArgumentException>(() => new LocalJsonDeveloperLifecycleStateStore(directory!));
    }

    [Fact]
    public async Task SaveAndLoad_CreateDirectoryAndPreserveAllLogicalValues()
    {
        using var parent = TemporaryDirectory.Create();
        var storage = Path.Combine(parent.Path, "new", "state storage");
        var store = new LocalJsonDeveloperLifecycleStateStore(storage);
        var state = State("Dev-Mixed/0019", "Exact/Task File.md");

        await store.SaveAsync(state);
        var loaded = await store.LoadAsync(state.TaskId);

        Assert.True(Directory.Exists(storage));
        Assert.NotNull(loaded);
        Assert.Equal(state.TaskId, loaded.TaskId);
        Assert.Equal(state.TaskFilePath, loaded.TaskFilePath);
        Assert.Equal(state.SavedAtUtc, loaded.SavedAtUtc);
        Assert.Equal(state.ResumeContext.RepositoryDirectory, loaded.ResumeContext.RepositoryDirectory);
        Assert.Equal(state.ResumeContext.Repository, loaded.ResumeContext.Repository);
        Assert.Equal(state.ResumeContext.PullRequestNumber, loaded.ResumeContext.PullRequestNumber);
        Assert.Equal(state.ResumeContext.FeatureBranch, loaded.ResumeContext.FeatureBranch);
        Assert.Equal(state.ResumeContext.BaseBranch, loaded.ResumeContext.BaseBranch);
        Assert.Equal(state.ResumeContext.GitRemoteName, loaded.ResumeContext.GitRemoteName);
    }

    [Fact]
    public async Task Save_SameTaskIdReplacesAndReturnsLatestState()
    {
        using var storage = TemporaryDirectory.Create();
        var store = new LocalJsonDeveloperLifecycleStateStore(storage.Path);
        var original = State("DEV-0019", "old.md");
        var replacement = new DeveloperLifecyclePersistedState(
            "DEV-0019",
            "new.md",
            new DeveloperLifecycleResumeContext(
                "New/Directory", new GitHubRepositoryIdentity("NewOwner", "NewRepository"),
                99, "feature/new", "trunk", "upstream"),
            original.SavedAtUtc.AddMinutes(1));

        await store.SaveAsync(original);
        await store.SaveAsync(replacement);
        var loaded = await store.LoadAsync("DEV-0019");

        Assert.Equal(replacement, loaded);
        Assert.Single(StateFiles(storage.Path));
        Assert.Empty(TemporaryFiles(storage.Path));
    }

    [Fact]
    public async Task Save_DifferentTaskIdsPersistIndependentlyWithoutRepresentativeCollision()
    {
        using var storage = TemporaryDirectory.Create();
        var store = new LocalJsonDeveloperLifecycleStateStore(storage.Path);

        await store.SaveAsync(State("DEV/0019"));
        await store.SaveAsync(State("DEV\\0019"));

        Assert.Equal("DEV/0019", (await store.LoadAsync("DEV/0019"))!.TaskId);
        Assert.Equal("DEV\\0019", (await store.LoadAsync("DEV\\0019"))!.TaskId);
        Assert.Equal(2, StateFiles(storage.Path).Length);
        Assert.Equal(2, StateFiles(storage.Path).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Mapping_IsDeterministicAcrossStoreInstances()
    {
        using var storage = TemporaryDirectory.Create();
        var first = new LocalJsonDeveloperLifecycleStateStore(storage.Path);
        var second = new LocalJsonDeveloperLifecycleStateStore(storage.Path);

        await first.SaveAsync(State("Exact-Task"));
        var firstFile = Assert.Single(StateFiles(storage.Path));
        await second.SaveAsync(State("Exact-Task", "replacement.md"));

        Assert.Equal(firstFile, Assert.Single(StateFiles(storage.Path)));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("..\\outside")]
    [InlineData("folder/task")]
    [InlineData("DEV:0019 ? * unicode-ä")]
    public async Task TaskId_UnusualAndTraversalTextCannotEscapeOrCreateDirectories(string taskId)
    {
        using var parent = TemporaryDirectory.Create();
        var storage = Path.Combine(parent.Path, "storage");
        var store = new LocalJsonDeveloperLifecycleStateStore(storage);

        await store.SaveAsync(State(taskId));
        var loaded = await store.LoadAsync(taskId);

        Assert.Equal(taskId, loaded!.TaskId);
        Assert.Single(StateFiles(storage));
        Assert.Empty(Directory.GetDirectories(storage));
        Assert.Single(Directory.GetFiles(storage));
    }

    [Fact]
    public async Task LoadMissingAndDeleteMissingAreNormalAndDeleteIsIdempotent()
    {
        using var storage = TemporaryDirectory.Create();
        var store = new LocalJsonDeveloperLifecycleStateStore(storage.Path);

        Assert.Null(await store.LoadAsync("missing"));
        await store.DeleteAsync("missing");
        await store.SaveAsync(State("DEV-0019"));
        await store.DeleteAsync("DEV-0019");
        await store.DeleteAsync("DEV-0019");

        Assert.Null(await store.LoadAsync("DEV-0019"));
    }

    [Fact]
    public async Task SuccessfulSaveLeavesOneValidFinalFileAndNoTemporaryFile()
    {
        using var storage = TemporaryDirectory.Create();
        var store = new LocalJsonDeveloperLifecycleStateStore(storage.Path);

        await store.SaveAsync(State("DEV-0019"));

        var final = Assert.Single(StateFiles(storage.Path));
        Assert.EndsWith(".lifecycle.json", final, StringComparison.Ordinal);
        Assert.Empty(TemporaryFiles(storage.Path));
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(final));
        Assert.Equal("DEV-0019", json.RootElement.GetProperty("taskId").GetString());
    }

    [Fact]
    public async Task PreCancelledReplacementPreservesExistingGoodStateAndLeavesNoTemporaryFile()
    {
        using var storage = TemporaryDirectory.Create();
        var store = new LocalJsonDeveloperLifecycleStateStore(storage.Path);
        await store.SaveAsync(State("DEV-0019", "good.md"));
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.SaveAsync(State("DEV-0019", "cancelled.md"), source.Token));

        Assert.Equal("good.md", (await store.LoadAsync("DEV-0019"))!.TaskFilePath);
        Assert.Empty(TemporaryFiles(storage.Path));
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"taskId\":\"DEV-0019\",\"savedAtUtc\":\"2026-01-01T00:00:00.0000000+00:00\"}")]
    public async Task Load_MalformedOrMissingRequiredDataFailsClearly(string json)
    {
        using var storage = TemporaryDirectory.Create();
        var store = new LocalJsonDeveloperLifecycleStateStore(storage.Path);
        await store.SaveAsync(State("DEV-0019"));
        await File.WriteAllTextAsync(Assert.Single(StateFiles(storage.Path)), json);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync("DEV-0019"));
    }

    public static TheoryData<string> InvalidPersistedData => new()
    {
        { "pull-request-number" },
        { "equal-branches" },
        { "non-utc-timestamp" },
        { "whitespace-task-path" }
    };

    [Theory]
    [MemberData(nameof(InvalidPersistedData))]
    public async Task Load_InvalidPersistedDomainValuesFailClearly(string invalidValue)
    {
        using var storage = TemporaryDirectory.Create();
        var store = new LocalJsonDeveloperLifecycleStateStore(storage.Path);
        await store.SaveAsync(State("DEV-0019", "task.md"));
        var path = Assert.Single(StateFiles(storage.Path));
        var json = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
        var context = json["resumeContext"]!.AsObject();
        switch (invalidValue)
        {
            case "pull-request-number":
                context["pullRequestNumber"] = 0;
                break;
            case "equal-branches":
                context["featureBranch"] = "main";
                context["baseBranch"] = "main";
                break;
            case "non-utc-timestamp":
                json["savedAtUtc"] = "2026-01-01T01:00:00.0000000+01:00";
                break;
            default:
                json["taskFilePath"] = "   ";
                break;
        }

        await File.WriteAllTextAsync(path, json.ToJsonString());

        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync("DEV-0019"));
    }

    [Fact]
    public async Task PersistedJsonContainsRequiredSchemaAndNoCredentialFields()
    {
        using var storage = TemporaryDirectory.Create();
        var store = new LocalJsonDeveloperLifecycleStateStore(storage.Path);
        await store.SaveAsync(State("DEV-0019"));
        var json = await File.ReadAllTextAsync(Assert.Single(StateFiles(storage.Path)));

        Assert.Contains("taskId", json, StringComparison.Ordinal);
        Assert.Contains("resumeContext", json, StringComparison.Ordinal);
        Assert.Contains("repositoryOwner", json, StringComparison.Ordinal);
        Assert.Contains("pullRequestNumber", json, StringComparison.Ordinal);
        Assert.DoesNotContain("authorization", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("headSha", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mergeSha", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreCancelledOperationsPropagateCancellation()
    {
        using var storage = TemporaryDirectory.Create();
        var store = new LocalJsonDeveloperLifecycleStateStore(storage.Path);
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(State("save"), source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.LoadAsync("load", source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.DeleteAsync("delete", source.Token));
    }

    [Fact]
    public async Task StoreOperationsRejectInvalidTaskIdOrNullState()
    {
        using var storage = TemporaryDirectory.Create();
        var store = new LocalJsonDeveloperLifecycleStateStore(storage.Path);

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.SaveAsync(null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.LoadAsync("   "));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.DeleteAsync(""));
    }

    private static DeveloperLifecycleResumeContext Context() => new(
        "Exact/Repository Directory",
        new GitHubRepositoryIdentity("ExactOwner", "ExactRepository"),
        82,
        "Feature/Exact",
        "Main",
        "Exact-Remote");

    private static DeveloperLifecyclePersistedState State(
        string taskId,
        string? taskFilePath = null) => new(
            taskId,
            taskFilePath,
            Context(),
            new DateTimeOffset(2026, 8, 14, 12, 34, 56, TimeSpan.Zero));

    private static string[] StateFiles(string directory) =>
        Directory.GetFiles(directory, "*.lifecycle.json", SearchOption.TopDirectoryOnly);

    private static string[] TemporaryFiles(string directory) =>
        Directory.GetFiles(directory, "*.tmp", SearchOption.TopDirectoryOnly);
}
