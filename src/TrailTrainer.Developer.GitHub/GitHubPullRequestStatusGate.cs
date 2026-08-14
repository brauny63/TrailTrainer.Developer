using System.Text.Json;
using System.Text.Json.Serialization;
using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.GitHub;

public sealed class GitHubPullRequestStatusGate : IPullRequestStatusGate
{
    private static readonly Uri PublicApiBaseAddress = new("https://api.github.com/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient httpClient;
    private readonly Uri apiBaseAddress;

    public GitHubPullRequestStatusGate(HttpClient httpClient, Uri? apiBaseAddress = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.apiBaseAddress = EnsureTrailingSlash(apiBaseAddress ?? httpClient.BaseAddress ?? PublicApiBaseAddress);
    }

    public async Task<PullRequestStatusGateResult> EvaluateAsync(
        GitHubRepositoryIdentity repository,
        int pullRequestNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (pullRequestNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pullRequestNumber));
        }

        var repositoryEndpoint = RepositoryEndpoint(repository);
        var pullRequest = await GetAsync<GitHubPullRequestDto>(
            new Uri(repositoryEndpoint, $"pulls/{pullRequestNumber}"),
            "read the pull request",
            cancellationToken);
        if (string.IsNullOrWhiteSpace(pullRequest?.Head?.Sha))
        {
            throw new InvalidDataException("GitHub returned Pull Request data without a head commit SHA.");
        }

        var headSha = pullRequest.Head.Sha;
        var checks = new List<PullRequestCheck>();
        await AddCheckRunsAsync(repositoryEndpoint, headSha, checks, cancellationToken);
        await AddCommitStatusesAsync(repositoryEndpoint, headSha, checks, cancellationToken);

        return new PullRequestStatusGateResult(
            pullRequestNumber,
            headSha,
            Evaluate(checks),
            checks);
    }

    private async Task AddCheckRunsAsync(
        Uri repositoryEndpoint,
        string headSha,
        ICollection<PullRequestCheck> checks,
        CancellationToken cancellationToken)
    {
        Uri? page = new(repositoryEndpoint, $"commits/{Uri.EscapeDataString(headSha)}/check-runs?per_page=100");
        while (page is not null)
        {
            var (response, payload) = await GetResponseAsync<GitHubCheckRunsDto>(
                page, "read check runs", cancellationToken);
            using (response)
            {
                if (payload?.CheckRuns is null)
                {
                    throw new InvalidDataException("GitHub returned incomplete Check Runs data.");
                }

                foreach (var checkRun in payload.CheckRuns)
                {
                    if (string.IsNullOrWhiteSpace(checkRun.Name))
                    {
                        throw new InvalidDataException("GitHub returned a Check Run without a name.");
                    }

                    checks.Add(new PullRequestCheck(
                        checkRun.Name,
                        NormalizeCheckRun(checkRun.Status, checkRun.Conclusion),
                        OptionalAbsoluteUri(checkRun.DetailsUrl, "Check Run details URL")));
                }

                page = NextPage(response);
            }
        }
    }

    private async Task AddCommitStatusesAsync(
        Uri repositoryEndpoint,
        string headSha,
        ICollection<PullRequestCheck> checks,
        CancellationToken cancellationToken)
    {
        Uri? page = new(repositoryEndpoint, $"commits/{Uri.EscapeDataString(headSha)}/status?per_page=100");
        while (page is not null)
        {
            var (response, payload) = await GetResponseAsync<GitHubCombinedStatusDto>(
                page, "read commit statuses", cancellationToken);
            using (response)
            {
                if (payload?.Statuses is null)
                {
                    throw new InvalidDataException("GitHub returned incomplete commit-status data.");
                }

                foreach (var status in payload.Statuses)
                {
                    if (string.IsNullOrWhiteSpace(status.Context))
                    {
                        throw new InvalidDataException("GitHub returned a commit status without a context name.");
                    }

                    checks.Add(new PullRequestCheck(
                        status.Context,
                        NormalizeCommitStatus(status.State),
                        OptionalAbsoluteUri(status.TargetUrl, "commit-status target URL")));
                }

                page = NextPage(response);
            }
        }
    }

    private async Task<T?> GetAsync<T>(Uri uri, string operation, CancellationToken cancellationToken)
    {
        var (response, payload) = await GetResponseAsync<T>(uri, operation, cancellationToken);
        using (response)
        {
            return payload;
        }
    }

    private async Task<(HttpResponseMessage Response, T? Payload)> GetResponseAsync<T>(
        Uri uri,
        string operation,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(uri);
        var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        try
        {
            EnsureSuccess(response, operation, cancellationToken);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
            return (response, payload);
        }
        catch (JsonException exception)
        {
            response.Dispose();
            throw new InvalidDataException("GitHub returned malformed JSON data.", exception);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private Uri RepositoryEndpoint(GitHubRepositoryIdentity repository) => new(
        apiBaseAddress,
        $"repos/{Uri.EscapeDataString(repository.Owner)}/{Uri.EscapeDataString(repository.Repository)}/");

    private static HttpRequestMessage CreateRequest(Uri uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        request.Headers.UserAgent.ParseAdd("TrailTrainer.Developer");
        return request;
    }

    private static void EnsureSuccess(
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
    }

    private static PullRequestCheckState NormalizeCheckRun(string? status, string? conclusion)
    {
        if (!string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            return PullRequestCheckState.Pending;
        }

        return conclusion?.ToLowerInvariant() switch
        {
            "success" or "neutral" or "skipped" => PullRequestCheckState.Successful,
            _ => PullRequestCheckState.Failed
        };
    }

    private static PullRequestCheckState NormalizeCommitStatus(string? state) =>
        state?.ToLowerInvariant() switch
        {
            "pending" => PullRequestCheckState.Pending,
            "success" => PullRequestCheckState.Successful,
            _ => PullRequestCheckState.Failed
        };

    private static PullRequestGateState Evaluate(IReadOnlyCollection<PullRequestCheck> checks)
    {
        if (checks.Any(check => check.State == PullRequestCheckState.Failed))
        {
            return PullRequestGateState.Failed;
        }

        if (checks.Count == 0 || checks.Any(check => check.State == PullRequestCheckState.Pending))
        {
            return PullRequestGateState.Pending;
        }

        return PullRequestGateState.Successful;
    }

    private static Uri? OptionalAbsoluteUri(string? value, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri
            : throw new InvalidDataException($"GitHub returned an invalid {description}.");
    }

    private static Uri? NextPage(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var values))
        {
            return null;
        }

        foreach (var link in string.Join(",", values).Split(','))
        {
            var parts = link.Split(';', StringSplitOptions.TrimEntries);
            if (parts.Length < 2 || !parts.Skip(1).Any(part =>
                    string.Equals(part, "rel=\"next\"", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var target = parts[0].Trim();
            if (target.Length > 2 && target[0] == '<' && target[^1] == '>' &&
                Uri.TryCreate(target[1..^1], UriKind.Absolute, out var next))
            {
                return next;
            }

            throw new InvalidDataException("GitHub returned an invalid pagination link.");
        }

        return null;
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

    private sealed record GitHubPullRequestDto
    {
        [JsonPropertyName("head")]
        public GitHubHeadDto? Head { get; init; }
    }

    private sealed record GitHubHeadDto
    {
        [JsonPropertyName("sha")]
        public string? Sha { get; init; }
    }

    private sealed record GitHubCheckRunsDto
    {
        [JsonPropertyName("check_runs")]
        public GitHubCheckRunDto[]? CheckRuns { get; init; }
    }

    private sealed record GitHubCheckRunDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("conclusion")]
        public string? Conclusion { get; init; }

        [JsonPropertyName("details_url")]
        public string? DetailsUrl { get; init; }
    }

    private sealed record GitHubCombinedStatusDto
    {
        [JsonPropertyName("statuses")]
        public GitHubCommitStatusDto[]? Statuses { get; init; }
    }

    private sealed record GitHubCommitStatusDto
    {
        [JsonPropertyName("context")]
        public string? Context { get; init; }

        [JsonPropertyName("state")]
        public string? State { get; init; }

        [JsonPropertyName("target_url")]
        public string? TargetUrl { get; init; }
    }
}
