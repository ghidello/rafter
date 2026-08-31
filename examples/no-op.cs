#:package Sotsera.Rafter@0.1.0

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Demonstrate an intentional no-op command.");

#pragma warning disable RAFTER005 // This no-op is intentional and is the selected command entry point.
var noOp = command.Target("no-op")
    .Description("Perform no work successfully.");
#pragma warning restore RAFTER005

return await command.RunAsync(noOp, args);
