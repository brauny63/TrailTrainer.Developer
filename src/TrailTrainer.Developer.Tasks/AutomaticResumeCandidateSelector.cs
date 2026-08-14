using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class AutomaticResumeCandidateSelector : IAutomaticResumeCandidateSelector
{
    private readonly IDeveloperLifecycleStateDiscovery discovery;

    public AutomaticResumeCandidateSelector(IDeveloperLifecycleStateDiscovery discovery)
    {
        this.discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
    }

    public async Task<AutomaticResumeCandidateResult> SelectAsync(
        CancellationToken cancellationToken = default)
    {
        var states = await discovery.ListAsync(cancellationToken)
            ?? throw new InvalidOperationException("Lifecycle state discovery returned a null collection.");
        var selected = states
            .OrderBy(state => state.SavedAtUtc)
            .ThenBy(state => state.TaskId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (selected is null)
        {
            return new AutomaticResumeCandidateResult(AutomaticResumeCandidateState.NotFound);
        }

        var target = new PersistedLifecycleResumeTarget(selected.TaskId, selected);
        return new AutomaticResumeCandidateResult(
            AutomaticResumeCandidateState.Found,
            selected,
            target);
    }
}
