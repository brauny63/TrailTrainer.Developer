using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Persistence;

public sealed class LocalJsonDeveloperLifecycleStateStore : IDeveloperLifecycleStateStore
{
    private readonly string storageDirectory;

    public LocalJsonDeveloperLifecycleStateStore(string storageDirectory)
    {
        this.storageDirectory = LifecycleStateJsonFormat.NormalizeStorageDirectory(storageDirectory);
    }

    public async Task SaveAsync(
        DeveloperLifecyclePersistedState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(storageDirectory);
        var targetPath = LifecycleStateJsonFormat.StatePath(storageDirectory, state.TaskId);
        var temporaryPath = Path.Combine(
            storageDirectory,
            $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await LifecycleStateJsonFormat.SerializeAsync(stream, state, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
        }
    }

    public async Task<DeveloperLifecyclePersistedState?> LoadAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = LifecycleStateJsonFormat.StatePath(storageDirectory, taskId);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await LifecycleStateJsonFormat.DeserializeAsync(stream, cancellationToken);
    }

    public Task DeleteAsync(string taskId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(LifecycleStateJsonFormat.StatePath(storageDirectory, taskId));
        return Task.CompletedTask;
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
