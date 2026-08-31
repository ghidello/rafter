#:package Sotsera.Rafter@0.1.0

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Demonstrate synchronous and asynchronous target conditions.");

var enabled = command.Option<bool>("enabled")
    .Description("Enable conditional work.")
    .Default(false);

var available = true;

var authored = command.Target("authored")
    .Description("Use a boolean evaluated while the command is authored.")
    .When(available)
    .Run(context => context.Output.Line("Authored condition passed."));

var deferred = command.Target("deferred")
    .Description("Evaluate a context-free function when the target becomes eligible.")
    .When(() => available)
    .Run(context => context.Output.Line("Deferred condition passed."));

var contextual = command.Target("contextual")
    .Description("Resolve an authored option from the target context.")
    .When(context => context.Value(enabled))
    .Run(context => context.Output.Line("Context-aware condition passed."));

var asynchronous = command.Target("asynchronous")
    .Description("Evaluate an asynchronous context-aware condition.")
    .When(async context =>
    {
        await Task.Yield();
        return context.Value(enabled);
    })
    .Run(context => context.Output.Line("Asynchronous condition passed."));

var all = command.Target("all")
    .Description("Evaluate every supported condition form.")
    .DependsOn(authored, deferred, contextual, asynchronous);

return await command.RunAsync(all, args);
