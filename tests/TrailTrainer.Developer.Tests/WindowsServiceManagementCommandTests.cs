using TrailTrainer.Developer.Host;

namespace TrailTrainer.Developer.Tests;

public sealed class WindowsServiceManagementCommandTests
{
    [Fact]
    public void NoArguments_FollowsNormalHostPath()
    {
        Assert.False(WindowsServiceManagementCommandDispatcher.HasCommand([]));
    }

    [Theory]
    [InlineData("install", Operation.Install)]
    [InlineData("uninstall", Operation.Uninstall)]
    [InlineData("start", Operation.Start)]
    [InlineData("stop", Operation.Stop)]
    [InlineData("status", Operation.Status)]
    public async Task ManagementCommand_DispatchesExactlyOnce(string command, Operation expected)
    {
        var manager = new RecordingServiceManager { Status = WindowsServiceState.Running };
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);

        var exitCode = await dispatcher.RunAsync(
            [command],
            @"C:\Program Files\TrailTrainer\TrailTrainer.Developer.Host.exe",
            TextWriter.Null,
            TextWriter.Null);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.SuccessExitCode, exitCode);
        Assert.Equal([expected], manager.Operations);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("install", "extra")]
    public async Task InvalidCommandOrArguments_ReturnsInvalidCommandExitCode(params string[] arguments)
    {
        var manager = new RecordingServiceManager();
        var error = new StringWriter();
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);

        var exitCode = await dispatcher.RunAsync(arguments, "host.exe", TextWriter.Null, error);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.InvalidCommandExitCode, exitCode);
        Assert.Empty(manager.Operations);
        Assert.Contains("Usage:", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Install_UsesCurrentExecutablePathAndDoesNotStartService()
    {
        var manager = new RecordingServiceManager();
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);
        const string executablePath = @"C:\Program Files\TrailTrainer\TrailTrainer.Developer.Host.exe";

        var exitCode = await dispatcher.RunAsync(
            ["install"], executablePath, TextWriter.Null, TextWriter.Null);

        Assert.Equal(0, exitCode);
        Assert.Equal(executablePath, manager.InstalledExecutablePath);
        Assert.Equal([Operation.Install], manager.Operations);
    }

    [Fact]
    public async Task OperationFailure_ReturnsFailureExitCodeAndDiagnostic()
    {
        var manager = new RecordingServiceManager { Exception = new InvalidOperationException("SCM denied access.") };
        var error = new StringWriter();
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);

        var exitCode = await dispatcher.RunAsync(["start"], "host.exe", TextWriter.Null, error);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.OperationFailureExitCode, exitCode);
        Assert.Contains("SCM denied access.", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Status_PrintsNormalizedState()
    {
        var manager = new RecordingServiceManager { Status = WindowsServiceState.Paused };
        var output = new StringWriter();
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);

        var exitCode = await dispatcher.RunAsync(["status"], "host.exe", output, TextWriter.Null);

        Assert.Equal(0, exitCode);
        Assert.Equal($"Paused{Environment.NewLine}", output.ToString());
    }

    [Fact]
    public async Task NonWindowsManagement_FailsWithoutExecutingProcess()
    {
        var runner = new RecordingProcessRunner();
        var manager = new ScWindowsServiceManager(runner, new StubPlatform(false));

        await Assert.ThrowsAsync<PlatformNotSupportedException>(() => manager.StartAsync());

        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task Install_UsesStructuredScArgumentsAndStableIdentity()
    {
        var runner = new RecordingProcessRunner(
            new WindowsServiceProcessResult(1060, string.Empty, string.Empty),
            Success());
        var manager = CreateManager(runner);
        var executablePath = Path.GetFullPath(@"C:\Program Files\TrailTrainer\host.exe");

        await manager.InstallAsync(executablePath);

        Assert.Equal(2, runner.Calls.Count);
        Assert.All(runner.Calls, call => Assert.Equal("sc.exe", call.Executable));
        Assert.Equal(
            [
                "create", "TrailTrainer Developer", "binPath=", $"\"{executablePath}\"",
                "start=", "auto", "DisplayName=", "TrailTrainer Developer"
            ],
            runner.Calls[1].Arguments);
        Assert.DoesNotContain(runner.Calls[1].Arguments, argument =>
            argument.Contains("powershell", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Install_ExistingServiceFailsWithoutCreateOrStart()
    {
        var runner = new RecordingProcessRunner(QueryState(4));
        var manager = CreateManager(runner);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.InstallAsync("host.exe"));

        Assert.Contains("already exists", exception.Message, StringComparison.Ordinal);
        Assert.Single(runner.Calls);
    }

    [Fact]
    public async Task Uninstall_AbsentServiceIsDeterministicAndDoesNotDeleteAnything()
    {
        var runner = new RecordingProcessRunner(
            new WindowsServiceProcessResult(1060, string.Empty, string.Empty));
        var manager = CreateManager(runner);

        await manager.UninstallAsync();

        var call = Assert.Single(runner.Calls);
        Assert.Equal(["query", "TrailTrainer Developer"], call.Arguments);
    }

    [Fact]
    public async Task Uninstall_InstalledServiceTargetsStableIdentity()
    {
        var runner = new RecordingProcessRunner(QueryState(1), Success());
        var manager = CreateManager(runner);

        await manager.UninstallAsync();

        Assert.Equal(["delete", "TrailTrainer Developer"], runner.Calls[1].Arguments);
    }

    [Theory]
    [InlineData(true, "start")]
    [InlineData(false, "stop")]
    public async Task StartAndStop_TargetStableIdentityOnce(bool start, string command)
    {
        var runner = new RecordingProcessRunner(Success());
        var manager = CreateManager(runner);

        if (start)
        {
            await manager.StartAsync();
        }
        else
        {
            await manager.StopAsync();
        }

        var call = Assert.Single(runner.Calls);
        Assert.Equal([command, "TrailTrainer Developer"], call.Arguments);
    }

    [Fact]
    public async Task ScFailure_ContainsExitCodeAndCapturedDiagnostic()
    {
        var runner = new RecordingProcessRunner(
            new WindowsServiceProcessResult(5, "output", "access denied"));
        var manager = CreateManager(runner);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StopAsync());

        Assert.Contains("exit code 5", exception.Message, StringComparison.Ordinal);
        Assert.Contains("access denied", exception.Message, StringComparison.Ordinal);
        Assert.Single(runner.Calls);
    }

    [Theory]
    [InlineData(1, WindowsServiceState.Stopped)]
    [InlineData(2, WindowsServiceState.StartPending)]
    [InlineData(3, WindowsServiceState.StopPending)]
    [InlineData(4, WindowsServiceState.Running)]
    [InlineData(7, WindowsServiceState.Paused)]
    [InlineData(5, WindowsServiceState.Unknown)]
    public async Task Status_MapsNumericScmState(int nativeState, WindowsServiceState expected)
    {
        var runner = new RecordingProcessRunner(QueryState(nativeState));

        var status = await CreateManager(runner).GetStatusAsync();

        Assert.Equal(expected, status);
    }

    [Fact]
    public async Task Status_AbsentServiceMapsToNotInstalled()
    {
        var runner = new RecordingProcessRunner(
            new WindowsServiceProcessResult(1060, string.Empty, "localized diagnostic"));

        var status = await CreateManager(runner).GetStatusAsync();

        Assert.Equal(WindowsServiceState.NotInstalled, status);
    }

    private static ScWindowsServiceManager CreateManager(IWindowsServiceProcessRunner runner) =>
        new(runner, new StubPlatform(true));

    private static WindowsServiceProcessResult Success() => new(0, string.Empty, string.Empty);

    private static WindowsServiceProcessResult QueryState(int state) => new(
        0,
        $"SERVICE_NAME: TrailTrainer Developer{Environment.NewLine}" +
        $"        TYPE               : 10  WIN32_OWN_PROCESS{Environment.NewLine}" +
        $"        localized-label    : {state}  localized-state{Environment.NewLine}",
        string.Empty);

    public enum Operation
    {
        Install,
        Uninstall,
        Start,
        Stop,
        Status
    }

    private sealed class RecordingServiceManager : IWindowsServiceManager
    {
        public List<Operation> Operations { get; } = [];
        public string? InstalledExecutablePath { get; private set; }
        public WindowsServiceState Status { get; init; }
        public Exception? Exception { get; init; }

        public Task InstallAsync(string executablePath, CancellationToken cancellationToken = default)
        {
            InstalledExecutablePath = executablePath;
            return Record(Operation.Install);
        }

        public Task UninstallAsync(CancellationToken cancellationToken = default) => Record(Operation.Uninstall);
        public Task StartAsync(CancellationToken cancellationToken = default) => Record(Operation.Start);
        public Task StopAsync(CancellationToken cancellationToken = default) => Record(Operation.Stop);

        public Task<WindowsServiceState> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            Operations.Add(Operation.Status);
            return Exception is null
                ? Task.FromResult(Status)
                : Task.FromException<WindowsServiceState>(Exception);
        }

        private Task Record(Operation operation)
        {
            Operations.Add(operation);
            return Exception is null ? Task.CompletedTask : Task.FromException(Exception);
        }
    }

    private sealed record StubPlatform(bool IsWindows) : IWindowsPlatform;

    private sealed class RecordingProcessRunner : IWindowsServiceProcessRunner
    {
        private readonly Queue<WindowsServiceProcessResult> results;

        public RecordingProcessRunner(params WindowsServiceProcessResult[] results)
        {
            this.results = new Queue<WindowsServiceProcessResult>(results);
        }

        public List<ProcessCall> Calls { get; } = [];

        public Task<WindowsServiceProcessResult> RunAsync(
            string executable,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new ProcessCall(executable, arguments.ToArray()));
            return Task.FromResult(results.Dequeue());
        }
    }

    private sealed record ProcessCall(string Executable, IReadOnlyList<string> Arguments);
}
