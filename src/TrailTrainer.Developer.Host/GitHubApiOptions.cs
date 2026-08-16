namespace TrailTrainer.Developer.Host;

public sealed class GitHubApiOptions
{
    public const string SectionName = "GitHub";

    public string Token { get; set; } = string.Empty;
}
