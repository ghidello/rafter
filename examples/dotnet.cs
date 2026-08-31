#:package Sotsera.Rafter@0.1.0

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Build and test a .NET solution.");

var solution = command.Option<string>("solution")
    .Description("Solution to build.")
    .Default("Rafter.slnx");

var configuration = command.Option<string>("configuration")
    .Description("Build configuration.")
    .Alias('c')
    .Default("Release");

var restore = command.Target("restore")
    .Description("Restore locked dependencies.")
    .Run(context => context.DotNet
        .Restore(solution)
        .LockedMode()
        .NoCache()
        .Run());

var build = command.Target("build")
    .Description("Build with warnings as errors.")
    .DependsOn(restore)
    .Run(context => context.DotNet
        .Build(solution)
        .Configuration(configuration)
        .NoRestore()
        .WarningsAsErrors()
        .Run());

var test = command.Target("test")
    .Description("Run tests without rebuilding.")
    .DependsOn(build)
    .Run(context => context.DotNet
        .Test(solution)
        .Configuration(configuration)
        .NoBuild()
        .Logger("console;verbosity=minimal")
        .Run());

return await command.RunAsync(test, args);
