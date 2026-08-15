namespace TrailTrainer.Developer.Core;

public sealed record AutomaticResumeRunRequest
{
    public AutomaticResumeRunRequest(
        AutomaticResumeBatchRunRequest batchRunRequest,
        int maximumBatchRuns)
    {
        ArgumentNullException.ThrowIfNull(batchRunRequest);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBatchRuns);
        BatchRunRequest = batchRunRequest;
        MaximumBatchRuns = maximumBatchRuns;
    }

    public AutomaticResumeBatchRunRequest BatchRunRequest { get; }
    public int MaximumBatchRuns { get; }
}
