#:package Sotsera.Rafter@0.1.0

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Demonstrate semantic target output.");

var diagnose = command.Target("diagnose")
    .Description("Emit every semantic output kind.")
    .Run(context =>
    {
        context.Output.Line("Inspecting repository.");
        context.Output.Warning("Optional documentation is missing.");
        context.Output.Error("A development package is missing.", recovery: "Pack it and retry.");
        context.Output.Success("Diagnostics were collected.");
        context.Output.Property("revision", "abc1234");
    });

return await command.RunAsync(diagnose, args);
