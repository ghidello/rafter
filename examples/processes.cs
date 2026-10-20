// Syntax draft: external processes are not implemented yet.
using Sotsera.Rafter;

var command = Rafter.Command("Display the installed .NET SDK version.");

return await command.RunAsync(args, async context =>
{
    await context.Process("dotnet")
        .Arguments("--version")
        .RunAsync();

    context.WriteLine("SDK inspection completed.");
});
