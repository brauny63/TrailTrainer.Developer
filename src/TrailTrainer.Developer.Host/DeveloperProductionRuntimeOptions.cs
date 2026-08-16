namespace TrailTrainer.Developer.Host;

public sealed class DeveloperProductionRuntimeOptions
{
    public const string SectionName = "DeveloperProductionRuntime";

    public string LifecycleStateStorageDirectory { get; set; } = string.Empty;
}
