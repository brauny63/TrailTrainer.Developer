namespace TrailTrainer.Developer.Core;

public sealed record CodexTaskExecutionRequest(
    string RepositoryPath,
    string DeveloperTaskFilePath,
    bool RepairReviewOnly = false)
{
    public string Instruction => DeveloperReviewContract.CreateCodexInstruction(DeveloperTaskFilePath, RepairReviewOnly);
}
