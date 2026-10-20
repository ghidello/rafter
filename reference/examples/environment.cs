#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Read greeting inputs from the command line, environment, or defaults.");

var name = command.Option<string>("name")
    .Description("The name to greet; an explicitly empty name is allowed.")
    .FromEnvironment("RAFTER_NAME")
    .Required();

var repeat = command.Option<int>("repeat")
    .Description("How many greetings to print.")
    .FromEnvironment("RAFTER_REPEAT")
    .Validate("Must be zero or greater.", value => value >= 0)
    .Default(1);

var uppercase = command.Option<bool>("uppercase")
    .Description("Whether to uppercase the greeting; omission leaves it unchanged.")
    .FromEnvironment("RAFTER_UPPERCASE");

var hello = command.Target("hello")
    .Description("Print greetings using the selected input values.")
    .Run(context =>
    {
        var greeting = $"Hello, {context.Value(name)}!";
        var count = context.Value(repeat);

        if (context.Value(uppercase).GetValueOrDefault())
        {
            greeting = greeting.ToUpperInvariant();
        }

        for (var index = 0; index < count; index++)
        {
            context.Output.WriteLine(greeting);
        }
    });

return await command.RunAsync(hello, args);
