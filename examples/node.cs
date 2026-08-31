#:package Sotsera.Rafter@0.1.0

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Demonstrate typed npm and pnpm operations.");

var directory = command.Option<string>("directory")
    .Description("Node project directory.")
    .Default("web");

var install = command.Target("install")
    .Description("Install locked dependencies.")
    .WorkingDirectory(directory)
    .Run(context => context.Pnpm
        .Install()
        .FrozenLockfile()
        .Environment(environment => environment.Set("CI", "true"))
        .Run());

var verify = command.Target("verify")
    .Description("Run lint and test scripts.")
    .DependsOn(install)
    .WorkingDirectory(directory)
    .Run(async context =>
    {
        await context.Pnpm
            .Run("lint")
            .Run();

        await context.Npm
            .Run("test")
            .Run();
    });

return await command.RunAsync(verify, args);
