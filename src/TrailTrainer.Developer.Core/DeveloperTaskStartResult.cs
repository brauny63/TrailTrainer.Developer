namespace TrailTrainer.Developer.Core;

public sealed record DeveloperTaskStartResult(
    DeveloperTaskId TaskId,
    string TaskTitle,
    string RepositoryRoot,
    string PreviousBranch,
    string CreatedBranch,
    string TaskFilePath,
    string ReviewReportPath);
