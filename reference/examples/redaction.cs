#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Demonstrate sensitive inputs using disposable example tokens.");

var token = command.Option<string>("token")
    .Description("A single-line example token; use synthetic values for this demonstration.")
    .Alias('t')
    .FromEnvironment("RAFTER_TOKEN")
    .Sensitive()
    .Validate("Must not be empty.", value => value.Length > 0)
    .Default("demo-token-only");

var fail = command.Option<bool>("fail")
    .Description("Raise an example exception to demonstrate masked exception details.")
    .Default(false);

var show = command.Target("show")
    .Description("Present the selected token through Rafter's redacted output.")
    .Run(context =>
    {
        var value = context.Value(token);
        context.Output.WriteLine($"Authorization: Bearer {value}");

        if (context.Value(fail))
        {
            throw new InvalidOperationException($"Example request failed for {value}.");
        }
    });

return await command.RunAsync(show, args);
