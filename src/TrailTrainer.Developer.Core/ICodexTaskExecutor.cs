namespace TrailTrainer.Developer.Core;

public interface ICodexTaskExecutor
{
    Task<CodexTaskExecutionResult> ExecuteAsync(
        CodexTaskExecutionRequest request,
        CancellationToken cancellationToken = default);
}
