#:package Sotsera.Rafter@0.1.0

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Run relative to the caller's working directory.");

var inspect = command.Target("inspect")
    .Description("Print the resolved command root.")
    .Run(context => context.Output.Property("root", context.Root));

return await command.RunAsync(inspect, args);
