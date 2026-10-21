#!/usr/bin/env dotnet
#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj
#:property PublishAot=false

// Assumes the command root contains a src directory with a buildable project or solution.
using Sotsera.Rafter;

var command = Rafter.Command("Build the project or solution in the src directory.");

return await command.RunAsync(args, async context =>
{
    await context.Process("dotnet")
        .WorkingDirectory("src")
        .Environment("DOTNET_NOLOGO", "1")
        .Arguments("build")
        .RunAsync();

    context.WriteLine("Build completed.");
});
