#:package Sotsera.Rafter@0.1.0

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Demonstrate an expected managed failure.");

var verify = command.Target("verify-invalid-input")
    .Description("Verify that invalid input is rejected.")
    .ExpectsFailure<InvalidDataException>()
    .Run(() =>
    {
        throw new InvalidDataException("Expected invalid fixture.");
    });

return await command.RunAsync(verify, args);
