using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Host;

public interface ICodexCompatibilityProbe
{
    Task<CodexTaskExecutionResult> ProbeAsync(CancellationToken cancellationToken = default);
}
