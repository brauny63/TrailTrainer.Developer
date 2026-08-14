namespace TrailTrainer.Developer.Core;

public sealed record DeveloperTaskDescriptor(
    DeveloperTaskId Id,
    string FilePath,
    string FileName);
