namespace TrailTrainer.Developer.Core;

public interface IUtcClock
{
    DateTimeOffset UtcNow { get; }
}
