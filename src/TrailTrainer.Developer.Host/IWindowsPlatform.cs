namespace TrailTrainer.Developer.Host;

public interface IWindowsPlatform
{
    bool IsWindows { get; }
}

public sealed class RuntimeWindowsPlatform : IWindowsPlatform
{
    public bool IsWindows => OperatingSystem.IsWindows();
}
