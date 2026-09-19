using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation).Description("Verify the packaged runtime.");
var message = command.Option<string>("message").Description("Message to emit.").Default("Package consumer passed.");
var entry = command.Target("verify").Description("Bind and execute through the public API.")
    .Run(context => context.Output.Success(context.Value(message)));

return await command.RunAsync(entry, args);
