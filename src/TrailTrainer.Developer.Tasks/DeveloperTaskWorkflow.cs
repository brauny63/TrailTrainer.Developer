using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class DeveloperTaskWorkflow : IDeveloperTaskWorkflow
{
    private readonly IDeveloperTaskParser taskParser;
    private readonly IDeveloperTaskStarter? starter;
    private readonly ICodexTaskExecutor? codexExecutor;
    private readonly ICodexExecutionStateStore? codexStateStore;
    private readonly IGitRepositoryStatusProvider? repositoryStatusProvider;
    private readonly IDeveloperReviewParser? reviewParser;
    private readonly IDeveloperReviewValidator? reviewValidator;
    private readonly IDeveloperTaskGatedCompleter gatedCompleter;
    private readonly IPullRequestService pullRequestService;

    public DeveloperTaskWorkflow(
        IDeveloperTaskParser taskParser,
        IDeveloperTaskGatedCompleter gatedCompleter,
        IPullRequestService pullRequestService,
        IDeveloperTaskStarter? starter = null,
        ICodexTaskExecutor? codexExecutor = null,
        ICodexExecutionStateStore? codexStateStore = null,
        IGitRepositoryStatusProvider? repositoryStatusProvider = null,
        IDeveloperReviewParser? reviewParser = null,
        IDeveloperReviewValidator? reviewValidator = null)
    {
        this.taskParser = taskParser ?? throw new ArgumentNullException(nameof(taskParser));
        if ((starter is null || codexExecutor is null || codexStateStore is null || repositoryStatusProvider is null || reviewParser is null || reviewValidator is null) &&
            (starter is not null || codexExecutor is not null || codexStateStore is not null || repositoryStatusProvider is not null || reviewParser is not null || reviewValidator is not null))
        {
            throw new ArgumentException("Starter, Codex executor, Codex state store, repository status provider, review parser, and review validator must be supplied together.");
        }
        this.starter = starter;
        this.codexExecutor = codexExecutor;
        this.codexStateStore = codexStateStore;
        this.repositoryStatusProvider = repositoryStatusProvider;
        this.reviewParser = reviewParser;
        this.reviewValidator = reviewValidator;
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
        var configuredStatusProvider = repositoryStatusProvider!;
        executionState = await codexStateStore.LoadAsync(taskId, cancellationToken);
        var resumedBranchCreated = executionState?.Phase == CodexExecutionPhase.BranchCreated;
        if (executionState is null)
        {
            ValidateTaskRepositoryIdentity(task, expectedRepositoryName, gitHubRepository);
            var repositoryStatus = await configuredStatusProvider.GetStatusAsync(repositoryDirectoryPath, cancellationToken);
            if (IsInterruptedStartRecovery(repositoryStatus, task.ExpectedBranch))
            {
                // Branch creation completed before the initial state became durable.
            }
            else
            {
                await configuredStarter.StartAsync(developerTaskFilePath, repositoryDirectoryPath, expectedRepositoryName, cancellationToken);
            }
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
            if (resumedBranchCreated)
            {
                var retryStatus = await configuredStatusProvider.GetStatusAsync(repositoryDirectoryPath, cancellationToken);
                ValidateSafeCodexBranch(retryStatus, task, repositoryDirectoryPath, requireClean: true, "pre-execution retry");
            }

            var codex = await configuredExecutor.ExecuteAsync(
                new CodexTaskExecutionRequest(repositoryDirectoryPath, developerTaskFilePath), cancellationToken);
            if (!codex.Succeeded)
            {
                var reason = codex.TimedOut ? "timed out" : $"failed with exit code {codex.ExitCode}";
                throw new DeveloperTaskExecutionException(
                    $"Task {taskId} Codex process {reason} in repository '{repositoryDirectoryPath}' on expected branch '{task.ExpectedBranch}' during process execution: {codex.StandardError.Trim()}");
            }

            await ValidateCodexSuccessAsync(task, repositoryDirectoryPath, codex.ExitCode, cancellationToken);
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

    private async Task ValidateCodexSuccessAsync(
        DeveloperTaskDocument task,
        string repositoryDirectoryPath,
        int exitCode,
        CancellationToken cancellationToken)
    {
        var status = await repositoryStatusProvider!.GetStatusAsync(repositoryDirectoryPath, cancellationToken);
        ValidateSafeCodexBranch(status, task, repositoryDirectoryPath, requireClean: false, "post-execution validation");

        var repositoryRoot = Path.GetFullPath(status.RepositoryRoot!);
        if (Path.IsPathRooted(task.ReviewReportPath))
        {
            throw ValidationFailure(task, repositoryDirectoryPath, task.ReviewReportPath, exitCode,
                "review path is not repository-relative");
        }

        var reviewPath = Path.GetFullPath(Path.Combine(repositoryRoot, task.ReviewReportPath));
        var relativeReviewPath = Path.GetRelativePath(repositoryRoot, reviewPath);
        if (Path.IsPathRooted(relativeReviewPath) || relativeReviewPath == ".." ||
            relativeReviewPath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relativeReviewPath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw ValidationFailure(task, repositoryDirectoryPath, reviewPath, exitCode,
                "review path resolves outside the repository");
        }

        try
        {
            var review = await reviewParser!.ParseAsync(reviewPath, cancellationToken);
            var validation = await reviewValidator!.ValidateAsync(task, review, cancellationToken);
            if (!validation.IsValid)
            {
                throw ValidationFailure(task, repositoryDirectoryPath, reviewPath, exitCode,
                    "review is invalid: " + string.Join("; ", validation.Errors));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DeveloperTaskExecutionException)
        {
            throw;
        }
        catch (FileNotFoundException exception)
        {
            throw ValidationFailure(task, repositoryDirectoryPath, reviewPath, exitCode,
                "expected review is missing; execution remains retryable only while the task branch is clean",
                exception);
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or FormatException)
        {
            throw ValidationFailure(task, repositoryDirectoryPath, reviewPath, exitCode,
                $"review cannot be parsed or validated: {exception.Message}", exception);
        }
    }

    private static void ValidateSafeCodexBranch(
        GitRepositoryStatus status,
        DeveloperTaskDocument task,
        string repositoryDirectoryPath,
        bool requireClean,
        string phase)
    {
        var problem = !status.IsRepository || status.RepositoryRoot is null
            ? "path is not a Git repository"
            : status.CurrentBranch is null
                ? "repository has a detached HEAD"
                : !string.Equals(status.CurrentBranch, task.ExpectedBranch, StringComparison.Ordinal)
                    ? $"current branch '{status.CurrentBranch}' is not expected branch '{task.ExpectedBranch}'"
                    : requireClean && status.HasUncommittedChanges
                        ? "repository has uncommitted changes and will not be cleaned or overwritten"
                        : null;
        if (problem is not null)
        {
            throw new DeveloperTaskExecutionException(
                $"Task {task.Id} repository '{repositoryDirectoryPath}' is unsafe during {phase}: {problem}.");
        }
    }

    private static DeveloperTaskExecutionException ValidationFailure(
        DeveloperTaskDocument task,
        string repositoryPath,
        string reviewPath,
        int exitCode,
        string reason,
        Exception? innerException = null)
    {
        var message = $"Task {task.Id} Codex success validation failed in repository '{repositoryPath}' " +
            $"on expected branch '{task.ExpectedBranch}' for review '{reviewPath}' after exit code {exitCode}: {reason}.";
        return innerException is null
            ? new DeveloperTaskExecutionException(message)
            : new DeveloperTaskExecutionException(message, innerException);
    }

    private static bool IsInterruptedStartRecovery(GitRepositoryStatus status, string expectedBranch)
    {
        if (!status.IsRepository || status.RepositoryRoot is null)
        {
            throw new InvalidOperationException("The supplied directory is not inside a Git working tree.");
        }
        if (status.CurrentBranch is null)
        {
            throw new InvalidOperationException($"Repository '{status.RepositoryRoot}' has a detached HEAD.");
        }
        if (status.HasUncommittedChanges)
        {
            throw new InvalidOperationException($"Repository '{status.RepositoryRoot}' has uncommitted changes.");
        }
        return string.Equals(status.CurrentBranch, expectedBranch, StringComparison.Ordinal);
    }

    private static void ValidateTaskRepositoryIdentity(
        DeveloperTaskDocument task,
        string expectedRepositoryName,
        GitHubRepositoryIdentity githubRepository)
    {
        if (!string.Equals(task.Repository, expectedRepositoryName, StringComparison.Ordinal) ||
            !string.Equals(githubRepository.Repository, expectedRepositoryName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Developer Task and configured repository identities do not match.");
        }
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
