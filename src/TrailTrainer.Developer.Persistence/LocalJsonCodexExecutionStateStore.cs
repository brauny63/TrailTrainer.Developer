using System.Text.Json;
using System.Collections.Concurrent;
using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Persistence;

public sealed class LocalJsonCodexExecutionStateStore : ICodexExecutionStateStore
{
    private readonly string directory;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> saveLocks = new(StringComparer.OrdinalIgnoreCase);

    public LocalJsonCodexExecutionStateStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        this.directory = Path.GetFullPath(directory);
    }

    public async Task<CodexExecutionState?> LoadAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var path = StatePath(taskId);
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<CodexExecutionState>(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidDataException($"Codex execution state '{path}' is empty.");
    }

    public async Task SaveAsync(CodexExecutionState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        var path = StatePath(state.TaskId);
        var saveLock = saveLocks.GetOrAdd(path, static _ => new SemaphoreSlim(1, 1));
        await saveLock.WaitAsync(cancellationToken);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(directory);
            await using (var stream = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, state, cancellationToken: cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"Failed to persist Codex execution state for task '{state.TaskId}' at '{path}'.",
                exception);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            saveLock.Release();
        }
    }

    public Task DeleteAsync(string taskId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(StatePath(taskId));
        return Task.CompletedTask;
    }

    private string StatePath(string taskId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        if (taskId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new ArgumentException("Invalid task ID.", nameof(taskId));
        return Path.Combine(directory, $"codex-{taskId}.json");
    }
}
