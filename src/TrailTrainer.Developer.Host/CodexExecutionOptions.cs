namespace TrailTrainer.Developer.Host;

public sealed class CodexExecutionOptions
{
    public const string SectionName = "CodexExecution";
    public string ExecutablePath { get; set; } = string.Empty;
    public string[] AdditionalArguments { get; set; } = [];
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);
    public int MaximumDiagnosticCharacters { get; set; } = 16_384;
}
