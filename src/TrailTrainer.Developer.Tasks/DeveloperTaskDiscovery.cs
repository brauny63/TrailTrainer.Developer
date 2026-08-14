using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class DeveloperTaskDiscovery : IDeveloperTaskDiscovery
{
    public Task<IReadOnlyList<DeveloperTaskDescriptor>> DiscoverAsync(
        string repositoryRootPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        cancellationToken.ThrowIfCancellationRequested();

        var repositoryRoot = Path.GetFullPath(repositoryRootPath);
        if (!Directory.Exists(repositoryRoot))
        {
            throw new DirectoryNotFoundException($"Repository root '{repositoryRoot}' does not exist.");
        }

        var taskDirectory = Path.Combine(repositoryRoot, "docs", "developer-tasks");
        if (!Directory.Exists(taskDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Developer Task directory '{taskDirectory}' does not exist.");
        }

        var descriptors = new List<DeveloperTaskDescriptor>();
        foreach (var filePath in Directory.EnumerateFiles(taskDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = Path.GetFileName(filePath);
            if (DeveloperTaskFileConvention.TryParseFileName(fileName, out var id))
            {
                descriptors.Add(new DeveloperTaskDescriptor(
                    id,
                    Path.GetFullPath(filePath),
                    fileName));
            }
        }

        IReadOnlyList<DeveloperTaskDescriptor> result = descriptors
            .OrderBy(descriptor => descriptor.Id.Number)
            .ToArray();
        return Task.FromResult(result);
    }
}
