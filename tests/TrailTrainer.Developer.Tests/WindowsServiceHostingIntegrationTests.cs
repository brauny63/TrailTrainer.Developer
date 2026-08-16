using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TrailTrainer.Developer.Host;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class WindowsServiceHostingIntegrationTests
{
    [Fact]
    public void WindowsServiceIntegration_UsesStableHostBoundaryServiceName()
    {
        Assert.Equal(
            "TrailTrainer Developer",
            AutomaticResumeWindowsServiceExtensions.ServiceName);
    }

    [Fact]
    public void AddAutomaticResumeWindowsService_NullCollectionRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AutomaticResumeWindowsServiceExtensions.AddAutomaticResumeWindowsService(null!));
    }

    [Fact]
    public void AddAutomaticResumeWindowsService_ReturnsSameCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddAutomaticResumeWindowsService();

        Assert.Same(services, returned);
    }

    [Fact]
    public void WindowsServiceIntegration_DoesNotDuplicateAutomaticResumeHostedAdapter()
    {
        var services = new ServiceCollection();

        services.AddAutomaticResumeWindowsService();
        services.AddAutomaticResumePipeline();

        Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(HostedAutomaticResumeService));
    }

    [Fact]
    public void HostAssembly_ContainsNoCustomServiceBaseImplementation()
    {
        var customServiceBase = typeof(AutomaticResumeWindowsServiceExtensions).Assembly
            .GetTypes()
            .Where(type => type.BaseType?.FullName == "System.ServiceProcess.ServiceBase")
            .ToArray();

        Assert.Empty(customServiceBase);
    }

    [Fact]
    public void WindowsServiceIntegration_AddsNoHostedServiceOrWorkflowExecutionOnConsole()
    {
        var services = new ServiceCollection();

        services.AddAutomaticResumeWindowsService();

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService));
    }
}
