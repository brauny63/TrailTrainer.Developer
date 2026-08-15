using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class SystemAsyncDelay : IAsyncDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) =>
        Task.Delay(delay, cancellationToken);
}
