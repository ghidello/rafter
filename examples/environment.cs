#:package Sotsera.Rafter@0.1.0

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Demonstrate ordered child-process environment changes.");

var fixture = command.RequiredOption<string>("fixture")
    .Description("Path to the deterministic process fixture.");

var inspect = command.Target("inspect")
    .Description("Launch the fixture with an isolated environment.")
    .Run(context => context.Process(fixture)
        .Argument("environment")
        .Environment(environment => environment
            .Set("DISCARDED", "value")
            .Clear()
            .Set("PATH", Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Set("CI", "true")
            .Set("EMPTY", string.Empty)
            .Unset("RAFTER_EXAMPLE_REMOVE"))
        .Run());

return await command.RunAsync(inspect, args);
