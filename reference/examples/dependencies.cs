#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Run dependent targets sequentially, sharing common work.");

var prepare = command.Target("prepare")
    .Description("Prepare inputs once for both branches.")
    .Run(context => context.Output.WriteLine("Inputs prepared."));

var build = command.Target("build")
    .Description("Build the prepared inputs.")
    .DependsOn(prepare)
    .Run(context => context.Output.WriteLine("Build completed."));

var test = command.Target("test")
    .Description("Test the prepared inputs.")
    .DependsOn(prepare)
    .Run(context => context.Output.WriteLine("Tests completed."));

var all = command.Target("all")
    .Description("Run both branches in declaration order.")
    .DependsOn(build, test);

return await command.RunAsync(all, args);
