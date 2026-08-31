#:package Sotsera.Rafter@0.1.0

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Attribute managed console writes to their targets.")
    .Concurrency(2);

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
    .Description("Run both console-writing targets.")
    .DependsOn(first, second);

return await command.RunAsync(all, args);
