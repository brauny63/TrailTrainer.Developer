namespace TrailTrainer.Developer.Host;

public sealed class InitialTaskIntakeOptions
{
    public const string SectionName = "InitialTaskIntake";

    public bool Enabled { get; set; }
    public string RepositoryPath { get; set; } = string.Empty;
    public string RepositoryName { get; set; } = string.Empty;
    public string GitHubOwner { get; set; } = string.Empty;
    public string BaseBranch { get; set; } = "main";
    public string RemoteName { get; set; } = "origin";
}
