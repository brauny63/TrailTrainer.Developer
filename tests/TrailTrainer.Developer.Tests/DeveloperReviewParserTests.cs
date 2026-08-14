using System.Text;
using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class DeveloperReviewParserTests
{
    private readonly DeveloperReviewParser parser = new();

    [Theory]
    [InlineData("–")]
    [InlineData("-")]
    public async Task ParseAsync_ValidCurrentReport_ParsesStructuredContent(string separator)
    {
        using var directory = TemporaryDirectory.Create();
        var filePath = WriteReport(directory.Path, BuildReport(separator: separator));

        var review = await parser.ParseAsync(filePath);

        Assert.Equal(new DeveloperTaskId(10), review.TaskId);
        Assert.Equal("Review Report Parsing and Validation", review.Title);
        Assert.True(Path.IsPathFullyQualified(review.FilePath));
        Assert.Equal(DeveloperReviewStatus.ReadyForReview, review.Status);
        Assert.Equal("Summary text.", review.Summary);
        Assert.Equal(["First requirement", "Second requirement"], review.RequirementsImplemented);
        Assert.Equal(["created.txt"], review.FilesCreated);
        Assert.Empty(review.FilesModified);
        Assert.Empty(review.FilesDeleted);
        Assert.Equal("Architecture notes.", review.ArchitectureNotes);
        Assert.Equal(["Parser tests"], review.TestsAdded);
        Assert.True(review.Verification.BuildSuccessful);
        Assert.Equal(2, review.Verification.BuildWarningCount);
        Assert.Equal(0, review.Verification.BuildErrorCount);
        Assert.True(review.Verification.TestSuccessful);
        Assert.Equal(12, review.Verification.TestsPassed);
        Assert.Equal(0, review.Verification.TestsFailed);
        Assert.Equal(1, review.Verification.TestsSkipped);
        Assert.True(review.Verification.DiffCheckSuccessful);
        Assert.Equal("None.", review.Deviations);
        Assert.Equal("None", review.OpenIssues);
        Assert.False(review.CommitCreated);
        Assert.False(review.PushPerformed);
    }

    [Fact]
    public async Task ParseAsync_BlockedStatus_ParsesBlocked()
    {
        using var directory = TemporaryDirectory.Create();
        var review = await parser.ParseAsync(WriteReport(directory.Path, BuildReport(status: "BLOCKED")));
        Assert.Equal(DeveloperReviewStatus.Blocked, review.Status);
    }

    [Fact]
    public async Task ParseAsync_UnknownStatus_Throws()
    {
        using var directory = TemporaryDirectory.Create();
        await Assert.ThrowsAsync<InvalidDataException>(() => parser.ParseAsync(
            WriteReport(directory.Path, BuildReport(status: "DONE"))));
    }

    [Theory]
    [InlineData("Failed. 3 warnings, 4 errors.", "Successful. 12 passed, 0 failed, 1 skipped.", "Successful.")]
    [InlineData("Successful. 2 warnings, 0 errors.", "Failed. 8 passed, 4 failed, 2 skipped.", "Successful.")]
    [InlineData("Successful. 2 warnings, 0 errors.", "Successful. 12 passed, 0 failed, 1 skipped.", "Failed. whitespace errors.")]
    public async Task ParseAsync_FailedVerification_ParsesFailureAndCounts(
        string build,
        string test,
        string diff)
    {
        using var directory = TemporaryDirectory.Create();
        var review = await parser.ParseAsync(WriteReport(
            directory.Path,
            BuildReport(build: build, test: test, diff: diff)));

        Assert.Equal(build.StartsWith("Successful", StringComparison.Ordinal), review.Verification.BuildSuccessful);
        Assert.Equal(test.StartsWith("Successful", StringComparison.Ordinal), review.Verification.TestSuccessful);
        Assert.Equal(diff.StartsWith("Successful", StringComparison.Ordinal), review.Verification.DiffCheckSuccessful);
    }

    [Fact]
    public async Task ParseAsync_ExplicitCommitAndPush_MapsTrue()
    {
        using var directory = TemporaryDirectory.Create();
        var review = await parser.ParseAsync(WriteReport(
            directory.Path,
            BuildReport(commitAndPush: "Commit created.\nPush performed.")));

        Assert.True(review.CommitCreated);
        Assert.True(review.PushPerformed);
    }

    [Fact]
    public async Task ParseAsync_InvalidFilename_Throws()
    {
        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "DEV-0010.md");
        File.WriteAllText(path, BuildReport());
        await Assert.ThrowsAsync<InvalidDataException>(() => parser.ParseAsync(path));
    }

    [Fact]
    public async Task ParseAsync_FilenameHeadingMismatch_Throws()
    {
        using var directory = TemporaryDirectory.Create();
        await Assert.ThrowsAsync<InvalidDataException>(() => parser.ParseAsync(
            WriteReport(directory.Path, BuildReport(headingId: "0011"))));
    }

    [Theory]
    [InlineData("")]
    [InlineData("# REVIEW-0010 -   \n")]
    public async Task ParseAsync_MissingOrEmptyHeading_Throws(string heading)
    {
        using var directory = TemporaryDirectory.Create();
        var report = BuildReport();
        report = report[(report.IndexOf('\n') + 1)..];
        await Assert.ThrowsAsync<InvalidDataException>(() => parser.ParseAsync(
            WriteReport(directory.Path, heading + report)));
    }

    public static TheoryData<string> RequiredSections => new()
    {
        "Status", "Summary", "Requirements Implemented", "Files Created", "Files Modified",
        "Files Deleted", "Architecture / Refactoring Notes", "Tests Added", "Verification",
        "Deviations from DEV-0010", "Open Issues / Known Limitations", "Commit and Push"
    };

    [Theory]
    [MemberData(nameof(RequiredSections))]
    public async Task ParseAsync_MissingRequiredSection_Throws(string omittedSection)
    {
        using var directory = TemporaryDirectory.Create();
        await Assert.ThrowsAsync<InvalidDataException>(() => parser.ParseAsync(
            WriteReport(directory.Path, BuildReport(omittedSection: omittedSection))));
    }

    [Fact]
    public async Task ParseAsync_DeviationsIdMismatch_Throws()
    {
        using var directory = TemporaryDirectory.Create();
        await Assert.ThrowsAsync<InvalidDataException>(() => parser.ParseAsync(
            WriteReport(directory.Path, BuildReport(deviationsId: "0011"))));
    }

    [Theory]
    [InlineData("build")]
    [InlineData("test")]
    [InlineData("diff")]
    public async Task ParseAsync_MissingVerificationSubsection_Throws(string subsection)
    {
        using var directory = TemporaryDirectory.Create();
        await Assert.ThrowsAsync<InvalidDataException>(() => parser.ParseAsync(
            WriteReport(directory.Path, BuildReport(omitVerification: subsection))));
    }

    [Theory]
    [InlineData("Successful with zero warnings")]
    [InlineData("Successful. x warnings, 0 errors.")]
    public async Task ParseAsync_MalformedVerification_Throws(string malformedBuild)
    {
        using var directory = TemporaryDirectory.Create();
        await Assert.ThrowsAsync<InvalidDataException>(() => parser.ParseAsync(
            WriteReport(directory.Path, BuildReport(build: malformedBuild))));
    }

    [Theory]
    [InlineData("No commit created.")]
    [InlineData("No push performed.")]
    [InlineData("Unknown state.")]
    public async Task ParseAsync_UndeterminableCommitOrPush_Throws(string content)
    {
        using var directory = TemporaryDirectory.Create();
        await Assert.ThrowsAsync<InvalidDataException>(() => parser.ParseAsync(
            WriteReport(directory.Path, BuildReport(commitAndPush: content))));
    }

    [Fact]
    public async Task ParseAsync_PreCanceledToken_ThrowsCancellation()
    {
        using var directory = TemporaryDirectory.Create();
        var path = WriteReport(directory.Path, BuildReport());
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => parser.ParseAsync(path, source.Token));
    }

    private static string WriteReport(string directory, string content)
    {
        var path = Path.Combine(directory, "REVIEW-0010.md");
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }

    private static string BuildReport(
        string separator = "–",
        string headingId = "0010",
        string status = "READY FOR REVIEW",
        string build = "Successful. 2 warnings, 0 errors.",
        string test = "Successful. 12 passed, 0 failed, 1 skipped.",
        string diff = "Successful. No whitespace errors.",
        string commitAndPush = "No commit created.\nNo push performed.",
        string deviationsId = "0010",
        string? omittedSection = null,
        string? omitVerification = null)
    {
        var sections = new List<(string Name, string Content)>
        {
            ("Status", status),
            ("Summary", "Summary text."),
            ("Requirements Implemented", "- First requirement\n- Second requirement"),
            ("Files Created", "- `created.txt`"),
            ("Files Modified", "None."),
            ("Files Deleted", "None"),
            ("Architecture / Refactoring Notes", "Architecture notes."),
            ("Tests Added", "- Parser tests"),
            ("Verification", BuildVerification(build, test, diff, omitVerification)),
            ($"Deviations from DEV-{deviationsId}", "None."),
            ("Open Issues / Known Limitations", "None"),
            ("Commit and Push", commitAndPush)
        };

        var builder = new StringBuilder($"# REVIEW-{headingId} {separator} Review Report Parsing and Validation\n\n");
        foreach (var section in sections.Where(section => section.Name != omittedSection))
        {
            builder.Append("## ").Append(section.Name).Append("\n\n")
                .Append(section.Content).Append("\n\n");
        }

        return builder.ToString();
    }

    private static string BuildVerification(string build, string test, string diff, string? omitted)
    {
        var parts = new List<string>();
        if (omitted != "build") parts.Add($"### dotnet build\n\n{build}");
        if (omitted != "test") parts.Add($"### dotnet test\n\n{test}");
        if (omitted != "diff") parts.Add($"### git diff --check\n\n{diff}");
        return string.Join("\n\n", parts);
    }
}
