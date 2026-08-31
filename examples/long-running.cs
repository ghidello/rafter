#:package Sotsera.Rafter@0.1.0

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Demonstrate cooperative callback cancellation.");

var wait = command.Target("wait")
    .Description("Wait until the invocation is cancelled.")
    .Run(async context =>
    {
        context.Output.Line("Waiting for cancellation.");
        await Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken);
    })
    .Finally(context => context.Output.Line("Long-running target cleanup."));

command.Finally(context => context.Output.Line("Command cleanup."));

return await command.RunAsync(wait, args);
