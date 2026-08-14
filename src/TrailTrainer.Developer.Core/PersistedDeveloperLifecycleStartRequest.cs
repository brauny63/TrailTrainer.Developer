namespace TrailTrainer.Developer.Core;

public sealed record PersistedDeveloperLifecycleStartRequest
{
    public PersistedDeveloperLifecycleStartRequest(
        string taskId,
        string? taskFilePath,
        string developerTaskFilePath,
        string repositoryDirectoryPath,
        string expectedRepositoryName,
        string commitMessage,
        string gitRemoteName,
        bool setUpstream,
        GitHubRepositoryIdentity gitHubRepository,
        string pullRequestBaseBranch,
        string? pullRequestBody,
        bool pullRequestDraft,
        PullRequestMergeMethod mergeMethod,
        string? mergeCommitTitle,
        string? mergeCommitMessage,
        bool deleteRemoteBranch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        if (taskFilePath is not null && string.IsNullOrWhiteSpace(taskFilePath))
        {
            throw new ArgumentException("Task file path must not be whitespace.", nameof(taskFilePath));
        }

        ArgumentNullException.ThrowIfNull(gitHubRepository);
        TaskId = taskId;
        TaskFilePath = taskFilePath;
        DeveloperTaskFilePath = developerTaskFilePath;
        RepositoryDirectoryPath = repositoryDirectoryPath;
        ExpectedRepositoryName = expectedRepositoryName;
        CommitMessage = commitMessage;
        GitRemoteName = gitRemoteName;
        SetUpstream = setUpstream;
        GitHubRepository = gitHubRepository;
        PullRequestBaseBranch = pullRequestBaseBranch;
        PullRequestBody = pullRequestBody;
        PullRequestDraft = pullRequestDraft;
        MergeMethod = mergeMethod;
        MergeCommitTitle = mergeCommitTitle;
        MergeCommitMessage = mergeCommitMessage;
        DeleteRemoteBranch = deleteRemoteBranch;
    }

    public string TaskId { get; }
    public string? TaskFilePath { get; }
    public string DeveloperTaskFilePath { get; }
    public string RepositoryDirectoryPath { get; }
    public string ExpectedRepositoryName { get; }
    public string CommitMessage { get; }
    public string GitRemoteName { get; }
    public bool SetUpstream { get; }
    public GitHubRepositoryIdentity GitHubRepository { get; }
    public string PullRequestBaseBranch { get; }
    public string? PullRequestBody { get; }
    public bool PullRequestDraft { get; }
    public PullRequestMergeMethod MergeMethod { get; }
    public string? MergeCommitTitle { get; }
    public string? MergeCommitMessage { get; }
    public bool DeleteRemoteBranch { get; }
}
