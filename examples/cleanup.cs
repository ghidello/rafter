#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Demonstrate target-owned and command-wide cleanup.");

var work = command.Target("work")
    .Description("Run work with context-free target cleanup.")
    .Run(context => context.Output.Line("Working."))
    .Finally(() => Console.WriteLine("Target cleanup."));

var contextual = command.Target("contextual")
    .Description("Run work with context-aware asynchronous target cleanup.")
    .Run(context => context.Output.Line("Working with context-aware cleanup."))
    .Finally(async context =>
    {
        await Task.Yield();
        context.Output.Line("Context-aware target cleanup.");
    });

var all = command.Target("all")
    .Description("Run every cleanup callback form.")
    .DependsOn(work, contextual);

command.Finally(async () =>
{
    await Task.Yield();
    Console.WriteLine("Command cleanup.");
});

return await command.RunAsync(all, args);
