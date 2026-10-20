#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Give target and command cleanup independent cooperative deadlines.");

var workDuration = command.Option<TimeSpan>("cleanup-work")
    .Description("How long target cleanup takes without cancellation.")
    .Validate("Must be between zero and one minute.", value => value >= TimeSpan.Zero && value <= TimeSpan.FromMinutes(1))
    .Default(TimeSpan.FromSeconds(3));

var targetTimeout = command.Option<TimeSpan>("cleanup-timeout")
    .Description("Deadline for target cleanup, starting when that cleanup begins.")
    .Default(TimeSpan.FromSeconds(1));

var sharedDuration = command.Option<TimeSpan>("command-cleanup-work")
    .Description("How long command cleanup takes without cancellation.")
    .Validate("Must be between zero and one minute.", value => value >= TimeSpan.Zero && value <= TimeSpan.FromMinutes(1))
    .Default(TimeSpan.FromMilliseconds(20));

var sharedTimeout = command.Option<TimeSpan>("command-cleanup-timeout")
    .Description("Independent deadline for command cleanup.")
    .Default(TimeSpan.FromSeconds(1));

var failBody = command.Option<bool>("fail-body")
    .Description("Demonstrate retention of a body exception alongside cleanup timeouts.")
    .Default(false);

command.Cleanup("Release shared resources even after target cleanup times out.", sharedTimeout, async context =>
{
    try
    {
        await Task.Delay(context.Value(sharedDuration), context.CancellationToken);
    }
    finally
    {
        context.Output.WriteLine("Command cleanup settled.");
    }
});

var work = command.Target("work")
    .Description("Run ordinary work before independently bounded cleanup.")
    .Run(context =>
    {
        context.Output.WriteLine("Body entered.");

        if (context.Value(failBody))
        {
            throw new IOException("Demonstration body failure.");
        }
    })
    .Cleanup("Release local resources.", targetTimeout, async context =>
    {
        try
        {
            await Task.Delay(context.Value(workDuration), context.CancellationToken);
        }
        finally
        {
            context.Output.WriteLine("Target cleanup settled.");
        }
    });

return await command.RunAsync(work, args);
