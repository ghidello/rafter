#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Report concurrent failures deterministically.")
    .Concurrency(2);

var first = command.Target("first")
    .Description("Fail second in time but first in plan order.")
    .Run(async context =>
    {
        await Task.Delay(100, context.CancellationToken);
        throw new InvalidOperationException("First planned failure.");
    });

var second = command.Target("second")
    .Description("Fail first in time but second in plan order.")
    .Run(async context =>
    {
        await Task.Delay(10, context.CancellationToken);
        throw new InvalidOperationException("Second planned failure.");
    });

var all = command.Target("all")
    .Description("Run both failing branches.")
    .DependsOn(first, second);

return await command.RunAsync(all, args);
