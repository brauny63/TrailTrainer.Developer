namespace TrailTrainer.Developer.Core;

public sealed record PullRequestGatedMergeResult(
    PullRequestStatusGateResult StatusGate,
    PullRequestMergeResult Merge);
