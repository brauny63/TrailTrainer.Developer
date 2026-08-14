namespace TrailTrainer.Developer.Core;

public interface IDeveloperReviewParser
{
    Task<DeveloperReviewDocument> ParseAsync(
        string reviewFilePath,
        CancellationToken cancellationToken = default);
}
