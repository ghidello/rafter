#!/usr/bin/env dotnet
#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj
#:property PublishAot=false

var command = Rafter.Command("Build a project and inspect an unsuccessful result before leaving the body.");

var project = command.Option<string>("project", "The project to build.")
    .Validate("Must not be blank.", value => !string.IsNullOrWhiteSpace(value))
    .Required();

return await command.RunAsync(args, async context =>
{
    var result = await context.Process("dotnet")
        .Arguments("build", context.GetValue(project))
        .OnFailure(_ => ProcessFailureAction.Fail)
        .RunAsync();

    if (!result.Succeeded)
    {
        // Fail has recorded command failure but still lets this body inspect the result.
        context.WriteLine($"Build exited with code {result.ExitCode}.");
        return;
    }

    context.WriteLine("Build completed.");
});
