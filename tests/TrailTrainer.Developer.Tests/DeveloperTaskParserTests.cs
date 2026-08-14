using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class DeveloperTaskParserTests
{
    private readonly DeveloperTaskParser parser = new();

    [Theory]
    [InlineData("–")]
    [InlineData("-")]
    public async Task ParseAsync_ValidDocument_ParsesHeadingMetadataAndPaths(string separator)
    {
        using var directory = TemporaryDirectory.Create();
        var filePath = WriteTask(directory.Path, "DEV-0006-Valid.md", $"""
            # DEV-0006 {separator} Discovery and Parsing

            ## Metadata

            - Task ID: `DEV-0006`
            - Repository: `TrailTrainer.Developer`
            - Expected branch: `feature/dev-0006`
            - Review report: `docs/developer-reviews/REVIEW-0006.md`
            - Additional value: tolerated

            ## Goal
            Content
            """);

        var document = await parser.ParseAsync(filePath);

        Assert.Equal(6, document.Id.Number);
        Assert.Equal("Discovery and Parsing", document.Title);
        Assert.True(Path.IsPathFullyQualified(document.FilePath));
        Assert.Equal("TrailTrainer.Developer", document.Repository);
        Assert.Equal("feature/dev-0006", document.ExpectedBranch);
        Assert.Equal("docs/developer-reviews/REVIEW-0006.md", document.ReviewReportPath);
    }

    [Fact]
    public async Task ParseAsync_FilenameAndHeadingMismatch_Throws()
    {
        using var directory = TemporaryDirectory.Create();
        var filePath = WriteTask(directory.Path, "DEV-0006-Task.md", ValidDocument("DEV-0007", "DEV-0007"));

        await Assert.ThrowsAsync<InvalidDataException>(() => parser.ParseAsync(filePath));
    }

    [Fact]
    public async Task ParseAsync_MetadataAndHeadingMismatch_Throws()
    {
        using var directory = TemporaryDirectory.Create();
        var filePath = WriteTask(directory.Path, "DEV-0006-Task.md", ValidDocument("DEV-0006", "DEV-0007"));

        await Assert.ThrowsAsync<InvalidDataException>(() => parser.ParseAsync(filePath));
    }

    [Fact]
    public async Task ParseAsync_MissingHeading_Throws()
    {
        using var directory = TemporaryDirectory.Create();
        var filePath = WriteTask(directory.Path, "DEV-0006-Task.md", "## Metadata\n- Task ID: DEV-0006");

        await Assert.ThrowsAsync<InvalidDataException>(() => parser.ParseAsync(filePath));
    }

    [Fact]
    public async Task ParseAsync_MissingMetadataSection_Throws()
    {
        using var directory = TemporaryDirectory.Create();
        var filePath = WriteTask(directory.Path, "DEV-0006-Task.md", "# DEV-0006 – Title");

        await Assert.ThrowsAsync<InvalidDataException>(() => parser.ParseAsync(filePath));
    }

    [Theory]
    [InlineData("Task ID")]
    [InlineData("Repository")]
    [InlineData("Expected branch")]
    [InlineData("Review report")]
    public async Task ParseAsync_MissingRequiredMetadata_Throws(string missingKey)
    {
        using var directory = TemporaryDirectory.Create();
        var metadata = new Dictionary<string, string>
        {
            ["Task ID"] = "DEV-0006",
            ["Repository"] = "TrailTrainer.Developer",
            ["Expected branch"] = "feature/dev-0006",
            ["Review report"] = "docs/developer-reviews/REVIEW-0006.md"
        };
        metadata.Remove(missingKey);
        var metadataLines = string.Join(Environment.NewLine, metadata.Select(item => $"- {item.Key}: `{item.Value}`"));
        var filePath = WriteTask(directory.Path, "DEV-0006-Task.md", $"# DEV-0006 – Title\n\n## Metadata\n{metadataLines}");

        await Assert.ThrowsAsync<InvalidDataException>(() => parser.ParseAsync(filePath));
    }

    [Fact]
    public async Task ParseAsync_InvalidFilename_Throws()
    {
        using var directory = TemporaryDirectory.Create();
        var filePath = WriteTask(directory.Path, "Task.md", ValidDocument("DEV-0006", "DEV-0006"));

        await Assert.ThrowsAsync<InvalidDataException>(() => parser.ParseAsync(filePath));
    }

    private static string ValidDocument(string headingId, string metadataId) => $$"""
        # {{headingId}} – Title

        ## Metadata

        - Task ID: `{{metadataId}}`
        - Repository: `TrailTrainer.Developer`
        - Expected branch: `feature/dev-0006`
        - Review report: `docs/developer-reviews/REVIEW-0006.md`
        """;

    private static string WriteTask(string directoryPath, string fileName, string content)
    {
        var filePath = Path.Combine(directoryPath, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }
}
