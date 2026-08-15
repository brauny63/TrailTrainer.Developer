namespace TrailTrainer.Developer.Core;

public sealed record DelayedAutomaticResumeRequest
{
    public DelayedAutomaticResumeRequest(
        AutomaticResumeRunRequest runRequest,
        TimeSpan resumeDelay)
    {
        ArgumentNullException.ThrowIfNull(runRequest);
        if (resumeDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(resumeDelay), "Resume delay must be greater than zero.");
        }

        RunRequest = runRequest;
        ResumeDelay = resumeDelay;
    }

    public AutomaticResumeRunRequest RunRequest { get; }
    public TimeSpan ResumeDelay { get; }
}
