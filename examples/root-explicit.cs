#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var explicitRoot = Environment.GetEnvironmentVariable("RAFTER_EXAMPLE_ROOT")
    ?? throw new InvalidOperationException("Set RAFTER_EXAMPLE_ROOT before running this example.");

var command = Rafter.Command(Root.At(explicitRoot))
    .Description("Run relative to an explicitly selected directory.");

var inspect = command.Target("inspect")
    .Description("Print the resolved explicit root.")
    .Run(context => context.Output.Property("root", context.Root));

return await command.RunAsync(inspect, args);
