using System.Diagnostics;
using TrailTrainer.Developer.Git;

namespace TrailTrainer.Developer.Tests;

public sealed class LocalGitRepositoryStatusProviderTests
{
    private readonly LocalGitRepositoryStatusProvider provider = new();

    [Fact]
    public async Task GetStatusAsync_InitializedRepository_ReturnsRootAndCleanStatus()
    {
        using var repository = TemporaryGitRepository.Create();
        var nestedDirectory = System.IO.Path.Combine(repository.Path, "nested directory");
        Directory.CreateDirectory(nestedDirectory);

        var status = await provider.GetStatusAsync(nestedDirectory);

        Assert.True(status.IsRepository);
        Assert.Equal(
            System.IO.Path.GetFullPath(repository.Path),
            status.RepositoryRoot,
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        Assert.False(status.HasUncommittedChanges);
    }

    [Fact]
    public async Task GetStatusAsync_KnownBranch_ReturnsCurrentBranch()
    {
        using var repository = TemporaryGitRepository.Create();
        repository.RunGit("checkout", "-b", "known-branch");

        var status = await provider.GetStatusAsync(repository.Path);

        Assert.Equal("known-branch", status.CurrentBranch);
    }

    [Fact]
    public async Task GetStatusAsync_UntrackedFile_ReportsUncommittedChanges()
    {
        using var repository = TemporaryGitRepository.Create();
        File.WriteAllText(System.IO.Path.Combine(repository.Path, "untracked.txt"), "content");

        var status = await provider.GetStatusAsync(repository.Path);

        Assert.True(status.HasUncommittedChanges);
    }

    [Fact]
    public async Task GetStatusAsync_NonRepository_ReturnsNotRepository()
    {
        using var directory = TemporaryDirectory.Create();

        var status = await provider.GetStatusAsync(directory.Path);

        Assert.False(status.IsRepository);
        Assert.Null(status.RepositoryRoot);
        Assert.Null(status.CurrentBranch);
        Assert.False(status.HasUncommittedChanges);
    }

    private sealed class TemporaryGitRepository : IDisposable
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

        public void RunGit(params string[] arguments)
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
        }

        public void Dispose() => directory.Dispose();
    }

    private sealed class TemporaryDirectory : IDisposable
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
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
