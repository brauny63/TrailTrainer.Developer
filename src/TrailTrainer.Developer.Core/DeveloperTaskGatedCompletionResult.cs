namespace TrailTrainer.Developer.Core;

public sealed record DeveloperTaskGatedCompletionResult(
    DeveloperTaskId TaskId,
    DeveloperReviewValidationResult ReviewValidation,
    DeveloperTaskCompletionResult Completion);
