using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Persistence;

internal static class LifecycleStateJsonFormat
{
    public const string StateFileExtension = ".lifecycle.json";
    private const int Sha256HexLength = 64;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string NormalizeStorageDirectory(string storageDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageDirectory);
        return Path.GetFullPath(storageDirectory);
    }

    public static string FileName(string taskId)
    {
        ValidateTaskId(taskId);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(taskId));
        return Convert.ToHexStringLower(hash) + StateFileExtension;
    }

    public static string StatePath(string storageDirectory, string taskId) =>
        Path.Combine(storageDirectory, FileName(taskId));

    public static bool IsFinalStateFileName(string fileName)
    {
        if (fileName.Length != Sha256HexLength + StateFileExtension.Length ||
            !fileName.EndsWith(StateFileExtension, StringComparison.Ordinal))
        {
            return false;
        }

        return fileName.AsSpan(0, Sha256HexLength).IndexOfAnyExcept("0123456789abcdef") < 0;
    }

    public static Task SerializeAsync(
        Stream stream,
        DeveloperLifecyclePersistedState state,
        CancellationToken cancellationToken) =>
        JsonSerializer.SerializeAsync(stream, ToDto(state), JsonOptions, cancellationToken);

    public static async Task<DeveloperLifecyclePersistedState> DeserializeAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        PersistedStateDto? dto;
        try
        {
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
