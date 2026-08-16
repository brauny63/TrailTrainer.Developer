namespace TrailTrainer.Developer.Core;

public sealed record CodexTaskExecutionResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut = false)
{
    public bool Succeeded => ExitCode == 0 && !TimedOut;
}
