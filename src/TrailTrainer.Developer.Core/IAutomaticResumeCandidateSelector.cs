namespace TrailTrainer.Developer.Core;

public interface IAutomaticResumeCandidateSelector
{
    Task<AutomaticResumeCandidateResult> SelectAsync(
        CancellationToken cancellationToken = default);
}
