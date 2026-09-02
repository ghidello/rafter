#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Demonstrate optional, defaulted, validated, enum, numeric, and repeated values.");

var label = command.Option<string>("label")
    .Description("Optional label.");

var threshold = command.Option<int?>("threshold")
    .Description("Optional numeric threshold.");

var retries = command.Option<int>("retries")
    .Description("Number of retries.")
    .Default(3)
    .Validate(value => value >= 0, "Retries must be zero or greater.")
    .Validate(value => value <= 10, "Retries cannot exceed ten.");

var mode = command.Option<ExecutionMode>("mode")
    .Description("Execution mode.")
    .Default(ExecutionMode.Verify);

var include = command.RepeatedOption<string>("include")
    .Description("Path to include; may be supplied more than once.")
    .Validate(values => values.Count <= 10, "No more than ten include paths may be supplied.");

var inspect = command.Target("inspect")
    .Description("Print resolved option values.")
    .Run(context =>
    {
        context.Output.Property("label", context.Value(label));
        context.Output.Property("threshold", context.Value(threshold));
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
