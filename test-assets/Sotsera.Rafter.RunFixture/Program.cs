using Sotsera.Rafter;

Command command = Rafter.Command(Root.Invocation)
    .Description("Phase 3 integration fixture.");

RequiredOption<int> count = command.RequiredOption<int>("count")
    .Description("Number of items.");

Target entry = command.Target("entry")
    .Description("Exercise parsing and presentation.")
    .Run(context => Console.WriteLine(context.Value(count)));

return await command.RunAsync(entry, args).ConfigureAwait(false);
