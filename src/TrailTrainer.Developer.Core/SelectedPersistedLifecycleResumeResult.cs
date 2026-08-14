namespace TrailTrainer.Developer.Core;

public sealed record SelectedPersistedLifecycleResumeResult
{
    public SelectedPersistedLifecycleResumeResult(
        SelectedPersistedLifecycleResumeState state,
        PersistedLifecycleSelectionResult selection,
        PersistedDeveloperLifecycleResumeResult? resume = null)
    {
        ArgumentNullException.ThrowIfNull(selection);
        switch (state)
        {
            case SelectedPersistedLifecycleResumeState.NotFound when
                selection.State != PersistedLifecycleSelectionState.NotFound || resume is not null:
                throw new ArgumentException(
                    "A NotFound result requires a NotFound selection and no resume result.");
            case SelectedPersistedLifecycleResumeState.Pending when
                selection.State != PersistedLifecycleSelectionState.Found ||
                resume?.State != PersistedDeveloperLifecycleResumeState.Pending:
                throw new ArgumentException(
                    "A Pending result requires a Found selection and Pending resume result.");
            case SelectedPersistedLifecycleResumeState.Failed when
                selection.State != PersistedLifecycleSelectionState.Found ||
                resume?.State != PersistedDeveloperLifecycleResumeState.Failed:
                throw new ArgumentException(
                    "A Failed result requires a Found selection and Failed resume result.");
            case SelectedPersistedLifecycleResumeState.Completed when
                selection.State != PersistedLifecycleSelectionState.Found ||
                resume?.State != PersistedDeveloperLifecycleResumeState.Completed:
                throw new ArgumentException(
                    "A Completed result requires a Found selection and Completed resume result.");
            case SelectedPersistedLifecycleResumeState.NotFound:
            case SelectedPersistedLifecycleResumeState.Pending:
            case SelectedPersistedLifecycleResumeState.Failed:
            case SelectedPersistedLifecycleResumeState.Completed:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state));
        }

        State = state;
        Selection = selection;
        Resume = resume;
    }

    public SelectedPersistedLifecycleResumeState State { get; }
    public PersistedLifecycleSelectionResult Selection { get; }
    public PersistedDeveloperLifecycleResumeResult? Resume { get; }
}
