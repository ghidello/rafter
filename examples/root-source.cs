#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Source)
    .Description("Run relative to this source file.");

var inspect = command.Target("inspect")
    .Description("Print the resolved source root.")
    .Run(context => context.Output.Property("root", context.Root));

return await command.RunAsync(inspect, args);
