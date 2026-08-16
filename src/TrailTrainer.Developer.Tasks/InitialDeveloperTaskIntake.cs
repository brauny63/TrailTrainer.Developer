using Microsoft.Extensions.Logging;
using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class InitialDeveloperTaskIntake : IInitialDeveloperTaskIntake
{
    private readonly IAutomaticResumeCandidateSelector candidateSelector;
    private readonly IDeveloperTaskDiscovery taskDiscovery;
    private readonly IGitRepositoryStatusProvider repositoryStatusProvider;
    private readonly ICodexExecutionStateStore codexStateStore;
    private readonly IPersistedDeveloperLifecycle persistedLifecycle;
    private readonly ILogger<InitialDeveloperTaskIntake> logger;

    public InitialDeveloperTaskIntake(
        IAutomaticResumeCandidateSelector candidateSelector,
        IDeveloperTaskDiscovery taskDiscovery,
        IGitRepositoryStatusProvider repositoryStatusProvider,
        ICodexExecutionStateStore codexStateStore,
        IPersistedDeveloperLifecycle persistedLifecycle,
        ILogger<InitialDeveloperTaskIntake> logger)
    {
        this.candidateSelector = candidateSelector ?? throw new ArgumentNullException(nameof(candidateSelector));
        this.taskDiscovery = taskDiscovery ?? throw new ArgumentNullException(nameof(taskDiscovery));
        this.repositoryStatusProvider = repositoryStatusProvider
            ?? throw new ArgumentNullException(nameof(repositoryStatusProvider));
        this.codexStateStore = codexStateStore ?? throw new ArgumentNullException(nameof(codexStateStore));
        this.persistedLifecycle = persistedLifecycle ?? throw new ArgumentNullException(nameof(persistedLifecycle));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InitialDeveloperTaskIntakeResult> ExecuteAsync(
        InitialDeveloperTaskIntakeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Enabled)
        {
            logger.LogInformation("Initial Developer Task intake is disabled.");
            return new InitialDeveloperTaskIntakeResult(InitialDeveloperTaskIntakeState.Disabled);
        }

        ValidateRequest(request);
        var candidate = await candidateSelector.SelectAsync(cancellationToken);
        if (candidate.State == AutomaticResumeCandidateState.Found)
        {
            logger.LogInformation(
                "Initial Developer Task intake skipped because resumable task {TaskId} has priority.",
                candidate.PersistedState!.TaskId);
            return new InitialDeveloperTaskIntakeResult(
                InitialDeveloperTaskIntakeState.ResumableWorkFound);
        }

        var repositoryStatus = await repositoryStatusProvider.GetStatusAsync(
            request.RepositoryPath,
            cancellationToken);
        if (!repositoryStatus.IsRepository || repositoryStatus.RepositoryRoot is null)
        {
            throw new InvalidOperationException(
                $"Initial intake path '{request.RepositoryPath}' is not inside a Git working tree.");
        }

        if (repositoryStatus.CurrentBranch is null)
        {
            throw new InvalidOperationException(
                $"Initial intake repository '{repositoryStatus.RepositoryRoot}' has a detached HEAD.");
        }

        var tasks = await taskDiscovery.DiscoverAsync(request.RepositoryPath, cancellationToken);
        DeveloperTaskDescriptor? recoverableTask = null;
        foreach (var task in tasks)
        {
            var state = await codexStateStore.LoadAsync(task.Id.ToString(), cancellationToken);
            if (state?.Phase == CodexExecutionPhase.ReviewRepairRequired &&
                string.Equals(Path.GetFullPath(state.RepositoryPath), repositoryStatus.RepositoryRoot, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Path.GetFullPath(state.TaskFilePath), Path.GetFullPath(task.FilePath), StringComparison.OrdinalIgnoreCase))
            {
                recoverableTask = task;
                break;
            }
        }

        if (repositoryStatus.HasUncommittedChanges && recoverableTask is null)
        {
            throw new InvalidOperationException(
                $"Initial intake repository '{repositoryStatus.RepositoryRoot}' has uncommitted changes.");
        }

        if (recoverableTask is not null)
        {
            logger.LogInformation(
                "Resuming review repair for {TaskId} before initial Developer Task intake.",
                recoverableTask.Id);
            return await StartAsync(recoverableTask, request, cancellationToken);
        }

        var selectedTask = tasks.FirstOrDefault();
        if (selectedTask is null)
        {
            logger.LogInformation(
                "Initial Developer Task intake found no task in repository {RepositoryPath}.",
                request.RepositoryPath);
            return new InitialDeveloperTaskIntakeResult(InitialDeveloperTaskIntakeState.NoTaskFound);
        }

        return await StartAsync(selectedTask, request, cancellationToken);
    }

    private async Task<InitialDeveloperTaskIntakeResult> StartAsync(
        DeveloperTaskDescriptor selectedTask,
        InitialDeveloperTaskIntakeRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Initial Developer Task intake selected {TaskId} from repository {RepositoryPath}.",
            selectedTask.Id,
            request.RepositoryPath);
        var taskId = selectedTask.Id.ToString();
        var startResult = await persistedLifecycle.StartAsync(
            new PersistedDeveloperLifecycleStartRequest(
                taskId,
                selectedTask.FilePath,
                selectedTask.FilePath,
                request.RepositoryPath,
                request.RepositoryName,
                $"Implement {taskId}",
                request.RemoteName,
                true,
                new GitHubRepositoryIdentity(request.GitHubOwner, request.RepositoryName),
                request.BaseBranch,
                null,
                false,
                request.MergeMethod,
                request.MergeCommitTitle,
                request.MergeCommitMessage,
                request.DeleteRemoteBranch),
            cancellationToken);

        logger.LogInformation(
            "Initial Developer Task intake started {TaskId}; persisted resume state present: {HasPersistedState}.",
            taskId,
            startResult.PersistedState is not null);
        return new InitialDeveloperTaskIntakeResult(
            InitialDeveloperTaskIntakeState.Started,
            selectedTask,
            startResult);
    }

    private static void ValidateRequest(InitialDeveloperTaskIntakeRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RepositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RepositoryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.GitHubOwner);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.BaseBranch);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RemoteName);
        if (!Directory.Exists(request.RepositoryPath))
        {
            throw new DirectoryNotFoundException(
                $"Initial intake repository '{Path.GetFullPath(request.RepositoryPath)}' does not exist.");
        }
    }
}
