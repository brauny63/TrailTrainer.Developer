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
