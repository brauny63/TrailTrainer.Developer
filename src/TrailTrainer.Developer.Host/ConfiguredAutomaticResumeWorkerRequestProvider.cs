using Microsoft.Extensions.Options;
using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Host;

public sealed class ConfiguredAutomaticResumeWorkerRequestProvider : IAutomaticResumeWorkerRequestProvider
{
    private readonly IOptions<AutomaticResumeHostOptions> options;

    public ConfiguredAutomaticResumeWorkerRequestProvider(IOptions<AutomaticResumeHostOptions> options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public AutomaticResumeWorkerRequest GetRequest()
    {
        var configured = options.Value;
        var stepRequest = new AutomaticResumeBatchStepRequest(
            configured.MergeMethod,
            configured.MergeCommitTitle,
            configured.MergeCommitMessage,
            configured.DeleteRemoteBranch);
        var batchRunRequest = new AutomaticResumeBatchRunRequest(
            stepRequest,
            configured.MaximumSteps);
        var runRequest = new AutomaticResumeRunRequest(
            batchRunRequest,
            configured.MaximumBatchRuns);
        var executionRequest = new RepeatedDelayedAutomaticResumeRequest(
            runRequest,
            configured.ResumeDelay,
            configured.MaximumRuns);
        return new AutomaticResumeWorkerRequest(executionRequest);
    }
}
