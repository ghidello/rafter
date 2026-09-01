#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Demonstrate reusable generic process specifications, execution, and capture.");

var fixture = command.RequiredOption<string>("fixture")
    .Description("Path to the deterministic process fixture.");

var message = command.Option<string>("message")
    .Description("Message emitted by the fixture.")
    .Default("fixture output");

var workingDirectory = command.Option<string>("working-directory")
    .Description("Working directory used by the fixture process.")
    .Default(".");

var timeout = command.Option<TimeSpan>("timeout")
    .Description("Maximum process execution time.")
    .Default(TimeSpan.FromSeconds(30));

var run = command.Target("run")
    .Description("Stream and capture fixture output.")
    .Run(async context =>
    {
        var process = context.Process(fixture)
            .Argument("emit")
            .WorkingDirectory(workingDirectory)
            .Timeout(timeout);

        var streamed = await process
            .Option("--stdout", message)
            .Option("--stderr", "streamed diagnostic")
            .Run();

        var capture = await process
            .Option("--stdout", message)
            .Option("--exit-code", 2)
            .ValidExitCodes(0, 2)
            // The limit applies independently to stdout and stderr.
            .CaptureLimitBytes(2 * 1024 * 1024)
            .Capture();

        context.Output.Property("streamedExitCode", streamed.ExitCode);
        context.Output.Property("captured", capture.StandardOutput.Trim());
    });

return await command.RunAsync(run, args);
