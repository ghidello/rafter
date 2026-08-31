#:package Sotsera.Rafter@0.1.0

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Demonstrate target-owned and command-wide cleanup.");

var work = command.Target("work")
    .Description("Run work with target-owned cleanup.")
    .Run(context => context.Output.Line("Working."))
    .Finally(context => context.Output.Line("Target cleanup."));

command.Finally(context => context.Output.Line("Command cleanup."));

return await command.RunAsync(work, args);
