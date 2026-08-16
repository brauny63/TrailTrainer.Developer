using System.Diagnostics;

namespace TrailTrainer.Developer.FakeCodexCli;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "child")
        {
            Thread.Sleep(TimeSpan.FromSeconds(2));
            File.WriteAllText(args[1], "child-survived");
            return 0;
        }

        var instruction = args.LastOrDefault() ?? string.Empty;
        if (args.Contains("fake-runner-pipe-timeout", StringComparer.Ordinal))
        {
            Console.Error.WriteLine("timed out connecting to runner pipe");
            return 1;
        }
        if (args.Contains("fake-probe-timeout", StringComparer.Ordinal) &&
            instruction.StartsWith("Compatibility probe:", StringComparison.Ordinal))
        {
            Thread.Sleep(Timeout.InfiniteTimeSpan);
        }
        const string spawnPrefix = "spawn-child:";
        var spawnAt = instruction.IndexOf(spawnPrefix, StringComparison.Ordinal);
        if (spawnAt >= 0)
        {
            var marker = instruction[(spawnAt + spawnPrefix.Length)..].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            Process.Start(new ProcessStartInfo
            {
                FileName = Environment.ProcessPath!,
                UseShellExecute = false,
                ArgumentList = { "child", marker }
            });
            Thread.Sleep(Timeout.InfiniteTimeSpan);
        }

        Console.WriteLine($"cwd={Environment.CurrentDirectory}");
        Console.WriteLine($"instruction={args.LastOrDefault()}");
        Console.WriteLine($"arguments={string.Join('|', args)}");
        foreach (var name in new[] { "USERPROFILE", "HOME", "HOMEDRIVE", "HOMEPATH", "APPDATA", "LOCALAPPDATA" })
        {
            Console.WriteLine($"{name}={Environment.GetEnvironmentVariable(name)}");
        }
        Console.Error.WriteLine("fake-codex-stderr");
        return args.Any(argument => argument.Contains("exit-23", StringComparison.Ordinal)) ? 23 : 0;
    }
}
