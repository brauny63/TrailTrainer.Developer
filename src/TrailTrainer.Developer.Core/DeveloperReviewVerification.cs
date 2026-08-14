namespace TrailTrainer.Developer.Core;

public sealed record DeveloperReviewVerification(
    bool BuildSuccessful,
    int BuildWarningCount,
    int BuildErrorCount,
    bool TestSuccessful,
    int TestsPassed,
    int TestsFailed,
    int TestsSkipped,
    bool DiffCheckSuccessful);
