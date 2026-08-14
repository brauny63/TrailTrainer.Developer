using System.Text;
using System.Text.RegularExpressions;
using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed partial class DeveloperTaskParser : IDeveloperTaskParser
{
    public async Task<DeveloperTaskDocument> ParseAsync(
        string developerTaskFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(developerTaskFilePath);

        var filePath = Path.GetFullPath(developerTaskFilePath);
        var fileName = Path.GetFileName(filePath);
        if (!DeveloperTaskFileConvention.TryParseFileName(fileName, out var fileId))
        {
            throw Invalid(filePath, "filename does not match 'DEV-NNNN-<descriptive-name>.md'");
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Developer Task file '{filePath}' does not exist.", filePath);
        }

        var markdown = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        var headingLine = lines.FirstOrDefault(line => line.StartsWith("# ", StringComparison.Ordinal));
        if (headingLine is null)
        {
            throw Invalid(filePath, "a level-1 heading is required");
        }

        var headingMatch = HeadingPattern().Match(headingLine);
        if (!headingMatch.Success)
        {
            throw Invalid(filePath, "the level-1 heading must contain a task ID and title");
        }

        var headingId = ParseId(headingMatch.Groups[1].Value, filePath, "heading");
        var title = headingMatch.Groups[2].Value.Trim();
        if (headingId != fileId)
        {
            throw Invalid(filePath, $"filename ID '{fileId}' differs from heading ID '{headingId}'");
        }

        var metadataHeadingIndex = Array.FindIndex(
            lines,
            line => string.Equals(line.Trim(), "## Metadata", StringComparison.Ordinal));
        if (metadataHeadingIndex < 0)
        {
            throw Invalid(filePath, "the '## Metadata' section is required");
        }

        var metadata = ParseMetadata(lines, metadataHeadingIndex + 1);
        var metadataTaskId = ParseId(
            RequiredMetadata(metadata, "Task ID", filePath),
            filePath,
            "metadata");
        if (metadataTaskId != fileId)
        {
            throw Invalid(filePath, $"metadata ID '{metadataTaskId}' differs from task ID '{fileId}'");
        }

        return new DeveloperTaskDocument(
            fileId,
            title,
            filePath,
            RequiredMetadata(metadata, "Repository", filePath),
            RequiredMetadata(metadata, "Expected branch", filePath),
            RequiredMetadata(metadata, "Review report", filePath));
    }

    private static Dictionary<string, string> ParseMetadata(string[] lines, int startIndex)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = startIndex; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                break;
            }

            var match = MetadataPattern().Match(line);
            if (match.Success)
            {
                metadata[match.Groups[1].Value.Trim()] = UnwrapBackticks(match.Groups[2].Value.Trim());
            }
        }

        return metadata;
    }

    private static string RequiredMetadata(
        IReadOnlyDictionary<string, string> metadata,
        string key,
        string filePath)
    {
        if (!metadata.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw Invalid(filePath, $"required metadata '{key}' is missing or empty");
        }

        return value;
    }

    private static DeveloperTaskId ParseId(string value, string filePath, string source)
    {
        var match = TaskIdPattern().Match(value);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var number))
        {
            throw Invalid(filePath, $"the {source} task ID '{value}' is invalid");
        }

        return new DeveloperTaskId(number);
    }

    private static string UnwrapBackticks(string value) =>
        value.Length >= 2 && value[0] == '`' && value[^1] == '`'
            ? value[1..^1].Trim()
            : value;

    private static InvalidDataException Invalid(string filePath, string diagnostic) =>
        new($"Developer Task file '{filePath}' is invalid: {diagnostic}.");

    [GeneratedRegex(@"^#\s+(DEV-\d{4})\s+(?:–|-)\s+(.+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^-\s*([^:]+):\s*(.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex MetadataPattern();

    [GeneratedRegex(@"^DEV-(\d{4})$", RegexOptions.CultureInvariant)]
    private static partial Regex TaskIdPattern();
}
