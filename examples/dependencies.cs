// Syntax draft: targets and external processes are not implemented yet.
// Assumes the command root contains a buildable project or solution with tests.
using Sotsera.Rafter;

var command = Rafter.Command("Build the project and run its tests.");

var build = command.Target("build", "Build the project.")
    .Run(async context =>
    {
        await context.Process("dotnet")
            .Arguments("build")
            .RunAsync();
    });

var test = command.Target("test", "Run the tests after a successful build.")
    .DependsOn(build)
    .Run(async context =>
    {
        await context.Process("dotnet")
            .Arguments("test", "--no-build")
            .RunAsync();
    });

var verify = command.Target("verify", "Build and test the project.")
    .DependsOn(build, test);

return await command.RunAsync(args, verify);
