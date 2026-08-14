using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.GitHub;

namespace TrailTrainer.Developer.Tests;

public sealed class GitHubPullRequestMergerTests
{
    [Theory]
    [InlineData(PullRequestMergeMethod.Merge, "merge")]
    [InlineData(PullRequestMergeMethod.Squash, "squash")]
    [InlineData(PullRequestMergeMethod.Rebase, "rebase")]
    public async Task MergeAsync_MapsMethodAndSendsExactRequest(
        PullRequestMergeMethod method,
        string expectedMethod)
    {
        var handler = new RecordingHandler(_ => JsonResponse("{\"merged\":true,\"sha\":\"merge-sha\"}"));
        var service = CreateService(handler);

        var result = await service.MergeAsync(
            Repository(), 42, "ExactHeadSha", method, "Exact title", "Exact message");

        Assert.True(result.Merged);
        Assert.Equal(42, result.PullRequestNumber);
        Assert.Equal("merge-sha", result.MergeCommitSha);
        Assert.Equal(method, result.Method);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.EndsWith("/repos/owner/repo/pulls/42/merge", request.Uri.AbsolutePath, StringComparison.Ordinal);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("ExactHeadSha", body.RootElement.GetProperty("sha").GetString());
        Assert.Equal(expectedMethod, body.RootElement.GetProperty("merge_method").GetString());
        Assert.Equal("Exact title", body.RootElement.GetProperty("commit_title").GetString());
        Assert.Equal("Exact message", body.RootElement.GetProperty("commit_message").GetString());
    }

    [Theory]
    [InlineData(0, "sha", PullRequestMergeMethod.Merge)]
    [InlineData(-1, "sha", PullRequestMergeMethod.Merge)]
    [InlineData(1, "", PullRequestMergeMethod.Merge)]
    [InlineData(1, "   ", PullRequestMergeMethod.Merge)]
    [InlineData(1, "sha", (PullRequestMergeMethod)99)]
    public async Task MergeAsync_InvalidInputRejectedBeforeHttp(
        int number,
        string sha,
        PullRequestMergeMethod method)
    {
        var handler = new RecordingHandler(_ => JsonResponse("{}"));

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            CreateService(handler).MergeAsync(Repository(), number, sha, method));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task MergeAsync_NullRepositoryRejectedBeforeHttp()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{}"));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CreateService(handler).MergeAsync(null!, 1, "sha", PullRequestMergeMethod.Merge));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task MergeAsync_NullOptionalValuesAreSupportedAndSentUnchanged()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{\"merged\":false,\"sha\":null}"));

        var result = await CreateService(handler).MergeAsync(
            Repository(), 42, "sha", PullRequestMergeMethod.Merge, null, null);

        Assert.False(result.Merged);
        Assert.Null(result.MergeCommitSha);
        using var body = JsonDocument.Parse(Assert.Single(handler.Requests).Body!);
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("commit_title").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("commit_message").ValueKind);
    }

    [Theory]
    [InlineData("{\"merged\":true,\"sha\":null}")]
    [InlineData("{\"merged\":true,\"sha\":\"   \"}")]
    public async Task MergeAsync_SuccessWithoutMergeShaFailsClearly(string response)
    {
        var handler = new RecordingHandler(_ => JsonResponse(response));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateService(handler).MergeAsync(Repository(), 42, "sha", PullRequestMergeMethod.Merge));

        Assert.Contains("merge commit SHA", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("null")]
    public async Task MergeAsync_MalformedOrIncompleteResponseFailsClearly(string response)
    {
        var handler = new RecordingHandler(_ => JsonResponse(response));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateService(handler).MergeAsync(Repository(), 42, "sha", PullRequestMergeMethod.Merge));
    }

    [Fact]
    public async Task MergeAsync_StaleHeadHttpFailurePropagatesWithoutRetry()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            ReasonPhrase = "Conflict"
        });

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateService(handler).MergeAsync(Repository(), 42, "stale", PullRequestMergeMethod.Merge));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task MergeAsync_ConfiguredBaseAddressAndHeadersAreUsed()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{\"merged\":true,\"sha\":\"merged\"}"));
        using var client = new HttpClient(handler);
        var service = new GitHubPullRequestMerger(client, new Uri("https://git.example/api/v3"));

        await service.MergeAsync(Repository(), 42, "sha", PullRequestMergeMethod.Merge);

        var request = Assert.Single(handler.Requests);
        Assert.StartsWith("https://git.example/api/v3/repos/owner/repo/", request.Uri.AbsoluteUri);
        Assert.Contains("application/vnd.github+json", request.Accept, StringComparison.Ordinal);
        Assert.Equal("2022-11-28", request.ApiVersion);
        Assert.Contains("TrailTrainer.Developer", request.UserAgent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MergeAsync_AuthorizationAndResponseBodyNotExposedOnFailure()
    {
        const string secret = "merge-secret-token";
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            ReasonPhrase = "Forbidden",
            Content = new StringContent(secret)
        });
        using var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        var service = new GitHubPullRequestMerger(client);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.MergeAsync(Repository(), 42, "sha", PullRequestMergeMethod.Merge));

        Assert.DoesNotContain(secret, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MergeAsync_CancellationPropagatesToHttp()
    {
        var handler = new RecordingHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return JsonResponse("{}");
        });
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateService(handler).MergeAsync(
                Repository(), 42, "sha", PullRequestMergeMethod.Merge, cancellationToken: source.Token));
    }

    [Fact]
    public void MergeResult_SuccessRequiresShaAndValidMethod()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new PullRequestMergeResult(1, true, null, PullRequestMergeMethod.Merge));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PullRequestMergeResult(1, false, null, (PullRequestMergeMethod)99));
    }

    private static GitHubPullRequestMerger CreateService(HttpMessageHandler handler) =>
        new(new HttpClient(handler), new Uri("https://api.test.example/root/"));

    private static GitHubRepositoryIdentity Repository() => new("owner", "repo");

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed record RecordedRequest(
        HttpMethod Method,
        Uri Uri,
        string? Body,
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

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri!,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken),
                string.Join(',', request.Headers.Accept),
                request.Headers.TryGetValues("X-GitHub-Api-Version", out var versions)
                    ? Assert.Single(versions)
                    : null,
                string.Join(',', request.Headers.UserAgent)));
            return await responseFactory(request, cancellationToken);
        }
    }
}
