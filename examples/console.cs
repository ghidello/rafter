#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Attribute managed console writes to their targets.")
    .Concurrency(2);

var immediate = command.Target("immediate")
    .Description("Write from a synchronous context-free callback.")
    .Run(() => Console.WriteLine("immediate: stdout"));

var first = command.Target("first")
    .Description("Write across an ordinary await.")
    .Run(async () =>
    {
        Console.WriteLine("first: stdout");
        await Task.Yield();
        Console.Error.WriteLine("first: stderr");
    });

var second = command.Target("second")
    .Description("Write across Task.Run and ConfigureAwait(false).")
    .Run(async () =>
    {
        await Task.Run(() => Console.WriteLine("second: task-run"));
        await Task.Delay(10).ConfigureAwait(false);
        Console.Error.WriteLine("second: configure-await-false");
    });

var all = command.Target("all")
    .Description("Run every console-writing target.")
    .DependsOn(immediate, first, second);

return await command.RunAsync(all, args);
