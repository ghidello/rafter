#:package Sotsera.Rafter@0.1.0

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Demonstrate optional, defaulted, enum, numeric, and repeated values.");

var label = command.Option<string>("label")
    .Description("Optional label.");

var retries = command.Option<int>("retries")
    .Description("Number of retries.")
    .Default(3);

var mode = command.Option<ExecutionMode>("mode")
    .Description("Execution mode.")
    .Default(ExecutionMode.Verify);

var include = command.Option<string[]>("include")
    .Description("Path to include; may be supplied more than once.")
    .Default([]);

var inspect = command.Target("inspect")
    .Description("Print resolved option values.")
    .Run(context =>
    {
        context.Output.Property("label", context.Value(label));
        context.Output.Property("retries", context.Value(retries));
        context.Output.Property("mode", context.Value(mode));
        context.Output.Property("include", context.Value(include));
    });

return await command.RunAsync(inspect, args);

enum ExecutionMode
{
    Verify,
    Repair,
}
