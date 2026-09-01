#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Demonstrate target and process working-directory scopes.");

var fixture = command.RequiredOption<string>("fixture")
    .Description("Path to the deterministic process fixture.");

var targetDirectory = command.Option<string>("target-directory")
    .Description("Working directory inherited by target operations.")
    .Default("workspace");

var processDirectory = command.Option<string>("process-directory")
    .Description("Per-process directory resolved from the target directory.")
    .Default("tools");

var inspect = command.Target("inspect")
    .Description("Inspect inherited and overridden working directories.")
    .WorkingDirectory(targetDirectory)
    .Run(async context =>
    {
        context.Output.Property("targetWorkingDirectory", context.WorkingDirectory);

        await context.Process(fixture)
            .Argument("working-directory")
            .Run();

        await context.Process(fixture)
            .Argument("working-directory")
            .WorkingDirectory(processDirectory)
            .Run();
    })
    .Finally(context =>
        context.Output.Property("targetCleanupWorkingDirectory", context.WorkingDirectory));

command.Finally(context =>
    context.Output.Property("commandCleanupWorkingDirectory", context.WorkingDirectory));

return await command.RunAsync(inspect, args);
