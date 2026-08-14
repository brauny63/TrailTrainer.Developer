using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class PersistedLifecycleSelector : IPersistedLifecycleSelector
{
    private readonly IDeveloperLifecycleStateDiscovery discovery;

    public PersistedLifecycleSelector(IDeveloperLifecycleStateDiscovery discovery)
    {
        this.discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
    }

    public async Task<PersistedLifecycleSelectionResult> SelectAsync(
        PersistedLifecycleSelectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var states = await discovery.ListAsync(cancellationToken);

        DeveloperLifecyclePersistedState? selected = request.Mode switch
        {
            PersistedLifecycleSelectionMode.ExactTaskId => SelectExact(states, request.TaskId!),
            PersistedLifecycleSelectionMode.Oldest => states
                .OrderBy(state => state.SavedAtUtc)
                .ThenBy(state => state.TaskId, StringComparer.Ordinal)
                .FirstOrDefault(),
            PersistedLifecycleSelectionMode.Newest => states
                .OrderByDescending(state => state.SavedAtUtc)
                .ThenByDescending(state => state.TaskId, StringComparer.Ordinal)
                .FirstOrDefault(),
            _ => throw new InvalidOperationException("The persisted lifecycle selection mode is unsupported.")
        };

        return selected is null
            ? new PersistedLifecycleSelectionResult(PersistedLifecycleSelectionState.NotFound)
            : new PersistedLifecycleSelectionResult(PersistedLifecycleSelectionState.Found, selected);
    }

    private static DeveloperLifecyclePersistedState? SelectExact(
        IEnumerable<DeveloperLifecyclePersistedState> states,
        string taskId)
    {
        var matches = states
            .Where(state => string.Equals(state.TaskId, taskId, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        return matches.Length switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new InvalidOperationException(
                $"Discovery returned duplicate persisted states for TaskId '{taskId}'.")
        };
    }
}
