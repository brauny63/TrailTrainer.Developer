namespace TrailTrainer.Developer.Core;

public interface IGitPusher
{
    Task<GitPushResult> PushAsync(
        string directoryPath,
        string remoteName,
        bool setUpstream,
        CancellationToken cancellationToken = default);
}
