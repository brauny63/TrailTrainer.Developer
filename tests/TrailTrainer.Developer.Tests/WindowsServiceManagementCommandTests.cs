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
    [InlineData("recovery", Operation.Recovery)]
    [InlineData("delayed-start", Operation.DelayedStart)]
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

    [Fact]
    public async Task Recovery_UsesExactRestartPolicyAndNonCrashFlag()
    {
        var runner = new RecordingProcessRunner(QueryState(1), Success(), Success());
        var manager = CreateManager(runner);

        await manager.ConfigureRecoveryAsync();

        Assert.Equal(3, runner.Calls.Count);
        Assert.All(runner.Calls, call => Assert.Equal("sc.exe", call.Executable));
        Assert.Equal(["query", "TrailTrainer Developer"], runner.Calls[0].Arguments);
        Assert.Equal(
            [
                "failure", "TrailTrainer Developer",
                "reset=", "86400",
                "actions=", "restart/60000/restart/60000/restart/60000"
            ],
            runner.Calls[1].Arguments);
        Assert.Equal(
            ["failureflag", "TrailTrainer Developer", "1"],
            runner.Calls[2].Arguments);
    }

    [Fact]
    public async Task Recovery_MissingServiceFailsWithoutConfigurationAttempt()
    {
        var runner = new RecordingProcessRunner(
            new WindowsServiceProcessResult(1060, string.Empty, string.Empty));
        var manager = CreateManager(runner);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.ConfigureRecoveryAsync());

        Assert.Contains("is not installed", exception.Message, StringComparison.Ordinal);
        var call = Assert.Single(runner.Calls);
        Assert.Equal(["query", "TrailTrainer Developer"], call.Arguments);
    }

    [Fact]
    public async Task Recovery_NonWindowsFailsBeforeProcessExecution()
    {
        var runner = new RecordingProcessRunner();
        var manager = new ScWindowsServiceManager(runner, new StubPlatform(false));

        await Assert.ThrowsAsync<PlatformNotSupportedException>(() =>
            manager.ConfigureRecoveryAsync());

        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task Recovery_ScmPolicyFailureIsSurfacedWithoutRetryOrFlagAttempt()
    {
        var runner = new RecordingProcessRunner(
            QueryState(4),
            new WindowsServiceProcessResult(5, string.Empty, "access denied"));
        var manager = CreateManager(runner);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.ConfigureRecoveryAsync());

        Assert.Contains("exit code 5", exception.Message, StringComparison.Ordinal);
        Assert.Contains("access denied", exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, runner.Calls.Count);
    }

    [Fact]
    public async Task Recovery_NonCrashFlagFailureIsSurfacedWithoutRetry()
    {
        var runner = new RecordingProcessRunner(
            QueryState(4),
            Success(),
            new WindowsServiceProcessResult(87, string.Empty, "unsupported"));
        var manager = CreateManager(runner);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.ConfigureRecoveryAsync());

        Assert.Contains("exit code 87", exception.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported", exception.Message, StringComparison.Ordinal);
        Assert.Equal(3, runner.Calls.Count);
    }

    [Fact]
    public async Task DelayedStart_ChecksExistenceAndConfiguresAutomaticDelayedModeOnly()
    {
        var runner = new RecordingProcessRunner(QueryState(1), Success());
        var manager = CreateManager(runner);

        await manager.ConfigureDelayedStartAsync();

        Assert.Equal(2, runner.Calls.Count);
        Assert.All(runner.Calls, call => Assert.Equal("sc.exe", call.Executable));
        Assert.Equal(["query", "TrailTrainer Developer"], runner.Calls[0].Arguments);
        Assert.Equal(
            ["config", "TrailTrainer Developer", "start=", "delayed-auto"],
            runner.Calls[1].Arguments);
        Assert.DoesNotContain(runner.Calls, call =>
            call.Arguments[0] is "start" or "stop");
    }

    [Fact]
    public async Task DelayedStart_MissingServiceFailsWithoutConfigurationAttempt()
    {
        var runner = new RecordingProcessRunner(
            new WindowsServiceProcessResult(1060, string.Empty, string.Empty));
        var manager = CreateManager(runner);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.ConfigureDelayedStartAsync());

        Assert.Contains("is not installed", exception.Message, StringComparison.Ordinal);
        var call = Assert.Single(runner.Calls);
        Assert.Equal(["query", "TrailTrainer Developer"], call.Arguments);
    }

    [Fact]
    public async Task DelayedStart_NonWindowsFailsBeforeProcessExecution()
    {
        var runner = new RecordingProcessRunner();
        var manager = new ScWindowsServiceManager(runner, new StubPlatform(false));

        await Assert.ThrowsAsync<PlatformNotSupportedException>(() =>
            manager.ConfigureDelayedStartAsync());

        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task DelayedStart_ScmFailureIsSurfacedWithoutRetry()
    {
        var runner = new RecordingProcessRunner(
            QueryState(4),
            new WindowsServiceProcessResult(5, string.Empty, "access denied"));
        var manager = CreateManager(runner);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.ConfigureDelayedStartAsync());

        Assert.Contains("exit code 5", exception.Message, StringComparison.Ordinal);
        Assert.Contains("access denied", exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, runner.Calls.Count);
    }

    [Fact]
    public async Task Setup_AppliesDelayedStartThenRecoveryExactlyOnce()
    {
        var manager = new RecordingServiceManager();
        var output = new StringWriter();
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);

        var exitCode = await dispatcher.RunAsync(["setup"], "host.exe", output, TextWriter.Null);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.SuccessExitCode, exitCode);
        Assert.Equal([Operation.DelayedStart, Operation.Recovery], manager.Operations);
        Assert.DoesNotContain(Operation.Install, manager.Operations);
        Assert.DoesNotContain(Operation.Start, manager.Operations);
        Assert.DoesNotContain(Operation.Stop, manager.Operations);
        Assert.Contains("operational setup configured", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Setup_DelayedStartFailureStopsImmediatelyAndSurfacesOriginalFailure()
    {
        var failure = new IOException("delayed-start failed");
        var manager = new RecordingServiceManager
        {
            FailureOperation = Operation.DelayedStart,
            Exception = failure
        };
        var error = new StringWriter();
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);

        var exitCode = await dispatcher.RunAsync(["setup"], "host.exe", TextWriter.Null, error);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.OperationFailureExitCode, exitCode);
        Assert.Equal([Operation.DelayedStart], manager.Operations);
        Assert.Contains(failure.Message, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Setup_RecoveryFailureIsSurfacedWithoutRollbackOrRetry()
    {
        var failure = new IOException("recovery failed");
        var manager = new RecordingServiceManager
        {
            FailureOperation = Operation.Recovery,
            Exception = failure
        };
        var error = new StringWriter();
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);

        var exitCode = await dispatcher.RunAsync(["setup"], "host.exe", TextWriter.Null, error);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.OperationFailureExitCode, exitCode);
        Assert.Equal([Operation.DelayedStart, Operation.Recovery], manager.Operations);
        Assert.Contains(failure.Message, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Setup_InvalidArgumentsReturnInvalidCommandExitCodeWithoutOperations()
    {
        var manager = new RecordingServiceManager();
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);

        var exitCode = await dispatcher.RunAsync(
            ["setup", "extra"], "host.exe", TextWriter.Null, TextWriter.Null);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.InvalidCommandExitCode, exitCode);
        Assert.Empty(manager.Operations);
    }

    [Fact]
    public async Task Setup_WithProductionManagerReusesExistingScmOperationsInOrder()
    {
        var runner = new RecordingProcessRunner(
            QueryState(1),
            Success(),
            QueryState(1),
            Success(),
            Success());
        var dispatcher = new WindowsServiceManagementCommandDispatcher(CreateManager(runner));

        var exitCode = await dispatcher.RunAsync(
            ["setup"], "host.exe", TextWriter.Null, TextWriter.Null);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.SuccessExitCode, exitCode);
        Assert.Equal(
            [
                "query",
                "config",
                "query",
                "failure",
                "failureflag"
            ],
            runner.Calls.Select(call => call.Arguments[0]));
        Assert.DoesNotContain(runner.Calls, call =>
            call.Arguments[0] is "create" or "start" or "stop");
    }

    [Fact]
    public async Task Setup_MissingServiceFailsBeforeAnyConfiguration()
    {
        var runner = new RecordingProcessRunner(
            new WindowsServiceProcessResult(1060, string.Empty, string.Empty));
        var dispatcher = new WindowsServiceManagementCommandDispatcher(CreateManager(runner));

        var exitCode = await dispatcher.RunAsync(
            ["setup"], "host.exe", TextWriter.Null, TextWriter.Null);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.OperationFailureExitCode, exitCode);
        var call = Assert.Single(runner.Calls);
        Assert.Equal(["query", "TrailTrainer Developer"], call.Arguments);
    }

    [Fact]
    public async Task Setup_NonWindowsFailsBeforeProcessExecution()
    {
        var runner = new RecordingProcessRunner();
        var manager = new ScWindowsServiceManager(runner, new StubPlatform(false));
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);

        var exitCode = await dispatcher.RunAsync(
            ["setup"], "host.exe", TextWriter.Null, TextWriter.Null);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.OperationFailureExitCode, exitCode);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task Provision_ChecksAbsenceThenInstallsAndReusesSetupInOrder()
    {
        var manager = new RecordingServiceManager { Status = WindowsServiceState.NotInstalled };
        var output = new StringWriter();
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);
        const string executablePath = @"C:\Program Files\TrailTrainer\TrailTrainer.Developer.Host.exe";

        var exitCode = await dispatcher.RunAsync(
            ["provision"], executablePath, output, TextWriter.Null);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.SuccessExitCode, exitCode);
        Assert.Equal(
            [Operation.Status, Operation.Install, Operation.DelayedStart, Operation.Recovery],
            manager.Operations);
        Assert.Equal(executablePath, manager.InstalledExecutablePath);
        Assert.DoesNotContain(Operation.Start, manager.Operations);
        Assert.DoesNotContain(Operation.Stop, manager.Operations);
        Assert.Contains("provisioned and stopped", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Provision_ExistingServicePreventsInstallAndSetup()
    {
        var manager = new RecordingServiceManager { Status = WindowsServiceState.Stopped };
        var error = new StringWriter();
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);

        var exitCode = await dispatcher.RunAsync(["provision"], "host.exe", TextWriter.Null, error);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.OperationFailureExitCode, exitCode);
        Assert.Equal([Operation.Status], manager.Operations);
        Assert.Contains("already exists", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Provision_InstallFailurePreventsSetupWithoutRetry()
    {
        var manager = new RecordingServiceManager
        {
            Status = WindowsServiceState.NotInstalled,
            FailureOperation = Operation.Install,
            Exception = new IOException("install failed")
        };
        var error = new StringWriter();
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);

        var exitCode = await dispatcher.RunAsync(["provision"], "host.exe", TextWriter.Null, error);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.OperationFailureExitCode, exitCode);
        Assert.Equal([Operation.Status, Operation.Install], manager.Operations);
        Assert.Contains("install failed", error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Operation.DelayedStart)]
    [InlineData(Operation.Recovery)]
    public async Task Provision_SetupFailureIsSurfacedWithoutUninstallRollback(Operation failureOperation)
    {
        var manager = new RecordingServiceManager
        {
            Status = WindowsServiceState.NotInstalled,
            FailureOperation = failureOperation,
            Exception = new IOException($"{failureOperation} failed")
        };
        var error = new StringWriter();
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);

        var exitCode = await dispatcher.RunAsync(["provision"], "host.exe", TextWriter.Null, error);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.OperationFailureExitCode, exitCode);
        var expected = failureOperation == Operation.DelayedStart
            ? new[] { Operation.Status, Operation.Install, Operation.DelayedStart }
            : new[] { Operation.Status, Operation.Install, Operation.DelayedStart, Operation.Recovery };
        Assert.Equal(expected, manager.Operations);
        Assert.DoesNotContain(Operation.Uninstall, manager.Operations);
        Assert.DoesNotContain(Operation.Start, manager.Operations);
        Assert.Contains($"{failureOperation} failed", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Provision_InvalidArgumentsReturnInvalidCommandExitCode()
    {
        var manager = new RecordingServiceManager();
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);

        var exitCode = await dispatcher.RunAsync(
            ["provision", "extra"], "host.exe", TextWriter.Null, TextWriter.Null);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.InvalidCommandExitCode, exitCode);
        Assert.Empty(manager.Operations);
    }

    [Fact]
    public async Task Provision_NonWindowsFailsBeforeProcessExecution()
    {
        var runner = new RecordingProcessRunner();
        var manager = new ScWindowsServiceManager(runner, new StubPlatform(false));
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);

        var exitCode = await dispatcher.RunAsync(
            ["provision"], "host.exe", TextWriter.Null, TextWriter.Null);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.OperationFailureExitCode, exitCode);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task Provision_WithProductionManagerUsesExistingScmOperationsOnly()
    {
        var runner = new RecordingProcessRunner(
            new WindowsServiceProcessResult(1060, string.Empty, string.Empty),
            new WindowsServiceProcessResult(1060, string.Empty, string.Empty),
            Success(),
            QueryState(1),
            Success(),
            QueryState(1),
            Success(),
            Success());
        var dispatcher = new WindowsServiceManagementCommandDispatcher(CreateManager(runner));

        var exitCode = await dispatcher.RunAsync(
            ["provision"], "host.exe", TextWriter.Null, TextWriter.Null);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.SuccessExitCode, exitCode);
        Assert.Equal(
            ["query", "query", "create", "query", "config", "query", "failure", "failureflag"],
            runner.Calls.Select(call => call.Arguments[0]));
        Assert.DoesNotContain(runner.Calls, call =>
            call.Arguments[0] is "start" or "stop" or "delete");
        Assert.All(runner.Calls, call =>
            Assert.Contains("TrailTrainer Developer", call.Arguments));
    }

    [Fact]
    public async Task Deprovision_NotInstalledIsIdempotentSuccessWithoutStopOrUninstall()
    {
        var manager = new RecordingServiceManager { Status = WindowsServiceState.NotInstalled };
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);

        var exitCode = await dispatcher.RunAsync(
            ["deprovision"], "host.exe", TextWriter.Null, TextWriter.Null);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.SuccessExitCode, exitCode);
        Assert.Equal([Operation.Status], manager.Operations);
    }

    [Fact]
    public async Task Deprovision_StoppedServiceUninstallsWithoutStop()
    {
        var manager = new RecordingServiceManager { Status = WindowsServiceState.Stopped };
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);

        var exitCode = await dispatcher.RunAsync(
            ["deprovision"], "host.exe", TextWriter.Null, TextWriter.Null);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.SuccessExitCode, exitCode);
        Assert.Equal([Operation.Status, Operation.Uninstall], manager.Operations);
    }

    [Theory]
    [InlineData(WindowsServiceState.Running)]
    [InlineData(WindowsServiceState.Paused)]
    public async Task Deprovision_ActiveServiceStopsOnceBeforeUninstall(WindowsServiceState state)
    {
        var manager = new RecordingServiceManager { Status = state };
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);

        var exitCode = await dispatcher.RunAsync(
            ["deprovision"], "host.exe", TextWriter.Null, TextWriter.Null);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.SuccessExitCode, exitCode);
        Assert.Equal([Operation.Status, Operation.Stop, Operation.Uninstall], manager.Operations);
        Assert.DoesNotContain(Operation.Start, manager.Operations);
    }

    [Theory]
    [InlineData(WindowsServiceState.StartPending)]
    [InlineData(WindowsServiceState.StopPending)]
    [InlineData(WindowsServiceState.Unknown)]
    public async Task Deprovision_IndeterminateStateFailsWithoutStopOrUninstall(WindowsServiceState state)
    {
        var manager = new RecordingServiceManager { Status = state };
        var error = new StringWriter();
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);

        var exitCode = await dispatcher.RunAsync(
            ["deprovision"], "host.exe", TextWriter.Null, error);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.OperationFailureExitCode, exitCode);
        Assert.Equal([Operation.Status], manager.Operations);
        Assert.Contains("without waiting or polling", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deprovision_StopFailurePreventsUninstallAndRetry()
    {
        var manager = new RecordingServiceManager
        {
            Status = WindowsServiceState.Running,
            FailureOperation = Operation.Stop,
            Exception = new IOException("stop failed")
        };
        var error = new StringWriter();
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);

        var exitCode = await dispatcher.RunAsync(
            ["deprovision"], "host.exe", TextWriter.Null, error);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.OperationFailureExitCode, exitCode);
        Assert.Equal([Operation.Status, Operation.Stop], manager.Operations);
        Assert.Contains("stop failed", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deprovision_UninstallFailureAfterStopIsSurfacedWithoutRestartOrRollback()
    {
        var manager = new RecordingServiceManager
        {
            Status = WindowsServiceState.Running,
            FailureOperation = Operation.Uninstall,
            Exception = new IOException("uninstall failed")
        };
        var error = new StringWriter();
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);

        var exitCode = await dispatcher.RunAsync(
            ["deprovision"], "host.exe", TextWriter.Null, error);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.OperationFailureExitCode, exitCode);
        Assert.Equal([Operation.Status, Operation.Stop, Operation.Uninstall], manager.Operations);
        Assert.DoesNotContain(Operation.Start, manager.Operations);
        Assert.Contains("uninstall failed", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deprovision_InvalidArgumentsReturnInvalidCommandExitCode()
    {
        var manager = new RecordingServiceManager();
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);

        var exitCode = await dispatcher.RunAsync(
            ["deprovision", "extra"], "host.exe", TextWriter.Null, TextWriter.Null);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.InvalidCommandExitCode, exitCode);
        Assert.Empty(manager.Operations);
    }

    [Fact]
    public async Task Deprovision_NonWindowsFailsBeforeProcessExecution()
    {
        var runner = new RecordingProcessRunner();
        var manager = new ScWindowsServiceManager(runner, new StubPlatform(false));
        var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);

        var exitCode = await dispatcher.RunAsync(
            ["deprovision"], "host.exe", TextWriter.Null, TextWriter.Null);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.OperationFailureExitCode, exitCode);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task Deprovision_WithProductionManagerUsesExistingScmOperationsOnly()
    {
        var runner = new RecordingProcessRunner(
            QueryState(4),
            Success(),
            QueryState(3),
            Success());
        var dispatcher = new WindowsServiceManagementCommandDispatcher(CreateManager(runner));

        var exitCode = await dispatcher.RunAsync(
            ["deprovision"], "host.exe", TextWriter.Null, TextWriter.Null);

        Assert.Equal(WindowsServiceManagementCommandDispatcher.SuccessExitCode, exitCode);
        Assert.Equal(["query", "stop", "query", "delete"],
            runner.Calls.Select(call => call.Arguments[0]));
        Assert.All(runner.Calls, call =>
            Assert.Contains("TrailTrainer Developer", call.Arguments));
        Assert.DoesNotContain(runner.Calls, call => call.Arguments[0] == "start");
    }

    [Fact]
    public async Task Deprovision_DoesNotDeleteApplicationOrLifecycleFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"trailtrainer-dev-0043-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var applicationFile = Path.Combine(directory, "TrailTrainer.Developer.Host.exe");
        var lifecycleFile = Path.Combine(directory, "lifecycle.json");
        await File.WriteAllTextAsync(applicationFile, "application");
        await File.WriteAllTextAsync(lifecycleFile, "state");
        try
        {
            var manager = new RecordingServiceManager { Status = WindowsServiceState.Stopped };
            var dispatcher = new WindowsServiceManagementCommandDispatcher(manager);

            var exitCode = await dispatcher.RunAsync(
                ["deprovision"], applicationFile, TextWriter.Null, TextWriter.Null);

            Assert.Equal(WindowsServiceManagementCommandDispatcher.SuccessExitCode, exitCode);
            Assert.True(File.Exists(applicationFile));
            Assert.True(File.Exists(lifecycleFile));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
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
        Status,
        Recovery,
        DelayedStart
    }

    private sealed class RecordingServiceManager : IWindowsServiceManager
    {
        public List<Operation> Operations { get; } = [];
        public string? InstalledExecutablePath { get; private set; }
        public WindowsServiceState Status { get; init; }
        public Exception? Exception { get; init; }
        public Operation? FailureOperation { get; init; }

        public Task InstallAsync(string executablePath, CancellationToken cancellationToken = default)
        {
            InstalledExecutablePath = executablePath;
            return Record(Operation.Install);
        }

        public Task UninstallAsync(CancellationToken cancellationToken = default) => Record(Operation.Uninstall);
        public Task StartAsync(CancellationToken cancellationToken = default) => Record(Operation.Start);
        public Task StopAsync(CancellationToken cancellationToken = default) => Record(Operation.Stop);
        public Task ConfigureRecoveryAsync(CancellationToken cancellationToken = default) => Record(Operation.Recovery);
        public Task ConfigureDelayedStartAsync(CancellationToken cancellationToken = default) =>
            Record(Operation.DelayedStart);

        public Task<WindowsServiceState> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            Operations.Add(Operation.Status);
            return Exception is null || FailureOperation is not null && FailureOperation != Operation.Status
                ? Task.FromResult(Status)
                : Task.FromException<WindowsServiceState>(Exception);
        }

        private Task Record(Operation operation)
        {
            Operations.Add(operation);
            return Exception is null || FailureOperation is not null && FailureOperation != operation
                ? Task.CompletedTask
                : Task.FromException(Exception);
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
