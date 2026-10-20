#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using System.Globalization;
using Sotsera.Rafter;

// Relaunch this app as the child so both file-app and companion execution demonstrate real process shutdown.
if (args is ["--child", var millisecondsText, var kind]
    && double.TryParse(millisecondsText, CultureInfo.InvariantCulture, out var milliseconds))
{
    using var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        stop.Cancel();
    };

    Console.WriteLine($"Child {kind} started.");

    try
    {
        await Task.Delay(TimeSpan.FromMilliseconds(milliseconds), stop.Token);
        Console.WriteLine($"Child {kind} finished.");
    }
    catch (OperationCanceledException) when (stop.IsCancellationRequested)
    {
        Console.Error.WriteLine($"Child {kind} acknowledged interruption.");
    }

    return 0;
}

var command = Rafter.Command(Root.Invocation)
    .Description("Start process shutdown at a deadline, then await it before running cleanup.");

var work = command.Option<TimeSpan>("work")
    .Description("How long the child work would take without interruption.")
    .Validate("Must be between zero and one minute.", value => value >= TimeSpan.Zero && value <= TimeSpan.FromMinutes(1))
    .Default(TimeSpan.FromSeconds(3));

var timeout = command.Option<TimeSpan>("timeout")
    .Description("Process deadline after launch; shutdown may extend the total duration.")
    .Validate("Must be positive and at most one minute.", value => value > TimeSpan.Zero && value <= TimeSpan.FromMinutes(1))
    .Default(TimeSpan.FromSeconds(1));

var slowCleanup = command.Option<bool>("slow-cleanup")
    .Description("Demonstrate the cleanup process reaching its own two-second deadline.")
    .Default(false);

var run = command.Target("run")
    .Description("Run the child with a process deadline.")
    .Run(async context =>
    {
        var result = await context.Process(Environment.ProcessPath)
            .Description("Run bounded child work.")
            .Arguments("--child", context.Value(work).TotalMilliseconds.ToString(CultureInfo.InvariantCulture), "work")
            .Timeout(timeout)
            .ShutdownGrace(TimeSpan.FromMilliseconds(500))
            .RunAsync();

        if (!result.Succeeded)
        {
            return;
        }

        context.Output.WriteLine("Child work completed before its deadline.");
    })
    .Cleanup("Run a cleanup process with its own deadline.", async context =>
    {
        var milliseconds = context.Value(slowCleanup) ? 10_000 : 20;
        var result = await context.Process(Environment.ProcessPath)
            .Description("Restore child resources.")
            .Arguments("--child", milliseconds.ToString(CultureInfo.InvariantCulture), "cleanup")
            .Timeout(TimeSpan.FromSeconds(2))
            .ShutdownGrace(TimeSpan.FromMilliseconds(500))
            .RunAsync();

        if (!result.Succeeded)
        {
            return;
        }

        context.Output.WriteLine("Parent cleanup completed.");
    });

return await command.RunAsync(run, args);
