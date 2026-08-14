using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Persistence;

public sealed class LocalJsonDeveloperLifecycleStateStore : IDeveloperLifecycleStateStore
{
    private const string StateFileExtension = ".lifecycle.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private readonly string storageDirectory;

    public LocalJsonDeveloperLifecycleStateStore(string storageDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageDirectory);
        this.storageDirectory = Path.GetFullPath(storageDirectory);
    }

    public async Task SaveAsync(
        DeveloperLifecyclePersistedState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(storageDirectory);
        var targetPath = StatePath(state.TaskId);
        var temporaryPath = Path.Combine(
            storageDirectory,
            $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var dto = ToDto(state);
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, dto, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
        }
    }

    public async Task<DeveloperLifecyclePersistedState?> LoadAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        ValidateTaskId(taskId);
        cancellationToken.ThrowIfCancellationRequested();
        var path = StatePath(taskId);
        if (!File.Exists(path))
        {
            return null;
        }

        PersistedStateDto? dto;
        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            dto = await JsonSerializer.DeserializeAsync<PersistedStateDto>(
                stream,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The persisted lifecycle state contains malformed JSON.", exception);
        }

        try
        {
            return FromDto(dto ?? throw new InvalidDataException(
                "The persisted lifecycle state is empty."));
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw new InvalidDataException("The persisted lifecycle state contains invalid required data.", exception);
        }
    }

    public Task DeleteAsync(string taskId, CancellationToken cancellationToken = default)
    {
        ValidateTaskId(taskId);
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(StatePath(taskId));
        return Task.CompletedTask;
    }

    private string StatePath(string taskId)
    {
        ValidateTaskId(taskId);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(taskId));
        var fileName = Convert.ToHexStringLower(hash) + StateFileExtension;
        return Path.Combine(storageDirectory, fileName);
    }

    private static void ValidateTaskId(string taskId) =>
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

    private static PersistedStateDto ToDto(DeveloperLifecyclePersistedState state) => new()
    {
        TaskId = state.TaskId,
        TaskFilePath = state.TaskFilePath,
        SavedAtUtc = state.SavedAtUtc.ToString("O"),
        ResumeContext = new ResumeContextDto
        {
            RepositoryDirectory = state.ResumeContext.RepositoryDirectory,
            RepositoryOwner = state.ResumeContext.Repository.Owner,
            RepositoryName = state.ResumeContext.Repository.Repository,
            PullRequestNumber = state.ResumeContext.PullRequestNumber,
            FeatureBranch = state.ResumeContext.FeatureBranch,
            BaseBranch = state.ResumeContext.BaseBranch,
            GitRemoteName = state.ResumeContext.GitRemoteName
        }
    };

    private static DeveloperLifecyclePersistedState FromDto(PersistedStateDto dto)
    {
        if (dto.TaskId is null || dto.SavedAtUtc is null || dto.ResumeContext is null ||
            dto.ResumeContext.RepositoryDirectory is null || dto.ResumeContext.RepositoryOwner is null ||
            dto.ResumeContext.RepositoryName is null || dto.ResumeContext.PullRequestNumber is null ||
            dto.ResumeContext.FeatureBranch is null || dto.ResumeContext.BaseBranch is null ||
            dto.ResumeContext.GitRemoteName is null)
        {
            throw new InvalidDataException("The persisted lifecycle state is missing required data.");
        }

        if (!DateTimeOffset.TryParseExact(
                dto.SavedAtUtc,
                "O",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var savedAtUtc))
        {
            throw new InvalidDataException("The persisted lifecycle state contains an invalid saved timestamp.");
        }

        var repository = new GitHubRepositoryIdentity(
            dto.ResumeContext.RepositoryOwner,
            dto.ResumeContext.RepositoryName);
        var context = new DeveloperLifecycleResumeContext(
            dto.ResumeContext.RepositoryDirectory,
            repository,
            dto.ResumeContext.PullRequestNumber.Value,
            dto.ResumeContext.FeatureBranch,
            dto.ResumeContext.BaseBranch,
            dto.ResumeContext.GitRemoteName);
        return new DeveloperLifecyclePersistedState(
            dto.TaskId,
            dto.TaskFilePath,
            context,
            savedAtUtc);
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record PersistedStateDto
    {
        [JsonPropertyName("taskId")]
        public string? TaskId { get; init; }

        [JsonPropertyName("taskFilePath")]
        public string? TaskFilePath { get; init; }

        [JsonPropertyName("savedAtUtc")]
        public string? SavedAtUtc { get; init; }

        [JsonPropertyName("resumeContext")]
        public ResumeContextDto? ResumeContext { get; init; }
    }

    private sealed record ResumeContextDto
    {
        [JsonPropertyName("repositoryDirectory")]
        public string? RepositoryDirectory { get; init; }

        [JsonPropertyName("repositoryOwner")]
        public string? RepositoryOwner { get; init; }

        [JsonPropertyName("repositoryName")]
        public string? RepositoryName { get; init; }

        [JsonPropertyName("pullRequestNumber")]
        public int? PullRequestNumber { get; init; }

        [JsonPropertyName("featureBranch")]
        public string? FeatureBranch { get; init; }

        [JsonPropertyName("baseBranch")]
        public string? BaseBranch { get; init; }

        [JsonPropertyName("gitRemoteName")]
        public string? GitRemoteName { get; init; }
    }
}
