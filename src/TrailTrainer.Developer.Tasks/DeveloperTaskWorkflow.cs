using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class DeveloperTaskWorkflow : IDeveloperTaskWorkflow
{
    private readonly IDeveloperTaskParser taskParser;
    private readonly IDeveloperTaskGatedCompleter gatedCompleter;
    private readonly IPullRequestService pullRequestService;

    public DeveloperTaskWorkflow(
        IDeveloperTaskParser taskParser,
        IDeveloperTaskGatedCompleter gatedCompleter,
        IPullRequestService pullRequestService)
    {
        this.taskParser = taskParser ?? throw new ArgumentNullException(nameof(taskParser));
        this.gatedCompleter = gatedCompleter ?? throw new ArgumentNullException(nameof(gatedCompleter));
        this.pullRequestService = pullRequestService
            ?? throw new ArgumentNullException(nameof(pullRequestService));
    }

    public async Task<DeveloperTaskWorkflowResult> ExecuteAsync(
        string developerTaskFilePath,
        string repositoryDirectoryPath,
        string expectedRepositoryName,
        string commitMessage,
        string gitRemoteName,
        bool setUpstream,
        GitHubRepositoryIdentity gitHubRepository,
        string pullRequestBaseBranch,
        string? pullRequestBody = null,
        bool pullRequestDraft = false,
        CancellationToken cancellationToken = default)
    {
        var task = await taskParser.ParseAsync(developerTaskFilePath, cancellationToken);
        var completion = await gatedCompleter.CompleteAsync(
            developerTaskFilePath,
            repositoryDirectoryPath,
            expectedRepositoryName,
            commitMessage,
            gitRemoteName,
            setUpstream,
            cancellationToken);

        var pullRequest = await pullRequestService.EnsureOpenAsync(
            gitHubRepository,
            completion.Completion.BranchName,
            pullRequestBaseBranch,
            CreatePullRequestTitle(task),
            pullRequestBody,
            pullRequestDraft,
            cancellationToken);

        return new DeveloperTaskWorkflowResult(task.Id, completion, pullRequest);
    }

    private static string CreatePullRequestTitle(DeveloperTaskDocument task)
    {
        var taskId = task.Id.ToString();
        if (task.Title.StartsWith($"{taskId} – ", StringComparison.Ordinal) ||
            task.Title.StartsWith($"{taskId} - ", StringComparison.Ordinal))
        {
            return task.Title;
        }

        return $"{taskId} – {task.Title}";
    }
}
