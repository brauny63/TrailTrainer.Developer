namespace TrailTrainer.Developer.Host;

public sealed class CodexCompatibilityProbeCommand(ICodexCompatibilityProbe probe)
{
    public async Task<int> RunAsync(TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        var result = await probe.ProbeAsync(cancellationToken);
        await output.WriteLineAsync($"Codex compatibility probe: exit={result.ExitCode}; timedOut={result.TimedOut}; failure={result.FailureKind}");
        await output.WriteLineAsync($"stdout: {result.StandardOutput}");
        await output.WriteLineAsync($"stderr: {result.StandardError}");
        return result.Succeeded ? 0 : 1;
    }
}
