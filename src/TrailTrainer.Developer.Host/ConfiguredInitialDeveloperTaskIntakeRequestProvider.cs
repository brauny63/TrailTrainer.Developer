using Microsoft.Extensions.Options;
using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Host;

public sealed class ConfiguredInitialDeveloperTaskIntakeRequestProvider
    : IInitialDeveloperTaskIntakeRequestProvider
{
    private readonly IOptions<InitialTaskIntakeOptions> intakeOptions;
    private readonly IOptions<AutomaticResumeHostOptions> resumeOptions;

    public ConfiguredInitialDeveloperTaskIntakeRequestProvider(
        IOptions<InitialTaskIntakeOptions> intakeOptions,
        IOptions<AutomaticResumeHostOptions> resumeOptions)
    {
        this.intakeOptions = intakeOptions ?? throw new ArgumentNullException(nameof(intakeOptions));
        this.resumeOptions = resumeOptions ?? throw new ArgumentNullException(nameof(resumeOptions));
    }

    public InitialDeveloperTaskIntakeRequest GetRequest()
    {
        var intake = intakeOptions.Value;
        var resume = resumeOptions.Value;
        return new InitialDeveloperTaskIntakeRequest(
            intake.Enabled,
            intake.RepositoryPath,
            intake.RepositoryName,
            intake.GitHubOwner,
            intake.BaseBranch,
            intake.RemoteName,
            resume.MergeMethod,
            resume.MergeCommitTitle,
            resume.MergeCommitMessage,
            resume.DeleteRemoteBranch);
    }
}
