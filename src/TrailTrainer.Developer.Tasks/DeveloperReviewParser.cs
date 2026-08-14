using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed partial class DeveloperReviewParser : IDeveloperReviewParser
{
    private static readonly string[] RequiredSectionNames =
    [
        "Status",
        "Summary",
        "Requirements Implemented",
        "Files Created",
        "Files Modified",
        "Files Deleted",
        "Architecture / Refactoring Notes",
        "Tests Added",
        "Verification",
        "Open Issues / Known Limitations",
        "Commit and Push"
    ];

    public async Task<DeveloperReviewDocument> ParseAsync(
        string reviewFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewFilePath);

        var filePath = Path.GetFullPath(reviewFilePath);
        var fileNameMatch = FileNamePattern().Match(Path.GetFileName(filePath));
        if (!fileNameMatch.Success)
        {
            throw Invalid(filePath, "filename does not match 'REVIEW-NNNN.md'");
        }

        var fileId = ParseReviewId(fileNameMatch.Groups[1].Value, filePath, "filename");
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Developer review file '{filePath}' does not exist.", filePath);
        }

        var markdown = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var headingLine = lines.FirstOrDefault(line => line.StartsWith("# ", StringComparison.Ordinal));
        if (headingLine is null)
        {
            throw Invalid(filePath, "a level-1 heading is required");
        }

        var headingMatch = HeadingPattern().Match(headingLine);
        if (!headingMatch.Success || string.IsNullOrWhiteSpace(headingMatch.Groups[2].Value))
        {
            throw Invalid(filePath, "the level-1 heading must contain a review ID and title");
        }

        var headingId = ParseReviewId(headingMatch.Groups[1].Value, filePath, "heading");
        if (headingId != fileId)
        {
            throw Invalid(filePath, $"filename ID '{fileId}' differs from heading ID '{headingId}'");
        }

        var sections = ParseSections(lines, filePath);
        foreach (var sectionName in RequiredSectionNames)
        {
            if (!sections.ContainsKey(sectionName))
            {
                throw Invalid(filePath, $"required section '## {sectionName}' is missing");
            }
        }

        var expectedDeviationsHeading = $"Deviations from {fileId}";
        var deviationsHeadings = sections.Keys
            .Where(name => name.StartsWith("Deviations from DEV-", StringComparison.Ordinal))
            .ToArray();
        if (deviationsHeadings.Length != 1 ||
            !string.Equals(deviationsHeadings[0], expectedDeviationsHeading, StringComparison.Ordinal))
        {
            throw Invalid(
                filePath,
                $"the deviations heading must be '## {expectedDeviationsHeading}'");
        }

        var status = ParseStatus(sections["Status"], filePath);
        var verification = ParseVerification(sections["Verification"], filePath);
        var commitAndPush = ParseCommitAndPush(sections["Commit and Push"], filePath);

        return new DeveloperReviewDocument(
            fileId,
            headingMatch.Groups[2].Value.Trim(),
            filePath,
            status,
            NormalizeText(sections["Summary"]),
            ParseBulletList(sections["Requirements Implemented"]),
            ParseFileList(sections["Files Created"]),
            ParseFileList(sections["Files Modified"]),
            ParseFileList(sections["Files Deleted"]),
            NormalizeText(sections["Architecture / Refactoring Notes"]),
            ParseBulletList(sections["Tests Added"]),
            verification,
            NormalizeText(sections[expectedDeviationsHeading]),
            NormalizeText(sections["Open Issues / Known Limitations"]),
            commitAndPush.CommitCreated,
            commitAndPush.PushPerformed);
    }

    private static Dictionary<string, string[]> ParseSections(string[] lines, string filePath)
    {
        var sections = new Dictionary<string, string[]>(StringComparer.Ordinal);
        for (var index = 0; index < lines.Length; index++)
        {
            if (!lines[index].StartsWith("## ", StringComparison.Ordinal))
            {
                continue;
            }

            var name = lines[index][3..].Trim();
            var contentStart = index + 1;
            var nextSection = Array.FindIndex(
                lines,
                contentStart,
                line => line.StartsWith("## ", StringComparison.Ordinal));
            var contentEnd = nextSection < 0 ? lines.Length : nextSection;
            if (!sections.TryAdd(name, lines[contentStart..contentEnd]))
            {
                throw Invalid(filePath, $"section '## {name}' occurs more than once");
            }

            index = contentEnd - 1;
        }

        return sections;
    }

    private static DeveloperReviewStatus ParseStatus(string[] lines, string filePath)
    {
        var value = FirstNonEmptyLine(lines);
        return value switch
        {
            "READY FOR REVIEW" => DeveloperReviewStatus.ReadyForReview,
            "BLOCKED" => DeveloperReviewStatus.Blocked,
            _ => throw Invalid(filePath, $"unsupported or missing review status '{value}'")
        };
    }

    private static DeveloperReviewVerification ParseVerification(string[] lines, string filePath)
    {
        var subsections = ParseVerificationSubsections(lines);
        var buildMatch = MatchVerification(
            subsections,
            "dotnet build",
            BuildVerificationPattern(),
            filePath);
        var testMatch = MatchVerification(
            subsections,
            "dotnet test",
            TestVerificationPattern(),
            filePath);
        var diffContent = RequiredSubsection(subsections, "git diff --check", filePath);
        var diffFirstLine = FirstNonEmptyLine(diffContent);
        var diffSuccessful = diffFirstLine.StartsWith("Successful.", StringComparison.Ordinal)
            ? true
            : diffFirstLine.StartsWith("Failed.", StringComparison.Ordinal)
                ? false
                : throw Invalid(filePath, "verification subsection 'git diff --check' is malformed");

        return new DeveloperReviewVerification(
            IsSuccessful(buildMatch.Groups[1].Value),
            ParseCount(buildMatch.Groups[2].Value),
            ParseCount(buildMatch.Groups[3].Value),
            IsSuccessful(testMatch.Groups[1].Value),
            ParseCount(testMatch.Groups[2].Value),
            ParseCount(testMatch.Groups[3].Value),
            ParseCount(testMatch.Groups[4].Value),
            diffSuccessful);
    }

    private static Dictionary<string, string[]> ParseVerificationSubsections(string[] lines)
    {
        var subsections = new Dictionary<string, string[]>(StringComparer.Ordinal);
        for (var index = 0; index < lines.Length; index++)
        {
            if (!lines[index].StartsWith("### ", StringComparison.Ordinal))
            {
                continue;
            }

            var name = lines[index][4..].Trim();
            var start = index + 1;
            var next = Array.FindIndex(lines, start, line => line.StartsWith("### ", StringComparison.Ordinal));
            var end = next < 0 ? lines.Length : next;
            subsections[name] = lines[start..end];
            index = end - 1;
        }

        return subsections;
    }

    private static Match MatchVerification(
        IReadOnlyDictionary<string, string[]> subsections,
        string name,
        Regex pattern,
        string filePath)
    {
        var content = RequiredSubsection(subsections, name, filePath);
        var match = pattern.Match(FirstNonEmptyLine(content));
        return match.Success
            ? match
            : throw Invalid(filePath, $"verification subsection '{name}' is malformed");
    }

    private static string[] RequiredSubsection(
        IReadOnlyDictionary<string, string[]> subsections,
        string name,
        string filePath) =>
        subsections.TryGetValue(name, out var content)
            ? content
            : throw Invalid(filePath, $"verification subsection '### {name}' is missing");

    private static (bool CommitCreated, bool PushPerformed) ParseCommitAndPush(
        string[] lines,
        string filePath)
    {
        bool? commitCreated = null;
        bool? pushPerformed = null;
        foreach (var line in lines.Select(line => line.Trim()).Where(line => line.Length > 0))
        {
            commitCreated = line switch
            {
                "No commit created." => false,
                "Commit created." or "A commit was created." => true,
                _ => commitCreated
            };
            pushPerformed = line switch
            {
                "No push performed." => false,
                "Push performed." or "A push was performed." => true,
                _ => pushPerformed
            };
        }

        if (commitCreated is null || pushPerformed is null)
        {
            throw Invalid(filePath, "commit and push state could not be determined");
        }

        return (commitCreated.Value, pushPerformed.Value);
    }

    private static IReadOnlyList<string> ParseFileList(string[] lines)
    {
        var text = NormalizeText(lines);
        return IsNone(text) ? [] : ParseBulletList(lines);
    }

    private static IReadOnlyList<string> ParseBulletList(string[] lines) => lines
        .Select(line => line.Trim())
        .Where(line => line.StartsWith("- ", StringComparison.Ordinal))
        .Select(line => UnwrapBackticks(line[2..].Trim()))
        .ToArray();

    private static string NormalizeText(string[] lines)
    {
        var start = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line));
        if (start < 0)
        {
            return string.Empty;
        }

        var end = Array.FindLastIndex(lines, line => !string.IsNullOrWhiteSpace(line));
        return string.Join(Environment.NewLine, lines[start..(end + 1)]);
    }

    private static string FirstNonEmptyLine(string[] lines) =>
        lines.FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))?.Trim() ?? string.Empty;

    private static string UnwrapBackticks(string value) =>
        value.Length >= 2 && value[0] == '`' && value[^1] == '`'
            ? value[1..^1]
            : value;

    private static bool IsNone(string value) =>
        string.Equals(value.Trim().TrimEnd('.'), "None", StringComparison.OrdinalIgnoreCase);

    private static bool IsSuccessful(string value) => value == "Successful";

    private static int ParseCount(string value) => int.Parse(value, CultureInfo.InvariantCulture);

    private static DeveloperTaskId ParseReviewId(string digits, string filePath, string source)
    {
        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ||
            number is < 1 or > 9999)
        {
            throw Invalid(filePath, $"the {source} review ID is invalid");
        }

        return new DeveloperTaskId(number);
    }

    private static InvalidDataException Invalid(string filePath, string diagnostic) =>
        new($"Developer review file '{filePath}' is invalid: {diagnostic}.");

    [GeneratedRegex(@"^REVIEW-(\d{4})\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex FileNamePattern();

    [GeneratedRegex(@"^#\s+REVIEW-(\d{4})\s+(?:–|-)\s+(.+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^(Successful|Failed)\.\s+(\d+) warnings,\s+(\d+) errors\.$", RegexOptions.CultureInvariant)]
    private static partial Regex BuildVerificationPattern();

    [GeneratedRegex(@"^(Successful|Failed)\.\s+(\d+) passed,\s+(\d+) failed,\s+(\d+) skipped\.$", RegexOptions.CultureInvariant)]
    private static partial Regex TestVerificationPattern();
}
