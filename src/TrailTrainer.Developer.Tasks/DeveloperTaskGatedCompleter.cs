using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class DeveloperTaskGatedCompleter : IDeveloperTaskGatedCompleter
{
    private readonly IDeveloperTaskParser taskParser;
    private readonly IDeveloperReviewParser reviewParser;
    private readonly IDeveloperReviewValidator reviewValidator;
    private readonly IDeveloperTaskCompleter taskCompleter;

    public DeveloperTaskGatedCompleter(
        IDeveloperTaskParser taskParser,
        IDeveloperReviewParser reviewParser,
        IDeveloperReviewValidator reviewValidator,
        IDeveloperTaskCompleter taskCompleter)
    {
        this.taskParser = taskParser ?? throw new ArgumentNullException(nameof(taskParser));
        this.reviewParser = reviewParser ?? throw new ArgumentNullException(nameof(reviewParser));
        this.reviewValidator = reviewValidator ?? throw new ArgumentNullException(nameof(reviewValidator));
        this.taskCompleter = taskCompleter ?? throw new ArgumentNullException(nameof(taskCompleter));
    }

    public async Task<DeveloperTaskGatedCompletionResult> CompleteAsync(
        string developerTaskFilePath,
        string repositoryDirectoryPath,
        string expectedRepositoryName,
        string commitMessage,
        string remoteName,
        bool setUpstream,
        CancellationToken cancellationToken = default)
    {
        var task = await taskParser.ParseAsync(developerTaskFilePath, cancellationToken);
        var reviewFilePath = ResolveReviewFilePath(task, repositoryDirectoryPath);
        var review = await reviewParser.ParseAsync(reviewFilePath, cancellationToken);
        var validation = await reviewValidator.ValidateAsync(task, review, cancellationToken);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                "Developer Task completion is blocked by review validation errors: " +
                string.Join("; ", validation.Errors));
        }

        var completion = await taskCompleter.CompleteAsync(
            developerTaskFilePath,
            repositoryDirectoryPath,
            expectedRepositoryName,
            commitMessage,
            remoteName,
            setUpstream,
            cancellationToken);

        return new DeveloperTaskGatedCompletionResult(task.Id, validation, completion);
    }

    private static string ResolveReviewFilePath(
        DeveloperTaskDocument task,
        string repositoryDirectoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryDirectoryPath);
        if (Path.IsPathRooted(task.ReviewReportPath))
        {
            throw new InvalidOperationException("The task Review report path must be repository-relative.");
        }

        var taskFilePath = Path.GetFullPath(task.FilePath);
        var taskDirectory = Directory.GetParent(taskFilePath);
        var docsDirectory = taskDirectory?.Parent;
        var repositoryDirectory = docsDirectory?.Parent;
        if (taskDirectory is null || docsDirectory is null || repositoryDirectory is null ||
            !PathNameEquals(taskDirectory.Name, "developer-tasks") ||
            !PathNameEquals(docsDirectory.Name, "docs"))
        {
            throw new InvalidOperationException(
                $"Task file '{taskFilePath}' is not located under '<repository>/docs/developer-tasks'.");
        }

        var repositoryRoot = Path.GetFullPath(repositoryDirectory.FullName);
        var suppliedDirectory = Path.GetFullPath(repositoryDirectoryPath);
        if (!IsWithin(repositoryRoot, suppliedDirectory))
        {
            throw new InvalidOperationException(
                $"Supplied repository directory '{suppliedDirectory}' is outside task repository '{repositoryRoot}'.");
        }

        var reviewFilePath = Path.GetFullPath(Path.Combine(repositoryRoot, task.ReviewReportPath));
        if (!IsWithin(repositoryRoot, reviewFilePath))
        {
            throw new InvalidOperationException(
                $"Review report path '{task.ReviewReportPath}' resolves outside repository '{repositoryRoot}'.");
        }

        return reviewFilePath;
    }

    private static bool IsWithin(string rootPath, string candidatePath)
    {
        var relativePath = Path.GetRelativePath(rootPath, candidatePath);
        return !Path.IsPathRooted(relativePath) &&
               !string.Equals(relativePath, "..", StringComparison.Ordinal) &&
               !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
               !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static bool PathNameEquals(string left, string right) =>
        string.Equals(
            left,
            right,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
