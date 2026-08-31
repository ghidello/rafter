#:package Sotsera.Rafter@0.1.0

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Run the smallest useful Rafter command.");

var hello = command.Target("hello")
    .Description("Print a greeting.")
    .Run(context => context.Output.Success("Hello from Rafter."));

return await command.RunAsync(hello, args);
