using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TrailTrainer.Developer.Host;

public static class AutomaticResumeWindowsServiceExtensions
{
    public const string ServiceName = "TrailTrainer Developer";

    public static IServiceCollection AddAutomaticResumeWindowsService(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddWindowsService(options => options.ServiceName = ServiceName);
        return services;
    }
}
