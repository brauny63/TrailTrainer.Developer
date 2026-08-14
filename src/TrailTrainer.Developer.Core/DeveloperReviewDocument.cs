namespace TrailTrainer.Developer.Core;

public sealed record DeveloperReviewDocument
{
    public DeveloperReviewDocument(
        DeveloperTaskId taskId,
        string title,
        string filePath,
        DeveloperReviewStatus status,
        string summary,
        IEnumerable<string> requirementsImplemented,
        IEnumerable<string> filesCreated,
        IEnumerable<string> filesModified,
        IEnumerable<string> filesDeleted,
        string architectureNotes,
        IEnumerable<string> testsAdded,
        DeveloperReviewVerification verification,
        string deviations,
        string openIssues,
        bool commitCreated,
        bool pushPerformed)
    {
        TaskId = taskId;
        Title = title;
        FilePath = filePath;
        Status = status;
        Summary = summary;
        RequirementsImplemented = requirementsImplemented.ToArray();
        FilesCreated = filesCreated.ToArray();
        FilesModified = filesModified.ToArray();
        FilesDeleted = filesDeleted.ToArray();
        ArchitectureNotes = architectureNotes;
        TestsAdded = testsAdded.ToArray();
        Verification = verification;
        Deviations = deviations;
        OpenIssues = openIssues;
        CommitCreated = commitCreated;
        PushPerformed = pushPerformed;
    }

    public DeveloperTaskId TaskId { get; }
    public string Title { get; }
    public string FilePath { get; }
    public DeveloperReviewStatus Status { get; }
    public string Summary { get; }
    public IReadOnlyList<string> RequirementsImplemented { get; }
    public IReadOnlyList<string> FilesCreated { get; }
    public IReadOnlyList<string> FilesModified { get; }
    public IReadOnlyList<string> FilesDeleted { get; }
    public string ArchitectureNotes { get; }
    public IReadOnlyList<string> TestsAdded { get; }
    public DeveloperReviewVerification Verification { get; }
    public string Deviations { get; }
    public string OpenIssues { get; }
    public bool CommitCreated { get; }
    public bool PushPerformed { get; }
}
