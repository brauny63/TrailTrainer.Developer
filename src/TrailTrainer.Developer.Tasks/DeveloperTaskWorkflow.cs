using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class DeveloperTaskWorkflow : IDeveloperTaskWorkflow
{
    private readonly IDeveloperTaskParser taskParser;
    private readonly IDeveloperTaskStarter? starter;
    private readonly ICodexTaskExecutor? codexExecutor;
    private readonly ICodexExecutionStateStore? codexStateStore;
    private readonly IDeveloperTaskGatedCompleter gatedCompleter;
    private readonly IPullRequestService pullRequestService;

    public DeveloperTaskWorkflow(
        IDeveloperTaskParser taskParser,
        IDeveloperTaskGatedCompleter gatedCompleter,
        IPullRequestService pullRequestService,
        IDeveloperTaskStarter? starter = null,
        ICodexTaskExecutor? codexExecutor = null,
        ICodexExecutionStateStore? codexStateStore = null)
    {
        this.taskParser = taskParser ?? throw new ArgumentNullException(nameof(taskParser));
        if ((starter is null || codexExecutor is null || codexStateStore is null) &&
            (starter is not null || codexExecutor is not null || codexStateStore is not null))
        {
            throw new ArgumentException("Starter, Codex executor, and Codex state store must be supplied together.");
        }
        this.starter = starter;
        this.codexExecutor = codexExecutor;
        this.codexStateStore = codexStateStore;
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
        var taskId = task.Id.ToString();
        CodexExecutionState? executionState = null;
        if (codexStateStore is not null)
        {
        var configuredStarter = starter!;
        var configuredExecutor = codexExecutor!;
        executionState = await codexStateStore.LoadAsync(taskId, cancellationToken);
        if (executionState is null)
        {
            await configuredStarter.StartAsync(developerTaskFilePath, repositoryDirectoryPath, expectedRepositoryName, cancellationToken);
            executionState = new CodexExecutionState(taskId, repositoryDirectoryPath, developerTaskFilePath, CodexExecutionPhase.BranchCreated);
            await codexStateStore.SaveAsync(executionState, cancellationToken);
        }
        else if (!string.Equals(Path.GetFullPath(executionState.RepositoryPath), Path.GetFullPath(repositoryDirectoryPath), StringComparison.OrdinalIgnoreCase) ||
                 !string.Equals(Path.GetFullPath(executionState.TaskFilePath), Path.GetFullPath(developerTaskFilePath), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Persisted Codex execution state for '{taskId}' does not match this task and repository.");
        }

        if (executionState.Phase == CodexExecutionPhase.BranchCreated)
        {
            var codex = await configuredExecutor.ExecuteAsync(
                new CodexTaskExecutionRequest(repositoryDirectoryPath, developerTaskFilePath), cancellationToken);
            if (!codex.Succeeded)
            {
                var reason = codex.TimedOut ? "timed out" : $"failed with exit code {codex.ExitCode}";
                throw new InvalidOperationException($"Codex task execution {reason}: {codex.StandardError.Trim()}");
            }

            executionState = executionState with { Phase = CodexExecutionPhase.CodexSucceeded };
            await codexStateStore.SaveAsync(executionState, cancellationToken);
        }
        }

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

        if (codexStateStore is not null) await codexStateStore.DeleteAsync(taskId, cancellationToken);

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
