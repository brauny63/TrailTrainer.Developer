namespace TrailTrainer.Developer.Core;

public sealed record CodexTaskExecutionResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut = false,
    CodexExecutionFailureKind FailureKind = CodexExecutionFailureKind.None)
{
    public bool Succeeded => ExitCode == 0 && !TimedOut;
}

public enum CodexExecutionFailureKind
{
    None,
    RunnerPipeTimeout
}
