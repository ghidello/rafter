#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Run dotnet with streamed output and an explicit child environment policy.");

var clean = command.Option<bool>("clean-environment")
    .Description("Start with an empty child environment; requires --dotnet to be an absolute executable path.")
    .Default(false);

var executable = command.Option<string>("dotnet")
    .Description("The dotnet executable name or path.")
    .Default("dotnet");

var version = command.Target("version")
    .Description("Print the SDK version using the command root as the working directory.")
    .Run(async context =>
    {
        var result = await context.Process(context.Value(executable))
            .Description("Inspect the selected .NET SDK.")
            .Argument("--version")
            .InheritEnvironment(!context.Value(clean))
            .Environment("DOTNET_NOLOGO", "1")
            .RunAsync();

        if (!result.Succeeded)
        {
            return;
        }

        context.Output.WriteLine("SDK inspection completed.");
    });

return await command.RunAsync(version, args);
