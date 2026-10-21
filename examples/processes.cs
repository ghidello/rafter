#!/usr/bin/env dotnet
#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj
#:property PublishAot=false

using Sotsera.Rafter;

var command = Rafter.Command("Display the installed .NET SDK version.");

return await command.RunAsync(args, async context =>
{
    await context.Process("dotnet")
        .Arguments("--version")
        .RunAsync();

    context.WriteLine("SDK inspection completed.");
});
