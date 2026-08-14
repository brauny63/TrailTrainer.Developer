using TrailTrainer.Developer.CLI;
using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tests;

public sealed class DeveloperCliApplicationTests
{
    [Theory]
    [InlineData("DEV-0009")]
    [InlineData("DEV-0009-Developer-Task-CLI.md")]
    public async Task Show_ResolvesCanonicalIdOrExactFilename(string taskArgument)
    {
        var fixture = new CliFixture();

        var result = await fixture.RunAsync("tasks", "show", taskArgument);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(fixture.Descriptor.FilePath, fixture.Parser.FilePath);
    }

    [Theory]
    [InlineData("DEV-9999")]
    [InlineData("0009")]
    [InlineData("Developer-Task-CLI")]
    public async Task Show_MissingOrFuzzyTask_ReturnsFailure(string taskArgument)
    {
        var fixture = new CliFixture();

        var result = await fixture.RunAsync("tasks", "show", taskArgument);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("No Developer Task", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Show_AmbiguousCanonicalIdentity_ReturnsFailure()
    {
        var fixture = new CliFixture();
        fixture.Discovery.Results =
        [
            fixture.Descriptor,
            fixture.Descriptor with { FileName = "DEV-0009-Other.md", FilePath = "C:\\tasks\\DEV-0009-Other.md" }
        ];

        var result = await fixture.RunAsync("tasks", "show", "DEV-0009");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("ambiguous", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task List_PrintsTasksInDiscoveryOrderWithoutParsing()
    {
        var fixture = new CliFixture();
        fixture.Discovery.Results =
        [
            fixture.Descriptor with { Id = new DeveloperTaskId(2), FileName = "DEV-0002-Second.md" },
            fixture.Descriptor with { Id = new DeveloperTaskId(1), FileName = "DEV-0001-First.md" }
        ];

        var result = await fixture.RunAsync("tasks", "list");

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Output.IndexOf("DEV-0002", StringComparison.Ordinal) <
                    result.Output.IndexOf("DEV-0001", StringComparison.Ordinal));
        Assert.Equal(0, fixture.Parser.CallCount);
    }

    [Fact]
    public async Task List_EmptyDiscovery_PrintsClearMessageAndSucceeds()
    {
        var fixture = new CliFixture();
        fixture.Discovery.Results = [];

        var result = await fixture.RunAsync("tasks", "list");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("No Developer Tasks", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Show_PrintsRequiredFieldsWithoutInvokingWorkflows()
    {
        var fixture = new CliFixture();

        var result = await fixture.RunAsync("tasks", "show", "DEV-0009");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("DEV-0009", result.Output, StringComparison.Ordinal);
        Assert.Contains(fixture.Document.Title, result.Output, StringComparison.Ordinal);
        Assert.Contains(fixture.Document.Repository, result.Output, StringComparison.Ordinal);
        Assert.Contains(fixture.Document.ExpectedBranch, result.Output, StringComparison.Ordinal);
        Assert.Contains(fixture.Document.ReviewReportPath, result.Output, StringComparison.Ordinal);
        Assert.Contains(fixture.Document.FilePath, result.Output, StringComparison.Ordinal);
        Assert.Equal(0, fixture.Starter.CallCount);
        Assert.Equal(0, fixture.Completer.CallCount);
    }

    [Fact]
    public async Task Start_DelegatesOnceWithResolvedValuesAndPrintsResult()
    {
        var fixture = new CliFixture();

        var result = await fixture.RunAsync("tasks", "start", "DEV-0009");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, fixture.Starter.CallCount);
        Assert.Equal(fixture.Descriptor.FilePath, fixture.Starter.TaskFilePath);
        Assert.Equal(fixture.RepositoryRoot, fixture.Starter.RepositoryPath);
        Assert.Equal("TrailTrainer.Developer", fixture.Starter.RepositoryName);
        Assert.Contains("main", result.Output, StringComparison.Ordinal);
        Assert.Contains(fixture.Document.ExpectedBranch, result.Output, StringComparison.Ordinal);
        Assert.Contains(fixture.Document.ReviewReportPath, result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Complete_DefaultsRemoteAndUpstreamAndPassesExactMessage()
    {
        var fixture = new CliFixture();
        const string message = "feat: exact Message Value";

        var result = await fixture.RunAsync(
            "tasks", "complete", "DEV-0009", "--message", message);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, fixture.Completer.CallCount);
        Assert.Equal(message, fixture.Completer.CommitMessage);
        Assert.Equal("origin", fixture.Completer.RemoteName);
        Assert.True(fixture.Completer.SetUpstream);
        Assert.Contains("commit-sha", result.Output, StringComparison.Ordinal);
        Assert.Contains(fixture.Document.ExpectedBranch, result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Complete_ExplicitRemoteIsPassedUnchanged()
    {
        var fixture = new CliFixture();

        var result = await fixture.RunAsync(
            "TASKS", "COMPLETE", "DEV-0009", "--MESSAGE", "message", "--REMOTE", "Exact-Remote");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Exact-Remote", fixture.Completer.RemoteName);
    }

    public static TheoryData<string[]> InvalidUsageArguments => new()
    {
        new[] { "unknown" },
        new[] { "tasks", "unknown" },
        new[] { "tasks", "complete", "DEV-0009" },
        new[] { "tasks", "complete", "DEV-0009", "--unknown" },
        new[] { "tasks", "complete", "DEV-0009", "--message" },
        new[] { "tasks", "complete", "DEV-0009", "--message", "--remote", "origin" },
        new[] { "tasks", "complete", "DEV-0009", "--message", "one", "--message", "two" },
        new[] { "tasks", "complete", "DEV-0009", "--message", "one", "--remote", "a", "--remote", "b" },
        new[] { "tasks", "complete", "DEV-0009", "--message", "one", "--set-upstream", "--set-upstream" }
    };

    [Theory]
    [MemberData(nameof(InvalidUsageArguments))]
    public async Task InvalidUsage_ReturnsNonZeroAndWritesError(string[] arguments)
    {
        var fixture = new CliFixture();

        var result = await fixture.RunAsync(arguments);

        Assert.NotEqual(0, result.ExitCode);
        Assert.NotEmpty(result.Error);
        Assert.Equal(0, fixture.Completer.CallCount);
    }

    [Fact]
    public async Task WorkflowFailure_ReturnsNonZeroWritesOnlyErrorAndNoSuccessOutput()
    {
        var fixture = new CliFixture();
        fixture.Starter.Exception = new InvalidOperationException("Workflow failed");

        var result = await fixture.RunAsync("tasks", "start", "DEV-0009");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains("Workflow failed", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("System.", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Complete_PropagatesCancellationToDependencies()
    {
        var fixture = new CliFixture();
        using var source = new CancellationTokenSource();

        var result = await fixture.RunAsync(
            source.Token,
            "tasks", "complete", "DEV-0009", "--message", "message");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(source.Token, fixture.Status.CancellationToken);
        Assert.Equal(source.Token, fixture.Discovery.CancellationToken);
        Assert.Equal(source.Token, fixture.Completer.CancellationToken);
    }

    private sealed class CliFixture
    {
        public CliFixture()
        {
            RepositoryRoot = Path.GetFullPath(Path.Combine("test-root", "TrailTrainer.Developer"));
            Descriptor = new DeveloperTaskDescriptor(
                new DeveloperTaskId(9),
                Path.Combine(RepositoryRoot, "docs", "developer-tasks", "DEV-0009-Developer-Task-CLI.md"),
                "DEV-0009-Developer-Task-CLI.md");
            Document = new DeveloperTaskDocument(
                Descriptor.Id,
                "Developer Task CLI",
                Descriptor.FilePath,
                "TrailTrainer.Developer",
                "feature/dev-0009-developer-task-cli",
                "docs/developer-reviews/REVIEW-0009.md");
            Status = new FakeStatusProvider(new GitRepositoryStatus(true, RepositoryRoot, "main", false));
            Discovery = new FakeDiscovery([Descriptor]);
            Parser = new FakeParser(Document);
            Starter = new FakeStarter(new DeveloperTaskStartResult(
                Document.Id, Document.Title, RepositoryRoot, "main", Document.ExpectedBranch,
                Document.FilePath, Document.ReviewReportPath));
            Completer = new FakeCompleter(new DeveloperTaskCompletionResult(
                Document.Id, Document.Title, RepositoryRoot, Document.ExpectedBranch,
                "commit-sha", "message", "origin", true, Document.FilePath, Document.ReviewReportPath));
            Application = new DeveloperCliApplication(Status, Discovery, Parser, Starter, Completer);
        }

        public string RepositoryRoot { get; }
        public DeveloperTaskDescriptor Descriptor { get; }
        public DeveloperTaskDocument Document { get; }
        public FakeStatusProvider Status { get; }
        public FakeDiscovery Discovery { get; }
        public FakeParser Parser { get; }
        public FakeStarter Starter { get; }
        public FakeCompleter Completer { get; }
        public DeveloperCliApplication Application { get; }

        public Task<CliResult> RunAsync(params string[] arguments) =>
            RunAsync(CancellationToken.None, arguments);

        public async Task<CliResult> RunAsync(CancellationToken token, params string[] arguments)
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            var exitCode = await Application.RunAsync(
                arguments, RepositoryRoot, output, error, token);
            return new CliResult(exitCode, output.ToString(), error.ToString());
        }
    }

    private sealed record CliResult(int ExitCode, string Output, string Error);

    private sealed class FakeStatusProvider(GitRepositoryStatus result) : IGitRepositoryStatusProvider
    {
        public CancellationToken CancellationToken { get; private set; }
        public Task<GitRepositoryStatus> GetStatusAsync(string path, CancellationToken token = default)
        { CancellationToken = token; return Task.FromResult(result); }
    }

    private sealed class FakeDiscovery(IReadOnlyList<DeveloperTaskDescriptor> results) : IDeveloperTaskDiscovery
    {
        public IReadOnlyList<DeveloperTaskDescriptor> Results { get; set; } = results;
        public CancellationToken CancellationToken { get; private set; }
        public Task<IReadOnlyList<DeveloperTaskDescriptor>> DiscoverAsync(string path, CancellationToken token = default)
        { CancellationToken = token; return Task.FromResult(Results); }
    }

    private sealed class FakeParser(DeveloperTaskDocument result) : IDeveloperTaskParser
    {
        public int CallCount { get; private set; }
        public string? FilePath { get; private set; }
        public Task<DeveloperTaskDocument> ParseAsync(string path, CancellationToken token = default)
        { CallCount++; FilePath = path; return Task.FromResult(result); }
    }

    private sealed class FakeStarter(DeveloperTaskStartResult result) : IDeveloperTaskStarter
    {
        public Exception? Exception { get; set; }
        public int CallCount { get; private set; }
        public string? TaskFilePath { get; private set; }
        public string? RepositoryPath { get; private set; }
        public string? RepositoryName { get; private set; }
        public Task<DeveloperTaskStartResult> StartAsync(
            string taskPath, string repositoryPath, string repositoryName, CancellationToken token = default)
        {
            CallCount++; TaskFilePath = taskPath; RepositoryPath = repositoryPath; RepositoryName = repositoryName;
            return Exception is null ? Task.FromResult(result) : Task.FromException<DeveloperTaskStartResult>(Exception);
        }
    }

    private sealed class FakeCompleter(DeveloperTaskCompletionResult result) : IDeveloperTaskCompleter
    {
        public int CallCount { get; private set; }
        public string? CommitMessage { get; private set; }
        public string? RemoteName { get; private set; }
        public bool SetUpstream { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public Task<DeveloperTaskCompletionResult> CompleteAsync(
            string taskPath, string repositoryPath, string repositoryName, string message,
            string remoteName, bool setUpstream, CancellationToken token = default)
        {
            CallCount++; CommitMessage = message; RemoteName = remoteName; SetUpstream = setUpstream;
            CancellationToken = token; return Task.FromResult(result);
        }
    }
}
