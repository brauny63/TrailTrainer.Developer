namespace TrailTrainer.Developer.Core;

public sealed class DeveloperTaskExecutionException : Exception
{
    public DeveloperTaskExecutionException(string message)
        : base(message)
    {
    }

    public DeveloperTaskExecutionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
