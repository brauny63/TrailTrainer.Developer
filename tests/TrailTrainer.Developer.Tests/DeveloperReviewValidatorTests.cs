using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class DeveloperReviewValidatorTests
{
    private readonly DeveloperReviewValidator validator = new();

    [Fact]
    public async Task ValidateAsync_ReviewableReport_IsValid()
    {
        var result = await validator.ValidateAsync(CreateTask(), CreateReview());
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
        Assert.Equal(new DeveloperTaskId(10), result.TaskId);
        Assert.Equal(DeveloperReviewStatus.ReadyForReview, result.ReviewStatus);
    }

    public static TheoryData<string> ErrorConditions => new()
    {
        "id", "filename", "blocked", "build-failed", "build-errors", "tests-failed",
        "failed-tests", "diff-failed", "commit", "push"
    };

    [Theory]
    [MemberData(nameof(ErrorConditions))]
    public async Task ValidateAsync_EachErrorCondition_Invalidates(string condition)
    {
        var task = CreateTask();
        var review = CreateReviewForCondition(condition);

        var result = await validator.ValidateAsync(task, review);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task ValidateAsync_MultipleErrors_AccumulatesDistinctErrors()
    {
        var verification = new DeveloperReviewVerification(false, 0, 2, false, 0, 3, 0, false);
        var review = CreateReview(
            status: DeveloperReviewStatus.Blocked,
            verification: verification,
            commitCreated: true,
            pushPerformed: true,
            taskId: new DeveloperTaskId(11),
            fileName: "REVIEW-0011.md");

        var result = await validator.ValidateAsync(CreateTask(), review);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 8);
        Assert.Equal(result.Errors.Count, result.Errors.Distinct(StringComparer.Ordinal).Count());
    }

    public static TheoryData<string> WarningConditions => new()
    {
        "build-warnings", "skipped-tests", "deviations", "issues"
    };

    [Theory]
    [MemberData(nameof(WarningConditions))]
    public async Task ValidateAsync_EachWarningCondition_WarnsWithoutInvalidating(string condition)
    {
        var result = await validator.ValidateAsync(CreateTask(), CreateReviewForCondition(condition));
        Assert.True(result.IsValid);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public async Task ValidateAsync_MultipleWarnings_AccumulateWithoutInvalidating()
    {
        var verification = ValidVerification() with { BuildWarningCount = 2, TestsSkipped = 3 };
        var review = CreateReview(
            verification: verification,
            deviations: "A deviation",
            openIssues: "An issue");

        var result = await validator.ValidateAsync(CreateTask(), review);

        Assert.True(result.IsValid);
        Assert.Equal(4, result.Warnings.Count);
    }

    [Theory]
    [InlineData(" None ")]
    [InlineData("None.")]
    [InlineData("none")]
    public async Task ValidateAsync_NoneMarkers_DoNotWarn(string noneMarker)
    {
        var result = await validator.ValidateAsync(
            CreateTask(),
            CreateReview(deviations: noneMarker, openIssues: noneMarker));
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task ValidateAsync_PreCanceledToken_ThrowsCancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            validator.ValidateAsync(CreateTask(), CreateReview(), source.Token));
    }

    private static DeveloperTaskDocument CreateTask() => new(
        new DeveloperTaskId(10),
        "Task",
        "C:\\tasks\\DEV-0010-Task.md",
        "TrailTrainer.Developer",
        "feature/task",
        "docs/developer-reviews/REVIEW-0010.md");

    private static DeveloperReviewDocument CreateReviewForCondition(string condition) => condition switch
    {
        "id" => CreateReview(taskId: new DeveloperTaskId(11)),
        "filename" => CreateReview(fileName: "REVIEW-0011.md"),
        "blocked" => CreateReview(status: DeveloperReviewStatus.Blocked),
        "build-failed" => CreateReview(verification: ValidVerification() with { BuildSuccessful = false }),
        "build-errors" => CreateReview(verification: ValidVerification() with { BuildErrorCount = 1 }),
        "tests-failed" => CreateReview(verification: ValidVerification() with { TestSuccessful = false }),
        "failed-tests" => CreateReview(verification: ValidVerification() with { TestsFailed = 1 }),
        "diff-failed" => CreateReview(verification: ValidVerification() with { DiffCheckSuccessful = false }),
        "commit" => CreateReview(commitCreated: true),
        "push" => CreateReview(pushPerformed: true),
        "build-warnings" => CreateReview(verification: ValidVerification() with { BuildWarningCount = 1 }),
        "skipped-tests" => CreateReview(verification: ValidVerification() with { TestsSkipped = 1 }),
        "deviations" => CreateReview(deviations: "Deviation"),
        "issues" => CreateReview(openIssues: "Issue"),
        _ => throw new ArgumentOutOfRangeException(nameof(condition), condition, "Unknown test condition.")
    };

    private static DeveloperReviewDocument CreateReview(
        DeveloperReviewStatus status = DeveloperReviewStatus.ReadyForReview,
        DeveloperReviewVerification? verification = null,
        bool commitCreated = false,
        bool pushPerformed = false,
        DeveloperTaskId? taskId = null,
        string fileName = "REVIEW-0010.md",
        string deviations = "None",
        string openIssues = "None") => new(
            taskId ?? new DeveloperTaskId(10),
            "Review",
            Path.GetFullPath(Path.Combine("reviews", fileName)),
            status,
            "Summary",
            [], [], [], [],
            "Architecture",
            [],
            verification ?? ValidVerification(),
            deviations,
            openIssues,
            commitCreated,
            pushPerformed);

    private static DeveloperReviewVerification ValidVerification() => new(
        true, 0, 0, true, 10, 0, 0, true);
}
