using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class SystemUtcClock : IUtcClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
