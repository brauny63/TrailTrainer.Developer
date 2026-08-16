using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Host;
using TrailTrainer.Developer.Tasks;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddAutomaticResumePipeline();
builder.Services.Configure<AutomaticResumeHostOptions>(
    builder.Configuration.GetSection(AutomaticResumeHostOptions.SectionName));
builder.Services.AddSingleton<
    IAutomaticResumeWorkerRequestProvider,
    ConfiguredAutomaticResumeWorkerRequestProvider>();

await builder.Build().RunAsync();
