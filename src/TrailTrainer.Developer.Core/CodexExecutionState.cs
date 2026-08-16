namespace TrailTrainer.Developer.Core;

public sealed record CodexExecutionState(
    string TaskId,
    string RepositoryPath,
    string TaskFilePath,
    CodexExecutionPhase Phase,
    DeveloperTaskGatedCompletionResult? Completion = null);
