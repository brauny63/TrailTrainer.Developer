namespace TrailTrainer.Developer.Core;

public sealed record CodexTaskExecutionRequest(string RepositoryPath, string DeveloperTaskFilePath)
{
    public string Instruction =>
        $"Work the Developer Task at {DeveloperTaskFilePath} completely. " +
        "Follow its scope, requirements, architecture constraints, verification steps, and Codex Completion Protocol. " +
        "Do not modify the Developer Task. Create the required review report. Do not commit and do not push.";
}
