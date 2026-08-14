using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Persistence;

public sealed class LocalJsonDeveloperLifecycleStateDiscovery : IDeveloperLifecycleStateDiscovery
{
    private readonly string storageDirectory;

    public LocalJsonDeveloperLifecycleStateDiscovery(string storageDirectory)
    {
        this.storageDirectory = LifecycleStateJsonFormat.NormalizeStorageDirectory(storageDirectory);
    }

    public async Task<IReadOnlyList<DeveloperLifecyclePersistedState>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(storageDirectory))
        {
            return Array.Empty<DeveloperLifecyclePersistedState>();
        }

        var states = new List<DeveloperLifecyclePersistedState>();
        var taskIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(storageDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = Path.GetFileName(path);
            if (!LifecycleStateJsonFormat.IsFinalStateFileName(fileName))
            {
                continue;
            }

            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var state = await LifecycleStateJsonFormat.DeserializeAsync(stream, cancellationToken);
            var expectedFileName = LifecycleStateJsonFormat.FileName(state.TaskId);
            if (!string.Equals(fileName, expectedFileName, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "A persisted lifecycle state's filename does not match its TaskId.");
            }

            if (!taskIds.Add(state.TaskId))
            {
                throw new InvalidDataException(
                    $"Duplicate persisted lifecycle state for TaskId '{state.TaskId}'.");
            }

            states.Add(state);
        }

        states.Sort(static (left, right) =>
        {
            var timestampComparison = left.SavedAtUtc.CompareTo(right.SavedAtUtc);
            return timestampComparison != 0
                ? timestampComparison
                : StringComparer.Ordinal.Compare(left.TaskId, right.TaskId);
        });
        return Array.AsReadOnly(states.ToArray());
    }
}
