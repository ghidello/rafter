#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Reuse execution settings across independent dotnet processes.");

var executable = command.Option<string>("dotnet")
    .Description("The dotnet executable name or path.")
    .Default("dotnet");

var timeout = command.Option<TimeSpan>("timeout")
    .Description("The deadline for each dotnet process.")
    .Validate("Must be positive and no greater than 49d17h2m47s294ms.",
        value => value > TimeSpan.Zero && value.TotalMilliseconds <= uint.MaxValue - 1)
    .Default(TimeSpan.FromSeconds(30));

var dotnetDefaults = new ProcessConfiguration()
    .Environment("DOTNET_NOLOGO", "1")
    .Environment("DOTNET_CLI_TELEMETRY_OPTOUT", "1")
    .Capture()
    .Timeout(timeout);

var selected = command.Target("selected")
    .Description("Print the SDK selected in the invocation directory.")
    .Run(async context =>
    {
        var result = await context.Process(context.Value(executable))
            .Configure(dotnetDefaults)
            .Description("Inspect the selected SDK.")
            .Argument("--version")
            .RunAsync();

        if (!result.Succeeded)
        {
            return;
        }

        context.Output.WriteLine($"Selected SDK: {result.Capture!.StandardOutput.Trim()}");
    });

var installed = command.Target("installed")
    .Description("Print installed SDKs using a separate process with the same defaults.")
    .DependsOn(selected)
    .Run(async context =>
    {
        var result = await context.Process(context.Value(executable))
            .Configure(dotnetDefaults)
            .Description("Inspect installed SDKs.")
            .Argument("--list-sdks")
            .RunAsync();

        if (!result.Succeeded)
        {
            return;
        }

        context.Output.WriteLine("Installed SDKs:");

        foreach (var line in result.Capture!.StandardOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
        {
            context.Output.WriteLine(line);
        }
    });

return await command.RunAsync(installed, args);
