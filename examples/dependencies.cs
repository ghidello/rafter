#!/usr/bin/env dotnet
#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj
#:property PublishAot=false

// Uses .NET 10 native Microsoft.Testing.Platform mode, as selected by this repository's global.json.
using Sotsera.Rafter;

var command = Rafter.Command("Build the project and run its tests.");

var project = command.Option<string>("project", "The test project to build and run.")
    .Required()
    .Validate("Must not be blank.", value => !string.IsNullOrWhiteSpace(value));

var build = command.Target("build", "Build the project.")
    .Run(async context =>
    {
        await context.Process("dotnet")
            .Arguments("build", context.GetValue(project))
            .RunAsync();
    });

var test = command.Target("test", "Run the tests after a successful build.")
    .DependsOn(build)
    .Run(async context =>
    {
        await context.Process("dotnet")
            .Arguments("test", "--project", context.GetValue(project), "--no-build")
            .RunAsync();
    });

var verify = command.Target("verify", "Build and test the project.")
    .DependsOn(build, test);

return await command.RunAsync(args, verify);
