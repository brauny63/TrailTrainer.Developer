namespace TrailTrainer.Developer.Core;

public sealed record DeveloperReviewValidationResult
{
    public DeveloperReviewValidationResult(
        DeveloperTaskId taskId,
        DeveloperReviewStatus reviewStatus,
        IEnumerable<string> errors,
        IEnumerable<string> warnings)
    {
        TaskId = taskId;
        ReviewStatus = reviewStatus;
        Errors = errors.Distinct(StringComparer.Ordinal).ToArray();
        Warnings = warnings.Distinct(StringComparer.Ordinal).ToArray();
    }

    public bool IsValid => Errors.Count == 0;
    public DeveloperTaskId TaskId { get; }
    public DeveloperReviewStatus ReviewStatus { get; }
    public IReadOnlyList<string> Errors { get; }
    public IReadOnlyList<string> Warnings { get; }
}
