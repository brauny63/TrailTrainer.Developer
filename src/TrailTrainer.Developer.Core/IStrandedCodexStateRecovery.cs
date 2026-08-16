namespace TrailTrainer.Developer.Core;

public interface IStrandedCodexStateRecovery
{
    Task<StrandedCodexStateRecoveryResult> TryRecoverAsync(
        InitialDeveloperTaskIntakeRequest request,
        CancellationToken cancellationToken = default);
}
