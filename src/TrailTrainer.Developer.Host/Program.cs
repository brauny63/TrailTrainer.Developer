using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Host;
using TrailTrainer.Developer.Tasks;

if (WindowsServiceManagementCommandDispatcher.HasCommand(args))
{
    var dispatcher = new WindowsServiceManagementCommandDispatcher(
        new ScWindowsServiceManager(new WindowsServiceProcessRunner(), new RuntimeWindowsPlatform()));
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
builder.Services.Configure<AutomaticResumeHostOptions>(
    builder.Configuration.GetSection(AutomaticResumeHostOptions.SectionName));
builder.Services.AddSingleton<
    IAutomaticResumeWorkerRequestProvider,
    ConfiguredAutomaticResumeWorkerRequestProvider>();

await builder.Build().RunAsync();
return WindowsServiceManagementCommandDispatcher.SuccessExitCode;
