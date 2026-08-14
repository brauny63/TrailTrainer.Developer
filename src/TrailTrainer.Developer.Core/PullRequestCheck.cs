namespace TrailTrainer.Developer.Core;

public sealed record PullRequestCheck
{
    public PullRequestCheck(string name, PullRequestCheckState state, Uri? detailsUrl = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        State = state;
        DetailsUrl = detailsUrl;
    }

    public string Name { get; }
    public PullRequestCheckState State { get; }
    public Uri? DetailsUrl { get; }
}
