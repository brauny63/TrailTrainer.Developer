namespace TrailTrainer.Developer.Core;

public interface IAsyncDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default);
}
