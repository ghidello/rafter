#:package Sotsera.Rafter@0.1.0

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Demonstrate a branching target graph.")
    .Concurrency(2);

var restore = command.Target("restore")
    .Description("Restore shared dependencies.")
    .Run(context => context.Output.Line("Restored."));

var buildServer = command.Target("build-server")
    .Description("Build the server.")
    .DependsOn(restore)
    .Run(context => context.Output.Line("Built server."));

var buildClient = command.Target("build-client")
    .Description("Build the client.")
    .DependsOn(restore)
    .Run(context => context.Output.Line("Built client."));

var verify = command.Target("verify")
    .Description("Verify every build output.")
    .DependsOn(buildServer, buildClient);

return await command.RunAsync(verify, args);
