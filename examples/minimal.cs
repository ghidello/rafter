#!/usr/bin/env dotnet
#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj
#:property PublishAot=false

// The default root is the working directory captured when RunAsync starts.
using Sotsera.Rafter;

var command = Rafter.Command("Print a greeting.");

return await command.RunAsync(args, context =>
{
    context.WriteLine("Hello from Rafter.");
});
