#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Demonstrate an intentional no-op command.");

var noOp = command.Target("no-op")
    .Description("Perform no work successfully.");

return await command.RunAsync(noOp, args);
