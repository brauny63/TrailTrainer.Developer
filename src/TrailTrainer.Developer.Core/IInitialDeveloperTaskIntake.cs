namespace TrailTrainer.Developer.Core;

public interface IInitialDeveloperTaskIntake
{
    Task<InitialDeveloperTaskIntakeResult> ExecuteAsync(
        InitialDeveloperTaskIntakeRequest request,
        CancellationToken cancellationToken = default);
}
