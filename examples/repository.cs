#:package Sotsera.Rafter@0.1.0

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Restore, build, test, and pack the Rafter repository.")
    .Concurrency(2);

var configuration = command.Option<string>("configuration")
    .Description("Build configuration.")
    .Alias('c')
    .Default("Release");

var prepare = command.Target("prepare")
    .Description("Prepare generated output directories.")
    .Run(context =>
    {
        context.FileSystem.EnsureEmptyDirectory("artifacts/packages");
        context.FileSystem.EnsureEmptyDirectory("artifacts/test-results");
    });

var restore = command.Target("restore")
    .Description("Restore repository dependencies.")
    .DependsOn(prepare)
    .Run(context => context.DotNet
        .Restore("Rafter.slnx")
        .LockedMode()
        .Run());

var build = command.Target("build")
    .Description("Build the repository with warnings as errors.")
    .DependsOn(restore)
    .Run(context => context.DotNet
        .Build("Rafter.slnx")
        .Configuration(configuration)
        .NoRestore()
        .WarningsAsErrors()
        .Run());

var test = command.Target("test")
    .Description("Run repository tests.")
    .DependsOn(build)
    .Run(context => context.DotNet
        .Test("Rafter.slnx")
        .Configuration(configuration)
        .NoBuild()
        .ResultsDirectory("artifacts/test-results")
        .Run());

var pack = command.Target("pack")
    .Description("Create repository packages.")
    .DependsOn(build)
    .Run(context => context.DotNet
        .Pack("Rafter.slnx")
        .Configuration(configuration)
        .NoBuild()
        .Output("artifacts/packages")
        .Run());

var verify = command.Target("verify")
    .Description("Run every repository verification step.")
    .DependsOn(test, pack);

return await command.RunAsync(verify, args);
