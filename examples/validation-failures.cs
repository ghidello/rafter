#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Distinguish invalid input from a validator implementation failure.");

var probe = command.RequiredOption<string>("probe")
    .Description("Use 'invalid' for rejected input or 'throw' for a synthetic validator failure.")
    .Validate(ValidateProbe, "The probe value is invalid.");

var inspect = command.Target("inspect")
    .Description("Run only after the probe passes validation.")
    .Run(context => context.Output.Property("probe", context.Value(probe)));

return await command.RunAsync(inspect, args);

static bool ValidateProbe(string value) => value switch
{
    "invalid" => false,
    "throw" => throw new InvalidOperationException("Synthetic validator implementation failure."),
    _ => true,
};
