using TrailTrainer.Developer.CLI;
using TrailTrainer.Developer.Git;
using TrailTrainer.Developer.Tasks;

var statusProvider = new LocalGitRepositoryStatusProvider();
var parser = new DeveloperTaskParser();
var application = new DeveloperCliApplication(
    statusProvider,
    new DeveloperTaskDiscovery(),
    parser,
    new DeveloperTaskStarter(parser, statusProvider, new LocalGitBranchCreator(statusProvider)),
    new DeveloperTaskCompleter(
        parser,
        statusProvider,
        new LocalGitStager(statusProvider),
        new LocalGitCommitter(statusProvider),
        new LocalGitPusher(statusProvider)));

return await application.RunAsync(
    args,
    Environment.CurrentDirectory,
    Console.Out,
    Console.Error);
