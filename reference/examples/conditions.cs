#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Skip optional work while allowing dependent targets to continue.");

var release = command.Option<bool>("release")
    .Description("Include the release step.")
    .Default(false);

var prepare = command.Target("prepare")
    .Description("Prepare inputs before evaluating the release condition.")
    .Run(context => context.Output.WriteLine("Inputs prepared."));

var publish = command.Target("publish")
    .Description("Demonstrate optional release work.")
    .DependsOn(prepare)
    .When("Run when release is requested.", context => context.Value(release))
    .Run(context => context.Output.WriteLine("Release step completed."));

var finish = command.Target("finish")
    .Description("Finish after the optional release target, even if it was skipped.")
    .DependsOn(publish)
    .Run(context => context.Output.WriteLine("Workflow completed."));

return await command.RunAsync(finish, args);
