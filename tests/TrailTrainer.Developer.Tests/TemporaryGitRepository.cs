using System.Diagnostics;

namespace TrailTrainer.Developer.Tests;

internal sealed class TemporaryGitRepository : IDisposable
{
    private readonly TemporaryDirectory directory;

    private TemporaryGitRepository(TemporaryDirectory directory)
    {
        this.directory = directory;
    }

    public string Path => directory.Path;

    public static TemporaryGitRepository Create()
    {
        var directory = TemporaryDirectory.Create("git repository");
        var repository = new TemporaryGitRepository(directory);

        try
        {
            repository.RunGit("init");
            repository.RunGit("config", "user.name", "TrailTrainer Test");
            repository.RunGit("config", "user.email", "trailtrainer-test@example.invalid");
            return repository;
        }
        catch
        {
            repository.Dispose();
            throw;
        }
    }

    public void CommitFile(string relativePath)
    {
        File.WriteAllText(System.IO.Path.Combine(Path, relativePath), "test content");
        RunGit("add", "--", relativePath);
        RunGit("commit", "-m", "Test commit");
    }

    public string RunGit(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = Path,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        startInfo.Environment["GIT_CONFIG_GLOBAL"] = System.IO.Path.Combine(Path, "missing-global-config");
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The test Git process could not be started.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(
            process.ExitCode == 0,
            $"git {string.Join(' ', arguments)} failed with exit code {process.ExitCode}. " +
            $"Output: {standardOutput} Error: {standardError}");
        return standardOutput.Trim();
    }

    public void Dispose() => directory.Dispose();
}

internal sealed class TemporaryDirectory : IDisposable
{
    private TemporaryDirectory(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static TemporaryDirectory Create(string? suffix = null)
    {
        var name = $"TrailTrainer.Developer.Tests-{Guid.NewGuid():N}";
        if (suffix is not null)
        {
            name += $"-{suffix}";
        }

        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), name);
        Directory.CreateDirectory(path);
        return new TemporaryDirectory(path);
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            foreach (var filePath in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(filePath, FileAttributes.Normal);
            }

            Directory.Delete(Path, recursive: true);
        }
    }
}
