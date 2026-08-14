using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class DeveloperReviewValidator : IDeveloperReviewValidator
{
    public Task<DeveloperReviewValidationResult> ValidateAsync(
        DeveloperTaskDocument task,
        DeveloperReviewDocument review,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(review);
        cancellationToken.ThrowIfCancellationRequested();

        var errors = new List<string>();
        var warnings = new List<string>();

        AddErrorIf(task.Id != review.TaskId, errors, "Review Task ID does not match the Developer Task ID.");
        AddErrorIf(
            !string.Equals(
                Path.GetFileName(task.ReviewReportPath),
                Path.GetFileName(review.FilePath),
                StringComparison.Ordinal),
            errors,
            "Review filename does not match the task Review report metadata.");
        AddErrorIf(review.Status == DeveloperReviewStatus.Blocked, errors, "Review status is BLOCKED.");
        AddErrorIf(!review.Verification.BuildSuccessful, errors, "Build verification was unsuccessful.");
        AddErrorIf(review.Verification.BuildErrorCount != 0, errors, "Build verification contains errors.");
        AddErrorIf(!review.Verification.TestSuccessful, errors, "Test verification was unsuccessful.");
        AddErrorIf(review.Verification.TestsFailed != 0, errors, "Test verification contains failed tests.");
        AddErrorIf(!review.Verification.DiffCheckSuccessful, errors, "Git diff check was unsuccessful.");
        AddErrorIf(review.CommitCreated, errors, "A commit was created during task implementation.");
        AddErrorIf(review.PushPerformed, errors, "A push was performed during task implementation.");

        AddWarningIf(review.Verification.BuildWarningCount > 0, warnings, "Build verification contains warnings.");
        AddWarningIf(review.Verification.TestsSkipped > 0, warnings, "Test verification contains skipped tests.");
        AddWarningIf(!IsNone(review.Deviations), warnings, "The review reports deviations from the Developer Task.");
        AddWarningIf(!IsNone(review.OpenIssues), warnings, "The review reports open issues or known limitations.");

        return Task.FromResult(new DeveloperReviewValidationResult(
            review.TaskId,
            review.Status,
            errors,
            warnings));
    }

    private static bool IsNone(string value) =>
        string.Equals(value.Trim().TrimEnd('.'), "None", StringComparison.OrdinalIgnoreCase);

    private static void AddErrorIf(bool condition, ICollection<string> errors, string error)
    {
        if (condition)
        {
            errors.Add(error);
        }
    }

    private static void AddWarningIf(bool condition, ICollection<string> warnings, string warning)
    {
        if (condition)
        {
            warnings.Add(warning);
        }
    }
}
