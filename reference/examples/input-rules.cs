#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Describe a range after validating related inputs.");

var minimum = command.Option<int>("minimum")
    .Description("The inclusive lower bound.")
    .Validate("Must be zero or greater.", value => value >= 0)
    .Default(1);

var maximum = command.Option<int>("maximum")
    .Description("The inclusive upper bound.")
    .Validate("Must be zero or greater.", value => value >= 0)
    .Default(10);

var ascending = command.Option<bool>("ascending")
    .Description("Choose ascending order explicitly; this is also the default order.")
    .Default(false);

var descending = command.Option<bool>("descending")
    .Description("Choose descending order.")
    .Default(false);

command.Validate(
    "--minimum must not exceed --maximum.",
    inputs => inputs.Value(minimum) <= inputs.Value(maximum));

command.Validate(
    "--ascending and --descending cannot be used together.",
    inputs => !(inputs.Value(ascending) && inputs.Value(descending)));

var range = command.Target("range")
    .Description("Print the validated range and its order.")
    .Run(context =>
    {
        var order = context.Value(descending) ? "descending" : "ascending";
        context.Output.WriteLine($"Range: {context.Value(minimum)}..{context.Value(maximum)} ({order}).");
    });

return await command.RunAsync(range, args);
