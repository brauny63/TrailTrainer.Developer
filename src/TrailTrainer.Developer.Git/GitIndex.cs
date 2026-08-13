namespace TrailTrainer.Developer.Git;

internal static class GitIndex
{
    public static async Task<bool> HasStagedChangesAsync(
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        var result = await GitProcessRunner.RunAsync(
            repositoryRoot,
            cancellationToken,
            "diff",
            "--cached",
            "--quiet",
            "--exit-code",
            "--");

        return result.ExitCode switch
        {
            0 => false,
            1 => true,
            _ => throw result.CreateException("determine whether the index contains staged changes")
        };
    }
}
