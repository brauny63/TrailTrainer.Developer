namespace TrailTrainer.Developer.Core;

public sealed record RepeatedDelayedAutomaticResumeRequest
{
    public RepeatedDelayedAutomaticResumeRequest(
        AutomaticResumeRunRequest runRequest,
        TimeSpan resumeDelay,
        int maximumRuns)
    {
        ArgumentNullException.ThrowIfNull(runRequest);
        if (resumeDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(resumeDelay), "Resume delay must be greater than zero.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRuns);
        RunRequest = runRequest;
        ResumeDelay = resumeDelay;
        MaximumRuns = maximumRuns;
    }

    public AutomaticResumeRunRequest RunRequest { get; }
    public TimeSpan ResumeDelay { get; }
    public int MaximumRuns { get; }
}
