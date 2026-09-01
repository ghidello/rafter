#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Demonstrate managed failures and recovery guidance.")
    .Concurrency(2);

var fail = command.Target("fail")
    .Description("Fail with an actionable diagnostic.")
    .Run(context =>
    {
        context.Output.Error(
            "Verification failed.",
            recovery: "Correct the fixture input and retry.");
        throw new InvalidOperationException("The verification fixture failed.");
    });

var blocked = command.Target("blocked")
    .Description("Run only after the failing target succeeds.")
    .DependsOn(fail)
    .Run(context => context.Output.Line("This target should be blocked."));

var independent = command.Target("independent")
    .Description("Settle independently after another branch fails.")
    .Run(async context =>
    {
        await Task.Delay(50, context.CancellationToken);
        context.Output.Success("Independent work settled.");
    });

var all = command.Target("all")
    .Description("Run every failure scenario branch.")
    .DependsOn(blocked, independent);

return await command.RunAsync(all, args);
