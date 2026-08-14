namespace TrailTrainer.Developer.Core;

public sealed record DeveloperTaskCompletionResult(
    DeveloperTaskId TaskId,
    string TaskTitle,
    string RepositoryRoot,
    string BranchName,
    string CommitSha,
    string CommitMessage,
    string RemoteName,
    bool SetUpstream,
    string TaskFilePath,
    string ReviewReportPath);
