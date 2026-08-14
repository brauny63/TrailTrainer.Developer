using System.Text.RegularExpressions;
using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

internal static partial class DeveloperTaskFileConvention
{
    public static bool TryParseFileName(string fileName, out DeveloperTaskId id)
    {
        var match = FileNamePattern().Match(fileName);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var number))
        {
            id = new DeveloperTaskId(number);
            return true;
        }

        id = default;
        return false;
    }

    [GeneratedRegex(@"^DEV-(\d{4})-.+\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex FileNamePattern();
}
