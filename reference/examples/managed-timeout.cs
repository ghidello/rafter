#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Request cancellation at a target deadline and await work before cleanup.")
    .Cleanup("Release shared resources.", context => context.Output.WriteLine("Command cleanup completed."));

var workDuration = command.Option<TimeSpan>("work")
    .Description("How long the managed work takes without cancellation.")
    .Validate("Must be between zero and one minute.", value => value >= TimeSpan.Zero && value <= TimeSpan.FromMinutes(1))
    .Default(TimeSpan.FromSeconds(3));

var timeout = command.Option<TimeSpan>("timeout")
    .Description("Target deadline; cancellation and settlement may extend the total duration.")
    .Default(TimeSpan.FromSeconds(1));

var ignoreCancellation = command.Option<bool>("ignore-cancellation")
    .Description("Finish the full work duration after the deadline to demonstrate cooperative cancellation.")
    .Default(false);

var work = command.Target("work")
    .Description("Run managed work with an explicit deadline.")
    .Timeout(timeout)
    .Run(async context =>
    {
        context.Output.WriteLine("Target work started.");
        var token = context.Value(ignoreCancellation) ? CancellationToken.None : context.CancellationToken;

        try
        {
            await Task.Delay(context.Value(workDuration), token);
        }
        finally
        {
            context.Output.WriteLine("Target work settled.");
        }
    })
    .Cleanup("Release local resources after work settles.", context =>
    {
        context.Output.WriteLine("Target cleanup completed.");
    });

var finish = command.Target("finish")
    .Description("Continue only when work succeeds.")
    .DependsOn(work)
    .Run(context => context.Output.WriteLine("Dependent work completed."));

return await command.RunAsync(finish, args);
