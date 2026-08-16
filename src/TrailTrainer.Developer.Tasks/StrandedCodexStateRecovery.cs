using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class StrandedCodexStateRecovery : IStrandedCodexStateRecovery
{
    private readonly IDeveloperTaskDiscovery taskDiscovery;
    private readonly IDeveloperTaskParser taskParser;
    private readonly IGitRepositoryStatusProvider statusProvider;
    private readonly ICodexExecutionStateStore codexStore;
    private readonly IDeveloperLifecycleStateDiscovery lifecycleDiscovery;
    private readonly IDeveloperLifecycleStateStore lifecycleStore;
    private readonly IDeveloperReviewParser reviewParser;
    private readonly IDeveloperReviewValidator reviewValidator;
    private readonly IUtcClock clock;

    public StrandedCodexStateRecovery(
        IDeveloperTaskDiscovery taskDiscovery,
        IDeveloperTaskParser taskParser,
        IGitRepositoryStatusProvider statusProvider,
        ICodexExecutionStateStore codexStore,
        IDeveloperLifecycleStateDiscovery lifecycleDiscovery,
        IDeveloperLifecycleStateStore lifecycleStore,
        IDeveloperReviewParser reviewParser,
        IDeveloperReviewValidator reviewValidator,
        IUtcClock clock)
    {
        this.taskDiscovery = taskDiscovery;
        this.taskParser = taskParser;
        this.statusProvider = statusProvider;
        this.codexStore = codexStore;
        this.lifecycleDiscovery = lifecycleDiscovery;
        this.lifecycleStore = lifecycleStore;
        this.reviewParser = reviewParser;
        this.reviewValidator = reviewValidator;
        this.clock = clock;
    }

    public async Task<StrandedCodexStateRecoveryResult> TryRecoverAsync(
        InitialDeveloperTaskIntakeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Enabled)
        {
            return new(false);
        }

        var lifecycleStates = await lifecycleDiscovery.ListAsync(cancellationToken)
            ?? throw new InvalidOperationException("Lifecycle state discovery returned a null collection.");
        if (lifecycleStates.Count != 0)
        {
            return new(false);
        }

        var status = await statusProvider.GetStatusAsync(request.RepositoryPath, cancellationToken);
        if (!status.IsRepository || status.RepositoryRoot is null || status.CurrentBranch is null ||
            !status.HasUncommittedChanges ||
            !SamePath(status.RepositoryRoot, request.RepositoryPath))
        {
            return new(false);
        }

        var descriptors = await taskDiscovery.DiscoverAsync(request.RepositoryPath, cancellationToken);
        var eligible = new List<(DeveloperTaskDescriptor Descriptor, DeveloperTaskDocument Task, CodexExecutionState State)>();
        foreach (var descriptor in descriptors)
        {
            var state = await codexStore.LoadAsync(descriptor.Id.ToString(), cancellationToken);
            if (state is null || state.Phase != CodexExecutionPhase.BranchCreated ||
                !string.Equals(state.TaskId, descriptor.Id.ToString(), StringComparison.Ordinal) ||
                !SamePath(state.RepositoryPath, status.RepositoryRoot) ||
                !SamePath(state.TaskFilePath, descriptor.FilePath))
            {
                continue;
            }

            var task = await taskParser.ParseAsync(descriptor.FilePath, cancellationToken);
            if (task.Id != descriptor.Id ||
                !string.Equals(task.Repository, request.RepositoryName, StringComparison.Ordinal) ||
                !string.Equals(status.CurrentBranch, task.ExpectedBranch, StringComparison.Ordinal) ||
                !await HasRepairEligibleInvalidReviewAsync(task, status.RepositoryRoot, cancellationToken))
            {
                continue;
            }

            eligible.Add((descriptor, task, state));
        }

        if (eligible.Count != 1)
        {
            return new(false);
        }

        var candidate = eligible[0];
        var taskId = candidate.Descriptor.Id.ToString();
        var startRequest = new PersistedDeveloperLifecycleStartRequest(
            taskId,
            candidate.Descriptor.FilePath,
            candidate.Descriptor.FilePath,
            status.RepositoryRoot,
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
            request.DeleteRemoteBranch);
        await lifecycleStore.SaveAsync(
            DeveloperLifecyclePersistedState.CreateRecovery(taskId, candidate.Descriptor.FilePath, startRequest, clock.UtcNow),
            cancellationToken);
        await codexStore.SaveAsync(candidate.State with { Phase = CodexExecutionPhase.ReviewRepairRequired }, cancellationToken);
        return new(true, taskId);
    }

    private async Task<bool> HasRepairEligibleInvalidReviewAsync(
        DeveloperTaskDocument task,
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        if (Path.IsPathRooted(task.ReviewReportPath)) return false;
        var reviewPath = Path.GetFullPath(Path.Combine(repositoryRoot, task.ReviewReportPath));
        if (!IsWithin(repositoryRoot, reviewPath) || !File.Exists(reviewPath)) return false;
        try
        {
            var review = await reviewParser.ParseAsync(reviewPath, cancellationToken);
            var validation = await reviewValidator.ValidateAsync(task, review, cancellationToken);
            return !validation.IsValid;
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or FormatException)
        {
            return true;
        }
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)), StringComparison.OrdinalIgnoreCase);

    private static bool IsWithin(string root, string path)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), path);
        return relative != ".." && !Path.IsPathRooted(relative) &&
            !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }
}
