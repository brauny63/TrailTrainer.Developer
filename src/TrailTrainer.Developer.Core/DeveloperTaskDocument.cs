namespace TrailTrainer.Developer.Core;

public sealed record DeveloperTaskDocument(
    DeveloperTaskId Id,
    string Title,
    string FilePath,
    string Repository,
    string ExpectedBranch,
    string ReviewReportPath);
