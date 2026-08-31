#:package Sotsera.Rafter@0.1.0

using System.Text.Json.Serialization;
using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Demonstrate generic process execution and capture.");

var fixture = command.RequiredOption<string>("fixture")
    .Description("Path to the deterministic process fixture.");

var message = command.Option<string>("message")
    .Description("Message emitted by the fixture.")
    .Default("fixture output");

var workingDirectory = command.Option<string>("working-directory")
    .Description("Working directory used by the fixture process.")
    .Default(".");

var run = command.Target("run")
    .Description("Stream and capture fixture output.")
    .Run(async context =>
    {
        await context.Process(fixture)
            .Argument("emit")
            .Option("--stdout", message)
            .Option("--stderr", "streamed diagnostic")
            .WorkingDirectory(workingDirectory)
            .Run();

        var capture = await context.Process(fixture)
            .Argument("emit")
            .Option("--stdout", message)
            .AcceptExitCode(0)
            .CaptureLimit(1024 * 1024)
            .Capture();

        var payload = await context.Process(fixture)
            .Argument("json")
            .CaptureJson(ProcessJsonContext.Default.FixturePayload);

        context.Output.Property("captured", capture.StandardOutput.Trim());
        context.Output.Property("payload", payload.Value);
    });

return await command.RunAsync(run, args);

sealed record FixturePayload(string Value);

[JsonSerializable(typeof(FixturePayload))]
sealed partial class ProcessJsonContext : JsonSerializerContext;
