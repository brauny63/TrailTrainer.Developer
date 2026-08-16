namespace TrailTrainer.Developer.Core;

public interface ICodexExecutionStateStore
{
    Task<CodexExecutionState?> LoadAsync(string taskId, CancellationToken cancellationToken = default);
    Task SaveAsync(CodexExecutionState state, CancellationToken cancellationToken = default);
    Task DeleteAsync(string taskId, CancellationToken cancellationToken = default);
}
