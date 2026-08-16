namespace TrailTrainer.Developer.Core;

public static class DeveloperReviewContract
{
    public static IReadOnlyList<string> RequiredSectionNames { get; } =
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

    public static string CreateCodexInstruction(string taskFilePath, bool repairReviewOnly = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskFilePath);
        var recovery = repairReviewOnly
            ? "Implementation output already exists. Inspect and repair only the invalid review; do not reset, clean, stash, overwrite, or duplicate implementation work. "
            : string.Empty;

        return $"Work the Developer Task at {taskFilePath} completely. " +
            "Follow its scope, requirements, architecture constraints, and verification steps. " + recovery +
            "DeveloperReviewParser is the authoritative review contract. Create the required review file with a filename and level-1 heading ID matching the task ID. " +
            "Use exactly these level-2 headings, without synonyms: " +
            string.Join(", ", RequiredSectionNames.Select(name => $"## {name}")) +
            ", and ## Deviations from DEV-NNNN with DEV-NNNN replaced by the exact task ID. " +
            "Status must be exactly READY FOR REVIEW or BLOCKED. Files Created, Files Modified, and Files Deleted must each contain bullet-list entries or None. " +
            "Under ## Verification emit exactly ### dotnet build, whose first non-empty line is Successful. N warnings, N errors. or Failed. N warnings, N errors.; " +
            "### dotnet test, whose first non-empty line is Successful. N passed, N failed, N skipped. or Failed. N passed, N failed, N skipped.; " +
            "and ### git diff --check, whose first non-empty line begins exactly Successful. or Failed. Replace every N with the actual non-negative count. " +
            "Under ## Commit and Push emit parser-supported state lines, including No commit created. and No push performed. " +
            "Do not invent alternative section names. Do not modify the Developer Task. Do not commit and do not push.";
    }
}
