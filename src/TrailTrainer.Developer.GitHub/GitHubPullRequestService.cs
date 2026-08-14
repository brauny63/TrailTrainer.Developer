using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.GitHub;

public sealed class GitHubPullRequestService : IPullRequestService
{
    private static readonly Uri PublicApiBaseAddress = new("https://api.github.com/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient httpClient;
    private readonly Uri apiBaseAddress;

    public GitHubPullRequestService(HttpClient httpClient, Uri? apiBaseAddress = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.apiBaseAddress = EnsureTrailingSlash(apiBaseAddress ?? httpClient.BaseAddress ?? PublicApiBaseAddress);
    }

    public async Task<PullRequestEnsureResult> EnsureOpenAsync(
        GitHubRepositoryIdentity repository,
        string headBranch,
        string baseBranch,
        string title,
        string? body = null,
        bool draft = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(headBranch);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseBranch);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (string.Equals(headBranch, baseBranch, StringComparison.Ordinal))
        {
            throw new ArgumentException("Head and base branch names must differ.", nameof(headBranch));
        }

        var endpoint = RepositoryPullRequestsEndpoint(repository);
        var query = $"?state=open&head={Uri.EscapeDataString(repository.Owner + ":" + headBranch)}" +
                    $"&base={Uri.EscapeDataString(baseBranch)}";
        using var lookupRequest = CreateRequest(HttpMethod.Get, new Uri(endpoint + query));
        using var lookupResponse = await httpClient.SendAsync(
            lookupRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        await EnsureSuccessAsync(lookupResponse, "look up open pull requests", cancellationToken);

        var candidates = await DeserializeAsync<GitHubPullRequestDto[]>(lookupResponse, cancellationToken) ?? [];
        var matches = candidates.Where(candidate =>
                string.Equals(candidate.State, "open", StringComparison.Ordinal) &&
                string.Equals(candidate.Head?.Reference, headBranch, StringComparison.Ordinal) &&
                string.Equals(candidate.Base?.Reference, baseBranch, StringComparison.Ordinal))
            .Take(2)
            .ToArray();

        if (matches.Length > 1)
        {
            throw new InvalidOperationException(
                $"Multiple open Pull Requests match head '{headBranch}' and base '{baseBranch}'.");
        }

        if (matches.Length == 1)
        {
            return new PullRequestEnsureResult(ToPullRequestInfo(matches[0]), Created: false);
        }

        var creation = new GitHubCreatePullRequestDto(title, headBranch, baseBranch, body, draft);
        using var createRequest = CreateRequest(HttpMethod.Post, endpoint);
        createRequest.Content = JsonContent.Create(creation, options: JsonOptions);
        using var createResponse = await httpClient.SendAsync(
            createRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        await EnsureSuccessAsync(createResponse, "create a pull request", cancellationToken);

        var created = await DeserializeAsync<GitHubPullRequestDto>(createResponse, cancellationToken)
            ?? throw new InvalidDataException("GitHub returned an empty Pull Request response.");
        return new PullRequestEnsureResult(ToPullRequestInfo(created), Created: true);
    }

    private Uri RepositoryPullRequestsEndpoint(GitHubRepositoryIdentity repository)
    {
        var relative = $"repos/{Uri.EscapeDataString(repository.Owner)}/" +
                       $"{Uri.EscapeDataString(repository.Repository)}/pulls";
        return new Uri(apiBaseAddress, relative);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        request.Headers.UserAgent.ParseAdd("TrailTrainer.Developer");
        return request;
    }

    private static async Task<T?> DeserializeAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("GitHub returned malformed JSON data.", exception);
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"GitHub failed to {operation}: HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).",
                inner: null,
                response.StatusCode);
        }

        await Task.CompletedTask;
    }

    private static PullRequestInfo ToPullRequestInfo(GitHubPullRequestDto pullRequest)
    {
        if (pullRequest.Number <= 0 ||
            string.IsNullOrWhiteSpace(pullRequest.HtmlUrl) ||
            !Uri.TryCreate(pullRequest.HtmlUrl, UriKind.Absolute, out var url) ||
            string.IsNullOrWhiteSpace(pullRequest.Title) ||
            string.IsNullOrWhiteSpace(pullRequest.Head?.Reference) ||
            string.IsNullOrWhiteSpace(pullRequest.Base?.Reference))
        {
            throw new InvalidDataException("GitHub returned incomplete Pull Request data.");
        }

        return new PullRequestInfo(
            pullRequest.Number,
            url,
            pullRequest.Title,
            pullRequest.Head.Reference,
            pullRequest.Base.Reference,
            pullRequest.Draft);
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

    private sealed record GitHubCreatePullRequestDto(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("head")] string Head,
        [property: JsonPropertyName("base")] string Base,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("draft")] bool Draft);

    private sealed record GitHubPullRequestDto
    {
        [JsonPropertyName("number")]
        public int Number { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }

        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("state")]
        public string? State { get; init; }

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }

        [JsonPropertyName("head")]
        public GitHubBranchDto? Head { get; init; }

        [JsonPropertyName("base")]
        public GitHubBranchDto? Base { get; init; }
    }

    private sealed record GitHubBranchDto
    {
        [JsonPropertyName("ref")]
        public string? Reference { get; init; }
    }
}
