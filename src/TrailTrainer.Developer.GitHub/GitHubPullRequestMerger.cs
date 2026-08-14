using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.GitHub;

public sealed class GitHubPullRequestMerger : IPullRequestMerger
{
    private static readonly Uri PublicApiBaseAddress = new("https://api.github.com/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient httpClient;
    private readonly Uri apiBaseAddress;

    public GitHubPullRequestMerger(HttpClient httpClient, Uri? apiBaseAddress = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.apiBaseAddress = EnsureTrailingSlash(apiBaseAddress ?? httpClient.BaseAddress ?? PublicApiBaseAddress);
    }

    public async Task<PullRequestMergeResult> MergeAsync(
        GitHubRepositoryIdentity repository,
        int pullRequestNumber,
        string expectedHeadSha,
        PullRequestMergeMethod method,
        string? commitTitle = null,
        string? commitMessage = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (pullRequestNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pullRequestNumber));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(expectedHeadSha);
        var methodValue = method switch
        {
            PullRequestMergeMethod.Merge => "merge",
            PullRequestMergeMethod.Squash => "squash",
            PullRequestMergeMethod.Rebase => "rebase",
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        };

        var endpoint = new Uri(
            apiBaseAddress,
            $"repos/{Uri.EscapeDataString(repository.Owner)}/" +
            $"{Uri.EscapeDataString(repository.Repository)}/pulls/{pullRequestNumber}/merge");
        using var request = CreateRequest(endpoint);
        request.Content = JsonContent.Create(
            new GitHubMergeRequestDto(expectedHeadSha, methodValue, commitTitle, commitMessage),
            options: JsonOptions);
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        EnsureSuccess(response, cancellationToken);

        GitHubMergeResponseDto? payload;
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            payload = await JsonSerializer.DeserializeAsync<GitHubMergeResponseDto>(
                stream,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("GitHub returned malformed merge response data.", exception);
        }

        if (payload?.Merged is null)
        {
            throw new InvalidDataException("GitHub returned incomplete merge response data.");
        }

        if (payload.Merged.Value && string.IsNullOrWhiteSpace(payload.Sha))
        {
            throw new InvalidDataException("GitHub confirmed the merge without returning a merge commit SHA.");
        }

        return new PullRequestMergeResult(
            pullRequestNumber,
            payload.Merged.Value,
            payload.Sha,
            method);
    }

    private static HttpRequestMessage CreateRequest(Uri endpoint)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, endpoint);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        request.Headers.UserAgent.ParseAdd("TrailTrainer.Developer");
        return request;
    }

    private static void EnsureSuccess(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"GitHub failed to merge the Pull Request: HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).",
                inner: null,
                response.StatusCode);
        }
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        if (!uri.IsAbsoluteUri)
        {
            throw new ArgumentException("The GitHub API base address must be absolute.", nameof(uri));
        }

        return uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri(uri.AbsoluteUri + "/");
    }

    private sealed record GitHubMergeRequestDto(
        [property: JsonPropertyName("sha")] string ExpectedHeadSha,
        [property: JsonPropertyName("merge_method")] string Method,
        [property: JsonPropertyName("commit_title")] string? CommitTitle,
        [property: JsonPropertyName("commit_message")] string? CommitMessage);

    private sealed record GitHubMergeResponseDto
    {
        [JsonPropertyName("sha")]
        public string? Sha { get; init; }

        [JsonPropertyName("merged")]
        public bool? Merged { get; init; }
    }
}
