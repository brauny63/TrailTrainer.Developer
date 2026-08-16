using System.Net;

namespace TrailTrainer.Developer.GitHub;

public enum GitHubApiFailureKind
{
    AuthenticationMissing,
    AuthenticationRejected,
    RepositoryNotFoundOrPrivateAccessDenied,
    InsufficientRepositoryAccess,
    RateLimited,
    HttpFailure
}

public sealed class GitHubApiException : HttpRequestException
{
    public GitHubApiException(GitHubApiFailureKind failureKind, string message, HttpStatusCode? statusCode = null)
        : base(message, inner: null, statusCode)
    {
        FailureKind = failureKind;
    }

    public GitHubApiFailureKind FailureKind { get; }
}
