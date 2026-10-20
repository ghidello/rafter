#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using System.Globalization;
using Sotsera.Rafter;

// This branch is the demonstration child program. Its Console writes are relayed by the parent Rafter invocation.
if (args is ["--child", var stepsText] && int.TryParse(stepsText, out var childSteps))
{
    using var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        stop.Cancel();
    };

    try
    {
        for (var step = 1; step <= childSteps; step++)
        {
            Console.WriteLine($"Child step {step}/{childSteps}; press Ctrl+C in the parent terminal to stop.");
            await Task.Delay(TimeSpan.FromSeconds(1), stop.Token);
        }
    }
    catch (OperationCanceledException) when (stop.IsCancellationRequested)
    {
        Console.Error.WriteLine("The child handled cancellation and finished its cleanup.");
    }

    return 0;
}

var command = Rafter.Command(Root.Invocation)
    .Description("Demonstrate graceful child-process cancellation. Press Ctrl+C while the child is running.");

var steps = command.Option<int>("steps")
    .Description("Number of one-second child steps.")
    .Validate("Must be between 1 and 60.", value => value is >= 1 and <= 60)
    .Default(30);

var wait = command.Target("wait")
    .Description("Run a cancellable child and await its shutdown.")
    .Run(async context =>
    {
        var result = await context.Process(Environment.ProcessPath)
            .Description("Run the demonstration child.")
            .Argument("--child", context.Value(steps).ToString(CultureInfo.InvariantCulture))
            .RunAsync();

        if (!result.Succeeded)
        {
            return;
        }

        context.Output.WriteLine("Child work completed.");
    })
    .Cleanup("Finish parent cleanup after the process settles.", context =>
        context.Output.WriteLine("Parent cleanup completed."));

return await command.RunAsync(wait, args);
