using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using TrailTrainer.Developer.Host;

namespace TrailTrainer.Developer.Tests;

public sealed class OperationalHealthDiagnosticsTests
{
    [Fact]
    public async Task RunningServiceAndValidRuntimeAreHealthy()
    {
        var manager = new ReadOnlyServiceManager(WindowsServiceState.Running);
        var validator = new RecordingRuntimeValidator();

        var result = await new OperationalHealthDiagnostics(manager, validator).EvaluateAsync();

        Assert.Equal(OperationalHealthStatus.Healthy, result.Status);
        Assert.Contains("running", result.Reason, StringComparison.Ordinal);
        Assert.Equal(1, manager.StatusCalls);
        Assert.Equal(1, validator.Calls);
        Assert.Equal(0, manager.MutationCalls);
    }

    [Theory]
    [InlineData(WindowsServiceState.Stopped)]
    [InlineData(WindowsServiceState.Paused)]
    public async Task StableNonRunningServiceAndValidRuntimeAreDegraded(WindowsServiceState state)
    {
        var manager = new ReadOnlyServiceManager(state);
        var validator = new RecordingRuntimeValidator();

        var result = await new OperationalHealthDiagnostics(manager, validator).EvaluateAsync();

        Assert.Equal(OperationalHealthStatus.Degraded, result.Status);
        Assert.Contains(state.ToString(), result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, manager.StatusCalls);
        Assert.Equal(1, validator.Calls);
        Assert.Equal(0, manager.MutationCalls);
    }

    [Theory]
    [InlineData(WindowsServiceState.NotInstalled)]
    [InlineData(WindowsServiceState.StartPending)]
    [InlineData(WindowsServiceState.StopPending)]
    [InlineData(WindowsServiceState.Unknown)]
    public async Task UnavailableOrUnstableServiceIsUnhealthyWithoutRuntimeResolution(
        WindowsServiceState state)
    {
        var manager = new ReadOnlyServiceManager(state);
        var validator = new RecordingRuntimeValidator();

        var result = await new OperationalHealthDiagnostics(manager, validator).EvaluateAsync();

        Assert.Equal(OperationalHealthStatus.Unhealthy, result.Status);
        Assert.Equal(1, manager.StatusCalls);
        Assert.Equal(0, validator.Calls);
        Assert.Equal(0, manager.MutationCalls);
    }

    [Fact]
    public async Task StatusFailureIsUnhealthyWithConciseDiagnostic()
    {
        var manager = new ReadOnlyServiceManager(
            WindowsServiceState.Unknown,
            new IOException("SCM unavailable"));
        var validator = new RecordingRuntimeValidator();

        var result = await new OperationalHealthDiagnostics(manager, validator).EvaluateAsync();

        Assert.Equal(OperationalHealthStatus.Unhealthy, result.Status);
        Assert.Contains("SCM unavailable", result.Reason, StringComparison.Ordinal);
        Assert.Equal(1, manager.StatusCalls);
        Assert.Equal(0, validator.Calls);
        Assert.Equal(0, manager.MutationCalls);
    }

    [Fact]
    public async Task RuntimeCompositionFailureIsUnhealthy()
    {
        var manager = new ReadOnlyServiceManager(WindowsServiceState.Running);
        var validator = new RecordingRuntimeValidator(new OptionsValidationException(
            "runtime",
            typeof(DeveloperProductionRuntimeOptions),
            ["storage directory is required"]));

        var result = await new OperationalHealthDiagnostics(manager, validator).EvaluateAsync();

        Assert.Equal(OperationalHealthStatus.Unhealthy, result.Status);
        Assert.Contains("storage directory is required", result.Reason, StringComparison.Ordinal);
        Assert.Equal(1, validator.Calls);
        Assert.Equal(0, manager.MutationCalls);
    }

    [Fact]
    public async Task ProductionRuntimeValidatorResolvesActualGraphWithoutWorkflowOrFileSideEffects()
    {
        var root = Path.Combine(Path.GetTempPath(), $"trailtrainer-dev-0045-{Guid.NewGuid():N}");
        var storage = Path.Combine(root, "lifecycle");
        var configuration = CreateConfiguration(storage);
        try
        {
            await new ProductionRuntimeHealthValidator(configuration).ValidateAsync();

            Assert.False(Directory.Exists(root));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ProductionRuntimeValidatorMissingConfigurationFailsClearly()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() =>
            new ProductionRuntimeHealthValidator(configuration).ValidateAsync());

        Assert.Contains(
            nameof(DeveloperProductionRuntimeOptions.LifecycleStateStorageDirectory),
            exception.Message,
            StringComparison.Ordinal);
    }

    private static IConfiguration CreateConfiguration(string storageDirectory) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DeveloperProductionRuntimeOptions.SectionName}:" +
                    nameof(DeveloperProductionRuntimeOptions.LifecycleStateStorageDirectory)] = storageDirectory,
                [$"{CodexExecutionOptions.SectionName}:ExecutablePath"] = "test-codex-never-run"
            })
            .Build();

    private sealed class RecordingRuntimeValidator(Exception? exception = null) : IProductionRuntimeHealthValidator
    {
        public int Calls { get; private set; }

        public Task ValidateAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return exception is null ? Task.CompletedTask : Task.FromException(exception);
        }
    }

    private sealed class ReadOnlyServiceManager(
        WindowsServiceState state,
        Exception? statusException = null) : IWindowsServiceManager
    {
        public int StatusCalls { get; private set; }
        public int MutationCalls { get; private set; }

        public Task<WindowsServiceState> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            StatusCalls++;
            return statusException is null
                ? Task.FromResult(state)
                : Task.FromException<WindowsServiceState>(statusException);
        }

        public Task InstallAsync(string executablePath, CancellationToken cancellationToken = default) => Mutation();
        public Task UninstallAsync(CancellationToken cancellationToken = default) => Mutation();
        public Task StartAsync(CancellationToken cancellationToken = default) => Mutation();
        public Task StopAsync(CancellationToken cancellationToken = default) => Mutation();
        public Task ConfigureRecoveryAsync(CancellationToken cancellationToken = default) => Mutation();
        public Task ConfigureDelayedStartAsync(CancellationToken cancellationToken = default) => Mutation();

        private Task Mutation()
        {
            MutationCalls++;
            return Task.CompletedTask;
        }
    }
}
