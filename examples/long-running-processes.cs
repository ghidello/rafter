#:package Sotsera.Rafter@0.1.0

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Cancel concurrent child-process trees.")
    .Concurrency(2);

var fixture = command.RequiredOption<string>("fixture")
    .Description("Path to the deterministic process fixture.");

var graceful = command.Target("graceful")
    .Description("Run a child that acknowledges graceful cancellation.")
    .Run(context => context.Process(fixture)
        .Argument("wait")
        .Flag("--acknowledge-cancel")
        .Run());

var forced = command.Target("forced")
    .Description("Run a process tree that requires forced termination.")
    .Run(context => context.Process(fixture)
        .Argument("spawn-child")
        .Flag("--ignore-cancel")
        .Run());

var all = command.Target("all")
    .Description("Run both cancellation behaviors.")
    .DependsOn(graceful, forced);

return await command.RunAsync(all, args);
