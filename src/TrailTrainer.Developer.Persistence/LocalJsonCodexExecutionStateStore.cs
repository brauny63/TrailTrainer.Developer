using System.Text.Json;
using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Persistence;

public sealed class LocalJsonCodexExecutionStateStore : ICodexExecutionStateStore
{
    private readonly string directory;

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
        Directory.CreateDirectory(directory);
        var path = StatePath(state.TaskId);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
            await JsonSerializer.SerializeAsync(stream, state, cancellationToken: cancellationToken);
            await stream.FlushAsync(cancellationToken);
            File.Move(temporary, path, true);
        }
        finally { File.Delete(temporary); }
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
