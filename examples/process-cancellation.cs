#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Cancel concurrent child-process trees.")
    .Concurrency(2);

var fixture = command.RequiredOption<string>("fixture")
    .Description("Path to the deterministic process fixture.");

var direct = command.Target("direct")
    .Description("Cancel a directly tracked child process.")
    .Run(context => context.Process(fixture)
        .Argument("wait")
        .Run()
    );

var forced = command.Target("forced")
    .Description("Run a process tree that requires forced termination.")
    .Run(context => context.Process(fixture)
        .Argument("spawn-child")
        .Run()
    );

var all = command.Target("all")
    .Description("Run both cancellation behaviors.")
    .DependsOn(direct, forced);

return await command.RunAsync(all, args);
