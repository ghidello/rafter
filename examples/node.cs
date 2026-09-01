#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Demonstrate typed npm and pnpm operations.");

var directory = command.Option<string>("directory")
    .Description("Node project directory.")
    .Default("web");

var install = command.Target("install")
    .Description("Install locked dependencies with a typed-process directory override.")
    .Run(context => context.Pnpm
        .Install()
        .FrozenLockfile()
        .WorkingDirectory(directory)
        .Environment(environment => environment.Set("CI", "true"))
        .Timeout(TimeSpan.FromMinutes(5))
        .Run()
    );

var verify = command.Target("verify")
    .Description("Run typed processes that inherit the target directory.")
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
