using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.GitHub;

namespace TrailTrainer.Developer.Tests;

public sealed class GitHubPullRequestServiceTests
{
    [Theory]
    [InlineData(null, "repo")]
    [InlineData("", "repo")]
    [InlineData("   ", "repo")]
    [InlineData("owner", null)]
    [InlineData("owner", "")]
    [InlineData("owner", "   ")]
    public void RepositoryIdentity_InvalidOwnerOrRepository_Throws(string? owner, string? repository)
    {
        Assert.ThrowsAny<ArgumentException>(() => new GitHubRepositoryIdentity(owner!, repository!));
    }

    public static TheoryData<string?, string?, string?> InvalidServiceInputs => new()
    {
        { null, "main", "title" },
        { "   ", "main", "title" },
        { "feature", null, "title" },
        { "feature", "   ", "title" },
        { "feature", "main", null },
        { "feature", "main", "   " }
    };

    [Theory]
    [MemberData(nameof(InvalidServiceInputs))]
    public async Task EnsureOpenAsync_InvalidInput_RejectsBeforeHttp(
        string? head,
        string? @base,
        string? title)
    {
        var handler = new RecordingHandler(_ => JsonResponse("[]"));
        var service = CreateService(handler);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.EnsureOpenAsync(
            Repository(), head!, @base!, title!));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task EnsureOpenAsync_EqualHeadAndBase_RejectsBeforeHttp()
    {
        var handler = new RecordingHandler(_ => JsonResponse("[]"));
        var service = CreateService(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => service.EnsureOpenAsync(
            Repository(), "main", "main", "title"));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task EnsureOpenAsync_NoExistingMatch_LooksUpThenCreatesWithExactPayloadAndParsesResult()
    {
        var handler = new RecordingHandler(request => request.Method == HttpMethod.Get
            ? JsonResponse("[]")
            : JsonResponse(PullRequestJson(42, "Exact Title", "Feature/Exact", "main", true)));
        var service = CreateService(handler);

        var result = await service.EnsureOpenAsync(
            Repository(),
            "Feature/Exact",
            "main",
            "Exact Title",
            "Exact body\nValue",
            draft: true);

        Assert.True(result.Created);
        Assert.Equal(42, result.PullRequest.Number);
        Assert.Equal(new Uri("https://github.example/pulls/42"), result.PullRequest.Url);
        Assert.Equal("Exact Title", result.PullRequest.Title);
        Assert.Equal("Feature/Exact", result.PullRequest.HeadBranch);
        Assert.Equal("main", result.PullRequest.BaseBranch);
        Assert.True(result.PullRequest.IsDraft);
        Assert.Equal([HttpMethod.Get, HttpMethod.Post], handler.Requests.Select(request => request.Method));
        using var payload = JsonDocument.Parse(handler.Requests[1].Body!);
        Assert.Equal("Exact Title", payload.RootElement.GetProperty("title").GetString());
        Assert.Equal("Feature/Exact", payload.RootElement.GetProperty("head").GetString());
        Assert.Equal("main", payload.RootElement.GetProperty("base").GetString());
        Assert.Equal("Exact body\nValue", payload.RootElement.GetProperty("body").GetString());
        Assert.True(payload.RootElement.GetProperty("draft").GetBoolean());
    }

    [Fact]
    public async Task EnsureOpenAsync_OneExactOpenMatch_ReturnsExistingWithoutCreate()
    {
        var candidates = "[" +
            PullRequestJson(1, "wrong head", "feature", "main", false, "closed") + "," +
            PullRequestJson(2, "wrong case", "Feature/Exact", "Main", false) + "," +
            PullRequestJson(3, "match", "Feature/Exact", "main", false) + "]";
        var handler = new RecordingHandler(_ => JsonResponse(candidates));
        var service = CreateService(handler);

        var result = await service.EnsureOpenAsync(
            Repository(), "Feature/Exact", "main", "unused title");

        Assert.False(result.Created);
        Assert.Equal(3, result.PullRequest.Number);
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Contains("state=open", handler.Requests[0].Uri.Query, StringComparison.Ordinal);
        Assert.Contains("head=owner%3AFeature%2FExact", handler.Requests[0].Uri.Query, StringComparison.Ordinal);
        Assert.Contains("base=main", handler.Requests[0].Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureOpenAsync_MultipleExactMatches_ThrowsAmbiguousWithoutCreate()
    {
        var json = "[" + PullRequestJson(1, "one", "feature", "main", false) + "," +
                   PullRequestJson(2, "two", "feature", "main", false) + "]";
        var handler = new RecordingHandler(_ => JsonResponse(json));
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnsureOpenAsync(
            Repository(), "feature", "main", "title"));

        Assert.Contains("Multiple", exception.Message, StringComparison.Ordinal);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task EnsureOpenAsync_ConfiguredBaseAddressIsHonored()
    {
        var handler = new RecordingHandler(_ => JsonResponse(PullRequestArrayJson(1, "feature", "main")));
        using var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        var service = new GitHubPullRequestService(client, new Uri("https://git.example/api/v3"));

        await service.EnsureOpenAsync(Repository(), "feature", "main", "title");

        Assert.StartsWith("https://git.example/api/v3/repos/owner/repo/pulls", handler.Requests[0].Uri.AbsoluteUri);
    }

    [Fact]
    public async Task EnsureOpenAsync_RepeatedCallFindsExistingAndDoesNotCreateAgain()
    {
        var call = 0;
        var handler = new RecordingHandler(request =>
        {
            call++;
            return call switch
            {
                1 => JsonResponse("[]"),
                2 => JsonResponse(PullRequestJson(10, "title", "feature", "main", false)),
                3 => JsonResponse(PullRequestArrayJson(10, "feature", "main")),
                _ => throw new InvalidOperationException("Unexpected HTTP request.")
            };
        });
        var service = CreateService(handler);

        var first = await service.EnsureOpenAsync(Repository(), "feature", "main", "title");
        var second = await service.EnsureOpenAsync(Repository(), "feature", "main", "title");

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(1, handler.Requests.Count(request => request.Method == HttpMethod.Post));
    }

    [Fact]
    public async Task EnsureOpenAsync_NonSuccessLookup_ThrowsWithoutLeakingAuthorization()
    {
        const string secret = "super-secret-token";
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            ReasonPhrase = "Unauthorized",
            Content = new StringContent(secret)
        });
        using var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        var service = new GitHubPullRequestService(client);

        var exception = await Assert.ThrowsAsync<GitHubApiException>(() => service.EnsureOpenAsync(
            Repository(), "feature", "main", "title"));

        Assert.Contains("401", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, exception.ToString(), StringComparison.Ordinal);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task EnsureOpenAsync_MissingCredential_FailsBeforeHttp()
    {
        var handler = new RecordingHandler(_ => JsonResponse("[]"));
        var service = new GitHubPullRequestService(new HttpClient(handler), new Uri("https://api.test.example/"));

        var exception = await Assert.ThrowsAsync<GitHubApiException>(() => service.EnsureOpenAsync(
            Repository(), "feature", "main", "title"));

        Assert.Equal(GitHubApiFailureKind.AuthenticationMissing, exception.FailureKind);
        Assert.Contains("GitHub:Token", exception.Message, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, GitHubApiFailureKind.AuthenticationRejected)]
    [InlineData(HttpStatusCode.Forbidden, GitHubApiFailureKind.InsufficientRepositoryAccess)]
    [InlineData(HttpStatusCode.NotFound, GitHubApiFailureKind.RepositoryNotFoundOrPrivateAccessDenied)]
    [InlineData(HttpStatusCode.TooManyRequests, GitHubApiFailureKind.RateLimited)]
    [InlineData(HttpStatusCode.BadGateway, GitHubApiFailureKind.HttpFailure)]
    public async Task EnsureOpenAsync_GitHubFailure_IsClassified(
        HttpStatusCode status,
        GitHubApiFailureKind expectedKind)
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(status));
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<GitHubApiException>(() => service.EnsureOpenAsync(
            Repository(), "feature", "main", "title"));

        Assert.Equal(expectedKind, exception.FailureKind);
    }

    [Fact]
    public async Task EnsureOpenAsync_ForbiddenRateLimit_IsClassifiedSeparately()
    {
        var handler = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
            response.Headers.Add("X-RateLimit-Remaining", "0");
            return response;
        });

        var exception = await Assert.ThrowsAsync<GitHubApiException>(() => CreateService(handler).EnsureOpenAsync(
            Repository(), "feature", "main", "title"));

        Assert.Equal(GitHubApiFailureKind.RateLimited, exception.FailureKind);
    }

    [Fact]
    public async Task ProbeAsync_UsesSameAuthenticationPathWithoutMutation()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{}"));
        var service = CreateService(handler);

        await service.ProbeAsync(Repository(), checkOpenPullRequests: true);

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
        Assert.Contains("/repos/owner/repo", handler.Requests[0].Uri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains("/pulls?state=open", handler.Requests[1].Uri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureOpenAsync_NonSuccessCreate_Throws()
    {
        var handler = new RecordingHandler(request => request.Method == HttpMethod.Get
            ? JsonResponse("[]")
            : new HttpResponseMessage(HttpStatusCode.UnprocessableEntity) { ReasonPhrase = "Unprocessable Entity" });
        var service = CreateService(handler);

        await Assert.ThrowsAsync<GitHubApiException>(() => service.EnsureOpenAsync(
            Repository(), "feature", "main", "title"));

        Assert.Equal(2, handler.Requests.Count);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    public async Task EnsureOpenAsync_MalformedLookupResponse_FailsClearly(string response)
    {
        var handler = new RecordingHandler(_ => JsonResponse(response));
        var service = CreateService(handler);

        await Assert.ThrowsAnyAsync<InvalidDataException>(() => service.EnsureOpenAsync(
            Repository(), "feature", "main", "title"));
    }

    [Fact]
    public async Task EnsureOpenAsync_IncompleteCreateResponse_FailsClearly()
    {
        var handler = new RecordingHandler(request => request.Method == HttpMethod.Get
            ? JsonResponse("[]")
            : JsonResponse("{}"));
        var service = CreateService(handler);

        await Assert.ThrowsAsync<InvalidDataException>(() => service.EnsureOpenAsync(
            Repository(), "feature", "main", "title"));
    }

    [Fact]
    public async Task EnsureOpenAsync_CancellationPropagatesToHttp()
    {
        var handler = new RecordingHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return JsonResponse("[]");
        });
        var service = CreateService(handler);
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.EnsureOpenAsync(
            Repository(), "feature", "main", "title", cancellationToken: source.Token));
    }

    private static GitHubPullRequestService CreateService(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        return new GitHubPullRequestService(client, new Uri("https://api.test.example/root/"));
    }

    private static GitHubRepositoryIdentity Repository() => new("owner", "repo");

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static string PullRequestArrayJson(int number, string head, string @base) =>
        "[" + PullRequestJson(number, "title", head, @base, false) + "]";

    private static string PullRequestJson(
        int number,
        string title,
        string head,
        string @base,
        bool draft,
        string state = "open") => JsonSerializer.Serialize(new
        {
            number,
            html_url = $"https://github.example/pulls/{number}",
            title,
            state,
            draft,
            head = new { @ref = head },
            @base = new { @ref = @base }
        });

    private sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Body);

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

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, body));
            return await responseFactory(request, cancellationToken);
        }
    }
}
