using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class DeveloperLifecycleOrchestrator : IDeveloperLifecycleOrchestrator
{
    private readonly IDeveloperTaskWorkflow workflow;
    private readonly IPullRequestStatusGate statusGate;
    private readonly IPullRequestMergeGate mergeGate;
    private readonly IPostMergeCleaner postMergeCleaner;

    public DeveloperLifecycleOrchestrator(
        IDeveloperTaskWorkflow workflow,
        IPullRequestStatusGate statusGate,
        IPullRequestMergeGate mergeGate,
        IPostMergeCleaner postMergeCleaner)
    {
        this.workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        this.statusGate = statusGate ?? throw new ArgumentNullException(nameof(statusGate));
        this.mergeGate = mergeGate ?? throw new ArgumentNullException(nameof(mergeGate));
        this.postMergeCleaner = postMergeCleaner ?? throw new ArgumentNullException(nameof(postMergeCleaner));
    }

    public async Task<DeveloperLifecycleResult> ExecuteAsync(
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
        bool deleteRemoteBranch,
        CancellationToken cancellationToken = default)
    {
        var workflowResult = await workflow.ExecuteAsync(
            developerTaskFilePath,
            repositoryDirectoryPath,
            expectedRepositoryName,
            commitMessage,
            gitRemoteName,
            setUpstream,
            gitHubRepository,
            pullRequestBaseBranch,
            pullRequestBody,
            pullRequestDraft,
            cancellationToken);

        var pullRequestNumber = workflowResult.PullRequest.PullRequest.Number;
        var statusResult = await statusGate.EvaluateAsync(
            gitHubRepository,
            pullRequestNumber,
            cancellationToken);

        if (statusResult.State == PullRequestGateState.Pending)
        {
            return new DeveloperLifecycleResult(
                DeveloperLifecycleState.Pending,
                workflowResult,
                statusResult);
        }

        if (statusResult.State == PullRequestGateState.Failed)
        {
            return new DeveloperLifecycleResult(
                DeveloperLifecycleState.Failed,
                workflowResult,
                statusResult);
        }

        if (statusResult.State != PullRequestGateState.Successful)
        {
            throw new InvalidOperationException("The explicit Pull Request status gate returned an unsupported state.");
        }

        var gatedMergeResult = await mergeGate.MergeAsync(
            gitHubRepository,
            pullRequestNumber,
            mergeMethod,
            mergeCommitTitle,
            mergeCommitMessage,
            cancellationToken);
        if (gatedMergeResult?.Merge is null || !gatedMergeResult.Merge.Merged)
        {
            throw new InvalidOperationException(
                "The guarded merge did not return a confirmed successful merge result.");
        }

        var cleanupResult = await postMergeCleaner.CleanupAsync(
            repositoryDirectoryPath,
            gitHubRepository,
            pullRequestNumber,
            gatedMergeResult.Merge,
            workflowResult.Completion.Completion.BranchName,
            pullRequestBaseBranch,
            gitRemoteName,
            deleteRemoteBranch,
            cancellationToken);

        return new DeveloperLifecycleResult(
            DeveloperLifecycleState.Completed,
            workflowResult,
            statusResult,
            gatedMergeResult,
            cleanupResult);
    }
}
