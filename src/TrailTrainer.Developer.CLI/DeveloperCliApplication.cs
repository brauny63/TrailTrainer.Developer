using System.Text.RegularExpressions;
using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.CLI;

public sealed partial class DeveloperCliApplication
{
    private readonly IGitRepositoryStatusProvider repositoryStatusProvider;
    private readonly IDeveloperTaskDiscovery taskDiscovery;
    private readonly IDeveloperTaskParser taskParser;
    private readonly IDeveloperTaskStarter taskStarter;
    private readonly IDeveloperTaskCompleter taskCompleter;

    public DeveloperCliApplication(
        IGitRepositoryStatusProvider repositoryStatusProvider,
        IDeveloperTaskDiscovery taskDiscovery,
        IDeveloperTaskParser taskParser,
        IDeveloperTaskStarter taskStarter,
        IDeveloperTaskCompleter taskCompleter)
    {
        this.repositoryStatusProvider = repositoryStatusProvider
            ?? throw new ArgumentNullException(nameof(repositoryStatusProvider));
        this.taskDiscovery = taskDiscovery ?? throw new ArgumentNullException(nameof(taskDiscovery));
        this.taskParser = taskParser ?? throw new ArgumentNullException(nameof(taskParser));
        this.taskStarter = taskStarter ?? throw new ArgumentNullException(nameof(taskStarter));
        this.taskCompleter = taskCompleter ?? throw new ArgumentNullException(nameof(taskCompleter));
    }

    public async Task<int> RunAsync(
        string[] arguments,
        string currentWorkingDirectory,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentWorkingDirectory);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            return await RunCoreAsync(
                arguments,
                currentWorkingDirectory,
                output,
                cancellationToken);
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync(exception.Message);
            return 1;
        }
    }

    private async Task<int> RunCoreAsync(
        string[] arguments,
        string currentWorkingDirectory,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        if (arguments.Length < 2 || !EqualsIgnoreCase(arguments[0], "tasks"))
        {
            throw Usage("Expected a command: tasks list|show|start|complete.");
        }

        var command = arguments[1];
        if (!EqualsIgnoreCase(command, "list") &&
            !EqualsIgnoreCase(command, "show") &&
            !EqualsIgnoreCase(command, "start") &&
            !EqualsIgnoreCase(command, "complete"))
        {
            throw Usage($"Unknown command 'tasks {command}'.");
        }

        var repositoryStatus = await repositoryStatusProvider.GetStatusAsync(
            currentWorkingDirectory,
            cancellationToken);
        if (!repositoryStatus.IsRepository || repositoryStatus.RepositoryRoot is null)
        {
            throw new InvalidOperationException(
                $"Directory '{Path.GetFullPath(currentWorkingDirectory)}' is not inside a Git working tree.");
        }

        var repositoryRoot = repositoryStatus.RepositoryRoot;
        var repositoryName = new DirectoryInfo(repositoryRoot).Name;
        var tasks = await taskDiscovery.DiscoverAsync(repositoryRoot, cancellationToken);

        if (EqualsIgnoreCase(command, "list"))
        {
            RequireArgumentCount(arguments, 2, "tasks list does not accept additional arguments.");
            if (tasks.Count == 0)
            {
                await output.WriteLineAsync("No Developer Tasks were found.");
                return 0;
            }

            foreach (var task in tasks)
            {
                await output.WriteLineAsync($"{task.Id}  {task.FileName}");
            }

            return 0;
        }

        if (arguments.Length < 3)
        {
            throw Usage($"tasks {command} requires a task ID or filename.");
        }

        var descriptor = ResolveTask(arguments[2], tasks);
        if (EqualsIgnoreCase(command, "show"))
        {
            RequireArgumentCount(arguments, 3, "tasks show does not accept additional arguments.");
            var document = await taskParser.ParseAsync(descriptor.FilePath, cancellationToken);
            await WriteTaskDocumentAsync(output, document);
            return 0;
        }

        if (EqualsIgnoreCase(command, "start"))
        {
            RequireArgumentCount(arguments, 3, "tasks start does not accept additional arguments.");
            var result = await taskStarter.StartAsync(
                descriptor.FilePath,
                repositoryRoot,
                repositoryName,
                cancellationToken);
            await WriteStartResultAsync(output, result);
            return 0;
        }

        var options = ParseCompleteOptions(arguments[3..]);
        var completion = await taskCompleter.CompleteAsync(
            descriptor.FilePath,
            repositoryRoot,
            repositoryName,
            options.Message,
            options.RemoteName,
            options.SetUpstream,
            cancellationToken);
        await WriteCompletionResultAsync(output, completion);
        return 0;
    }

    private static DeveloperTaskDescriptor ResolveTask(
        string taskArgument,
        IReadOnlyList<DeveloperTaskDescriptor> tasks)
    {
        IEnumerable<DeveloperTaskDescriptor> matches;
        var idMatch = CanonicalTaskIdPattern().Match(taskArgument);
        if (idMatch.Success)
        {
            var id = new DeveloperTaskId(int.Parse(idMatch.Groups[1].Value));
            matches = tasks.Where(task => task.Id == id);
        }
        else
        {
            matches = tasks.Where(task => string.Equals(
                task.FileName,
                taskArgument,
                StringComparison.Ordinal));
        }

        var resolved = matches.Take(2).ToArray();
        return resolved.Length switch
        {
            0 => throw new InvalidOperationException($"No Developer Task matches '{taskArgument}'."),
            1 => resolved[0],
            _ => throw new InvalidOperationException($"Developer Task identity '{taskArgument}' is ambiguous.")
        };
    }

    private static CompleteOptions ParseCompleteOptions(string[] arguments)
    {
        string? message = null;
        var remoteName = "origin";
        var remoteSpecified = false;
        var setUpstream = true;
        var setUpstreamSpecified = false;

        for (var index = 0; index < arguments.Length; index++)
        {
            var option = arguments[index];
            if (EqualsIgnoreCase(option, "--message"))
            {
                if (message is not null)
                {
                    throw Usage("Option '--message' may only be specified once.");
                }

                message = ReadOptionValue(arguments, ref index, "--message");
            }
            else if (EqualsIgnoreCase(option, "--remote"))
            {
                if (remoteSpecified)
                {
                    throw Usage("Option '--remote' may only be specified once.");
                }

                remoteName = ReadOptionValue(arguments, ref index, "--remote");
                remoteSpecified = true;
            }
            else if (EqualsIgnoreCase(option, "--set-upstream"))
            {
                if (setUpstreamSpecified)
                {
                    throw Usage("Option '--set-upstream' may only be specified once.");
                }

                setUpstream = true;
                setUpstreamSpecified = true;
            }
            else
            {
                throw Usage($"Unknown option '{option}'.");
            }
        }

        if (message is null)
        {
            throw Usage("Option '--message <commit-message>' is required for tasks complete.");
        }

        return new CompleteOptions(message, remoteName, setUpstream);
    }

    private static string ReadOptionValue(string[] arguments, ref int index, string option)
    {
        if (index + 1 >= arguments.Length || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw Usage($"Option '{option}' requires a value.");
        }

        index++;
        return arguments[index];
    }

    private static void RequireArgumentCount(string[] arguments, int count, string diagnostic)
    {
        if (arguments.Length != count)
        {
            throw Usage(diagnostic);
        }
    }

    private static async Task WriteTaskDocumentAsync(TextWriter output, DeveloperTaskDocument document)
    {
        await output.WriteLineAsync($"Task: {document.Id}  {document.Title}");
        await output.WriteLineAsync($"Repository: {document.Repository}");
        await output.WriteLineAsync($"Expected branch: {document.ExpectedBranch}");
        await output.WriteLineAsync($"Review report: {document.ReviewReportPath}");
        await output.WriteLineAsync($"Task file: {document.FilePath}");
    }

    private static async Task WriteStartResultAsync(TextWriter output, DeveloperTaskStartResult result)
    {
        await output.WriteLineAsync($"Task: {result.TaskId}  {result.TaskTitle}");
        await output.WriteLineAsync($"Repository root: {result.RepositoryRoot}");
        await output.WriteLineAsync($"Previous branch: {result.PreviousBranch}");
        await output.WriteLineAsync($"Created branch: {result.CreatedBranch}");
        await output.WriteLineAsync($"Task file: {result.TaskFilePath}");
        await output.WriteLineAsync($"Review report: {result.ReviewReportPath}");
    }

    private static async Task WriteCompletionResultAsync(TextWriter output, DeveloperTaskCompletionResult result)
    {
        await output.WriteLineAsync($"Task: {result.TaskId}  {result.TaskTitle}");
        await output.WriteLineAsync($"Repository root: {result.RepositoryRoot}");
        await output.WriteLineAsync($"Branch: {result.BranchName}");
        await output.WriteLineAsync($"Commit: {result.CommitSha}  {result.CommitMessage}");
        await output.WriteLineAsync($"Remote: {result.RemoteName}  Set upstream: {result.SetUpstream}");
        await output.WriteLineAsync($"Task file: {result.TaskFilePath}");
        await output.WriteLineAsync($"Review report: {result.ReviewReportPath}");
    }

    private static bool EqualsIgnoreCase(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static ArgumentException Usage(string diagnostic) => new(diagnostic);

    private sealed record CompleteOptions(string Message, string RemoteName, bool SetUpstream);

    [GeneratedRegex(@"^DEV-(\d{4})$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalTaskIdPattern();
}
