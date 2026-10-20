// Syntax draft: the new Rafter library is not implemented yet.
using Sotsera.Rafter;

var command = Rafter.Command("Describe a range after validating its bounds and optional label.");

var minimum = command.Option<int>("minimum", "The inclusive lower bound.")
    .Validate("Must be zero or greater.", value => value >= 0)
    .Validate("Must not exceed one hundred.", value => value <= 100)
    .Default(1);

var maximum = command.Option<int>("maximum", "The inclusive upper bound.")
    .Validate("Must be zero or greater.", value => value >= 0)
    .Validate("Must not exceed one hundred.", value => value <= 100)
    .Default(10);

var label = command.Option<string>("label", "An optional name for the range.")
    .Validate("Must not be blank when supplied.", value => !string.IsNullOrWhiteSpace(value));

command.Validate(
    "Minimum must not exceed maximum.",
    inputs => inputs.GetValue(minimum) <= inputs.GetValue(maximum));

return await command.RunAsync(args, context =>
{
    var lower = context.GetValue(minimum);
    var upper = context.GetValue(maximum);
    var rangeLabel = context.GetValue(label);

    if (rangeLabel is not null)
    {
        context.WriteLine(rangeLabel);
    }

    context.WriteLine($"Range: {lower}..{upper}.");
});
