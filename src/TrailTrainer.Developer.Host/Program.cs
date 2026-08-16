using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Host;
using TrailTrainer.Developer.Tasks;

if (args.Length == 1 && args[0].Equals("codex-probe", StringComparison.OrdinalIgnoreCase))
{
    var probeBuilder = Host.CreateApplicationBuilder();
    probeBuilder.Services.AddDeveloperProductionRuntime(probeBuilder.Configuration);
    using var probeProvider = probeBuilder.Services.BuildServiceProvider();
    return await new CodexCompatibilityProbeCommand(
        probeProvider.GetRequiredService<ICodexCompatibilityProbe>()).RunAsync(Console.Out);
}

if (args.Length is 3 or 4 && args[0].Equals("github-probe", StringComparison.OrdinalIgnoreCase))
{
    var checkPulls = args.Length == 4 && args[3].Equals("--check-pulls", StringComparison.OrdinalIgnoreCase);
    if (args.Length == 4 && !checkPulls)
    {
        await Console.Error.WriteLineAsync("Usage: github-probe <owner> <repository> [--check-pulls]");
        return 1;
    }

    var probeBuilder = Host.CreateApplicationBuilder();
    probeBuilder.Services.AddDeveloperProductionRuntime(probeBuilder.Configuration);
    using var probeProvider = probeBuilder.Services.BuildServiceProvider();
    return await new GitHubRepositoryProbeCommand(
        probeProvider.GetRequiredService<TrailTrainer.Developer.GitHub.IGitHubRepositoryProbe>())
        .RunAsync(args[1], args[2], checkPulls, Console.Out, Console.Error);
}

if (WindowsServiceManagementCommandDispatcher.HasCommand(args))
{
    var serviceManager = new ScWindowsServiceManager(
        new WindowsServiceProcessRunner(),
        new RuntimeWindowsPlatform());
    Func<IOperationalHealthDiagnostics>? healthDiagnosticsFactory = null;
    if (WindowsServiceManagementCommandDispatcher.IsHealthCommand(args))
    {
        var healthBuilder = Host.CreateApplicationBuilder();
        healthDiagnosticsFactory = () => new OperationalHealthDiagnostics(
            serviceManager,
            new ProductionRuntimeHealthValidator(healthBuilder.Configuration));
    }

    var dispatcher = new WindowsServiceManagementCommandDispatcher(
        serviceManager,
        healthDiagnosticsFactory);
    return await dispatcher.RunAsync(
        args,
        Environment.ProcessPath,
        Console.Out,
        Console.Error);
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddAutomaticResumeWindowsService();
builder.Services.AddDeveloperProductionRuntime(builder.Configuration);
builder.Services.AddAutomaticResumePipeline();
builder.Services.AddSingleton<
    IAutomaticResumeWorkerRequestProvider,
    ConfiguredAutomaticResumeWorkerRequestProvider>();

await builder.Build().RunAsync();
return WindowsServiceManagementCommandDispatcher.SuccessExitCode;
