#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Keep results separate from information, warnings, and expected failures.");

var failure = command.Option<FailureStage>("fail")
    .Description("Demonstrate an expected failure in a condition, body, or cleanup.")
    .Default(FailureStage.None);

command.Cleanup("Release shared resources.", context =>
{
    context.Diagnostics.Info("Shared resources released.");

    if (context.Value(failure) == FailureStage.CommandCleanup)
    {
        context.Fail("Shared resource release could not complete.");
        return;
    }
});

var work = command.Target("work")
    .Description("Check prerequisites and produce a result.")
    .When("Run after checking the prerequisite.", context =>
    {
        context.Diagnostics.Info("Checking the prerequisite.");

        if (context.Value(failure) == FailureStage.Condition)
        {
            context.Fail("The prerequisite is unavailable.");
            return false;
        }

        return true;
    })
    .Run(context =>
    {
        context.Diagnostics.Info("Preparing the result.");
        context.Diagnostics.Warning("This is a demonstration warning; work can continue.");

        if (context.Value(failure) == FailureStage.Body)
        {
            context.Fail("The result could not be produced.");
            return;
        }

        context.Output.WriteLine("Work completed.");
    })
    .Cleanup("Release local resources.", context =>
    {
        context.Diagnostics.Info("Local resources released.");

        if (context.Value(failure) == FailureStage.TargetCleanup)
        {
            context.Fail("Local resource release could not complete.");
            return;
        }
    });

var finish = command.Target("finish")
    .Description("Continue after successful work and local cleanup.")
    .DependsOn(work)
    .Run(context => context.Output.WriteLine("Dependent work completed."));

return await command.RunAsync(finish, args);

enum FailureStage
{
    None,
    Condition,
    Body,
    TargetCleanup,
    CommandCleanup
}
