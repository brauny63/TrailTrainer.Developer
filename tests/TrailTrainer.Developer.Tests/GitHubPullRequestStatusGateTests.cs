using System.Net;
using System.Net.Http.Headers;
using System.Text;
using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.GitHub;

namespace TrailTrainer.Developer.Tests;

public sealed class GitHubPullRequestStatusGateTests
{
    public static TheoryData<string, string?, PullRequestCheckState> CheckRunStates => new()
    {
        { "queued", null, PullRequestCheckState.Pending },
        { "in_progress", null, PullRequestCheckState.Pending },
        { "waiting", null, PullRequestCheckState.Pending },
        { "unknown", null, PullRequestCheckState.Pending },
        { "completed", "success", PullRequestCheckState.Successful },
        { "completed", "neutral", PullRequestCheckState.Successful },
        { "completed", "skipped", PullRequestCheckState.Successful },
        { "completed", "failure", PullRequestCheckState.Failed },
        { "completed", "cancelled", PullRequestCheckState.Failed },
        { "completed", "timed_out", PullRequestCheckState.Failed },
        { "completed", "action_required", PullRequestCheckState.Failed },
        { "completed", "startup_failure", PullRequestCheckState.Failed },
        { "completed", "stale", PullRequestCheckState.Failed },
        { "completed", "future_conclusion", PullRequestCheckState.Failed },
        { "completed", null, PullRequestCheckState.Failed }
    };

    [Theory]
    [MemberData(nameof(CheckRunStates))]
    public async Task EvaluateAsync_NormalizesCheckRuns(
        string status,
        string? conclusion,
        PullRequestCheckState expected)
    {
        var handler = StandardHandler(CheckRunsJson(CheckRun("build", status, conclusion)), StatusesJson());

        var result = await CreateService(handler).EvaluateAsync(Repository(), 17);

        var check = Assert.Single(result.Checks);
        Assert.Equal(expected, check.State);
    }

    public static TheoryData<string, PullRequestCheckState> CommitStatusStates => new()
    {
        { "pending", PullRequestCheckState.Pending },
        { "success", PullRequestCheckState.Successful },
        { "failure", PullRequestCheckState.Failed },
        { "error", PullRequestCheckState.Failed },
        { "future_state", PullRequestCheckState.Failed }
    };

    [Theory]
    [MemberData(nameof(CommitStatusStates))]
    public async Task EvaluateAsync_NormalizesCommitStatuses(string state, PullRequestCheckState expected)
    {
        var handler = StandardHandler(CheckRunsJson(), StatusesJson(Status("quality", state)));

        var result = await CreateService(handler).EvaluateAsync(Repository(), 17);

        Assert.Equal(expected, Assert.Single(result.Checks).State);
    }

    [Fact]
    public async Task EvaluateAsync_MapsNamesUrlsAndPreservesDuplicateNames()
    {
        var handler = StandardHandler(
            CheckRunsJson(CheckRun("build", "completed", "success", "https://checks.example/1")),
            StatusesJson(Status("build", "success", "https://statuses.example/1")));

        var result = await CreateService(handler).EvaluateAsync(Repository(), 17);

        Assert.Equal(2, result.Checks.Count);
        Assert.Equal(["build", "build"], result.Checks.Select(check => check.Name));
        Assert.Equal(new Uri("https://checks.example/1"), result.Checks[0].DetailsUrl);
        Assert.Equal(new Uri("https://statuses.example/1"), result.Checks[1].DetailsUrl);
    }

    [Theory]
    [InlineData("", "", PullRequestGateState.Pending)]
    [InlineData("success", "", PullRequestGateState.Successful)]
    [InlineData("success", "pending", PullRequestGateState.Pending)]
    [InlineData("pending", "failure", PullRequestGateState.Failed)]
    [InlineData("failure", "pending", PullRequestGateState.Failed)]
    public async Task EvaluateAsync_EvaluatesCombinedGateWithFailurePrecedence(
        string checkConclusion,
        string statusState,
        PullRequestGateState expected)
    {
        var checks = checkConclusion.Length == 0
            ? CheckRunsJson()
            : CheckRunsJson(CheckRun("check", checkConclusion == "pending" ? "queued" : "completed", checkConclusion));
        var statuses = statusState.Length == 0
            ? StatusesJson()
            : StatusesJson(Status("status", statusState));
        var handler = StandardHandler(checks, statuses);

        var result = await CreateService(handler).EvaluateAsync(Repository(), 17);

        Assert.Equal(expected, result.State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task EvaluateAsync_InvalidPullRequestNumberRejectedBeforeHttp(int number)
    {
        var handler = StandardHandler(CheckRunsJson(), StatusesJson());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CreateService(handler).EvaluateAsync(Repository(), number));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task EvaluateAsync_NullRepositoryRejectedBeforeHttp()
    {
        var handler = StandardHandler(CheckRunsJson(), StatusesJson());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CreateService(handler).EvaluateAsync(null!, 17));

        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"head\":{}}")]
    [InlineData("{\"head\":{\"sha\":\"   \"}}")]
    public async Task EvaluateAsync_MissingHeadShaFailsBeforeCommitRequests(string pullRequestJson)
    {
        var handler = new RecordingHandler(_ => JsonResponse(pullRequestJson));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateService(handler).EvaluateAsync(Repository(), 17));

        Assert.Contains("head commit SHA", exception.Message, StringComparison.Ordinal);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task EvaluateAsync_UsesExactHeadShaAfterPullRequestRequest()
    {
        const string sha = "AbC123/exact";
        var handler = StandardHandler(CheckRunsJson(), StatusesJson(), sha);

        var result = await CreateService(handler).EvaluateAsync(Repository(), 17);

        Assert.Equal(sha, result.HeadSha);
        Assert.Equal(17, result.PullRequestNumber);
        Assert.Equal(3, handler.Requests.Count);
        Assert.EndsWith("/pulls/17", handler.Requests[0].Uri.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("/commits/AbC123%2Fexact/check-runs", handler.Requests[1].Uri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains("/commits/AbC123%2Fexact/status", handler.Requests[2].Uri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_FollowsCheckRunAndCommitStatusPagination()
    {
        var call = 0;
        var handler = new RecordingHandler(_ =>
        {
            call++;
            return call switch
            {
                1 => JsonResponse(PullRequestJson("sha")),
                2 => JsonResponse(CheckRunsJson(CheckRun("check-1", "completed", "success")),
                    "<https://api.test.example/root/repos/owner/repo/commits/sha/check-runs?per_page=100&page=2>; rel=\"next\""),
                3 => JsonResponse(CheckRunsJson(CheckRun("check-2", "completed", "success"))),
                4 => JsonResponse(StatusesJson(Status("status-1", "success")),
                    "<https://api.test.example/root/repos/owner/repo/commits/sha/status?per_page=100&page=2>; rel=\"next\""),
                5 => JsonResponse(StatusesJson(Status("status-2", "success"))),
                _ => throw new InvalidOperationException("Unexpected request.")
            };
        });

        var result = await CreateService(handler).EvaluateAsync(Repository(), 17);

        Assert.Equal(["check-1", "check-2", "status-1", "status-2"], result.Checks.Select(check => check.Name));
        Assert.Equal(5, handler.Requests.Count);
        Assert.Equal(PullRequestGateState.Successful, result.State);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task EvaluateAsync_NonSuccessAtEachStageFailsClearly(int failingCall)
    {
        var call = 0;
        var handler = new RecordingHandler(_ =>
        {
            call++;
            if (call == failingCall)
            {
                return new HttpResponseMessage(HttpStatusCode.BadGateway) { ReasonPhrase = "Bad Gateway" };
            }

            return call switch
            {
                1 => JsonResponse(PullRequestJson("sha")),
                2 => JsonResponse(CheckRunsJson()),
                _ => JsonResponse(StatusesJson())
            };
        });

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateService(handler).EvaluateAsync(Repository(), 17));

        Assert.Contains("502", exception.Message, StringComparison.Ordinal);
        Assert.Equal(failingCall, handler.Requests.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task EvaluateAsync_MalformedJsonAtEachStageFailsClearly(int malformedCall)
    {
        var call = 0;
        var handler = new RecordingHandler(_ =>
        {
            call++;
            if (call == malformedCall)
            {
                return JsonResponse("not-json");
            }

            return call switch
            {
                1 => JsonResponse(PullRequestJson("sha")),
                2 => JsonResponse(CheckRunsJson()),
                _ => JsonResponse(StatusesJson())
            };
        });

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateService(handler).EvaluateAsync(Repository(), 17));
    }

    [Fact]
    public async Task EvaluateAsync_ConfiguredBaseAddressAndHeadersAreUsed()
    {
        var handler = StandardHandler(CheckRunsJson(), StatusesJson());
        using var client = new HttpClient(handler);
        var service = new GitHubPullRequestStatusGate(client, new Uri("https://git.example/api/v3"));

        await service.EvaluateAsync(Repository(), 17);

        Assert.StartsWith("https://git.example/api/v3/repos/owner/repo/", handler.Requests[0].Uri.AbsoluteUri);
        Assert.All(handler.Requests, request =>
        {
            Assert.Contains("application/vnd.github+json", request.Accept, StringComparison.Ordinal);
            Assert.Equal("2022-11-28", request.ApiVersion);
            Assert.Contains("TrailTrainer.Developer", request.UserAgent, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task EvaluateAsync_AuthorizationIsNotExposedOnFailure()
    {
        const string secret = "status-gate-secret";
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            ReasonPhrase = "Unauthorized",
            Content = new StringContent(secret)
        });
        using var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        var service = new GitHubPullRequestStatusGate(client);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.EvaluateAsync(Repository(), 17));

        Assert.DoesNotContain(secret, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_CancellationPropagates()
    {
        var handler = new RecordingHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return JsonResponse(PullRequestJson("sha"));
        });
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateService(handler).EvaluateAsync(Repository(), 17, source.Token));
    }

    [Fact]
    public void CoreModels_ValidateAndDefensivelyCopyCollections()
    {
        Assert.ThrowsAny<ArgumentException>(() => new PullRequestCheck(" ", PullRequestCheckState.Pending));
        var source = new List<PullRequestCheck> { new("build", PullRequestCheckState.Successful) };
        var result = new PullRequestStatusGateResult(17, "sha", PullRequestGateState.Successful, source);

        source.Clear();

        Assert.Single(result.Checks);
    }

    private static RecordingHandler StandardHandler(
        string checkRuns,
        string statuses,
        string sha = "head-sha") => new(request =>
    {
        if (request.RequestUri!.AbsolutePath.EndsWith("/pulls/17", StringComparison.Ordinal))
        {
            return JsonResponse(PullRequestJson(sha));
        }

        return request.RequestUri.AbsolutePath.EndsWith("/check-runs", StringComparison.Ordinal)
            ? JsonResponse(checkRuns)
            : JsonResponse(statuses);
    });

    private static GitHubPullRequestStatusGate CreateService(HttpMessageHandler handler) =>
        new(new HttpClient(handler), new Uri("https://api.test.example/root/"));

    private static GitHubRepositoryIdentity Repository() => new("owner", "repo");

    private static string PullRequestJson(string sha) => $"{{\"head\":{{\"sha\":\"{sha}\"}}}}";

    private static string CheckRunsJson(params string[] checks) =>
        $"{{\"check_runs\":[{string.Join(',', checks)}]}}";

    private static string CheckRun(string name, string status, string? conclusion, string? detailsUrl = null) =>
        $"{{\"name\":\"{name}\",\"status\":\"{status}\",\"conclusion\":{JsonString(conclusion)}," +
        $"\"details_url\":{JsonString(detailsUrl)}}}";

    private static string StatusesJson(params string[] statuses) =>
        $"{{\"state\":\"success\",\"statuses\":[{string.Join(',', statuses)}]}}";

    private static string Status(string context, string state, string? targetUrl = null) =>
        $"{{\"context\":\"{context}\",\"state\":\"{state}\",\"target_url\":{JsonString(targetUrl)}}}";

    private static string JsonString(string? value) => value is null ? "null" : $"\"{value}\"";

    private static HttpResponseMessage JsonResponse(string json, string? link = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (link is not null)
        {
            response.Headers.TryAddWithoutValidation("Link", link);
        }

        return response;
    }

    private sealed record RecordedRequest(
        Uri Uri,
        string Accept,
        string? ApiVersion,
        string UserAgent);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
            : this((request, _) => Task.FromResult(responseFactory(request)))
        {
        }

        public RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            this.responseFactory = responseFactory;
        }

        public List<RecordedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.RequestUri!,
                string.Join(',', request.Headers.Accept),
                request.Headers.TryGetValues("X-GitHub-Api-Version", out var versions)
                    ? Assert.Single(versions)
                    : null,
                string.Join(',', request.Headers.UserAgent)));
            return responseFactory(request, cancellationToken);
        }
    }
}
