namespace TrailTrainer.Developer.Core;

public interface IGitStager
{
    Task<GitStageResult> StageAllAsync(
        string directoryPath,
        CancellationToken cancellationToken = default);
}
