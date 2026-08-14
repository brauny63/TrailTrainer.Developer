namespace TrailTrainer.Developer.Core;

public interface IDeveloperTaskParser
{
    Task<DeveloperTaskDocument> ParseAsync(
        string developerTaskFilePath,
        CancellationToken cancellationToken = default);
}
