#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Verify that sensitive values are redacted from every output path.");

var fixture = command.RequiredOption<string>("fixture")
    .Description("Path to the deterministic process fixture.");

var secret = command.RequiredOption<string>("synthetic-secret")
    .Description("Disposable synthetic value used only to verify redaction.")
    .FromEnvironment("RAFTER_SYNTHETIC_SECRET")
    .Sensitive();

var verify = command.Target("verify")
    .Description("Emit the synthetic value through managed and child-process output.")
    .Run(async context =>
    {
        var value = context.Value(secret);

        context.Output.Line($"Semantic output: {value}");
        Console.WriteLine($"Console output: {value}");
        Console.Error.WriteLine($"Console error: {value}");

        await context.Process(fixture)
            .Argument("emit")
            .Option("--stdout", secret)
            .Option("--stderr", secret)
            .Environment(environment => environment
                .SetSensitive("RAFTER_CHILD_SECRET", secret))
            .Run();
    });

return await command.RunAsync(verify, args);
