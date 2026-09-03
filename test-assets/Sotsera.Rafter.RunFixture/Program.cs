using Sotsera.Rafter;
using Sotsera.Rafter.RunFixture;

bool selfCancel = args.Any(static argument =>
    string.Equals(argument, "--self-cancel", StringComparison.Ordinal));
if (selfCancel)
{
    SignalFixture.PrepareIsolatedConsole();
}

Command command = Rafter.Command(Root.Invocation)
    .Description("Rafter integration fixture.");

RequiredOption<int> count = command.RequiredOption<int>("count")
    .Description("Number of items.");

DefaultedOption<bool> selfCancellation = command.Option<bool>("self-cancel")
    .Description("Request an isolated console cancellation signal.")
    .Default(false);

Target entry = command.Target("entry")
    .Description("Exercise parsing and presentation.")
    .Run(async context =>
    {
        if (context.Value(selfCancellation))
        {
            SignalFixture.SendCancellation();
            await Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken).ConfigureAwait(false);
        }

        Console.WriteLine(context.Value(count));
    });

return await command.RunAsync(entry, args).ConfigureAwait(false);
