namespace TrailTrainer.Developer.Core;

public interface IDeveloperReviewValidator
{
    Task<DeveloperReviewValidationResult> ValidateAsync(
        DeveloperTaskDocument task,
        DeveloperReviewDocument review,
        CancellationToken cancellationToken = default);
}
