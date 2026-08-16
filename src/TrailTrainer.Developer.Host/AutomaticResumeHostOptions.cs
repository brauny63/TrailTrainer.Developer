using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Host;

public sealed class AutomaticResumeHostOptions
{
    public const string SectionName = "AutomaticResume";

    public PullRequestMergeMethod MergeMethod { get; set; } = PullRequestMergeMethod.Squash;
    public string? MergeCommitTitle { get; set; }
    public string? MergeCommitMessage { get; set; }
    public bool DeleteRemoteBranch { get; set; }
    public int MaximumSteps { get; set; } = 1;
    public int MaximumBatchRuns { get; set; } = 1;
    public TimeSpan ResumeDelay { get; set; } = TimeSpan.FromMinutes(5);
    public int MaximumRuns { get; set; } = 1;
}
