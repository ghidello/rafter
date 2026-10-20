#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Print a greeting using typed command-line options.");

var name = command.Option<string>("name")
    .Description("The name to greet.")
    .Alias('n')
    .Required();

var repeat = command.Option<int>("repeat")
    .Description("How many greetings to print.")
    .Alias('r')
    .Validate("Must be zero or greater.", value => value >= 0)
    .Default(1);

var uppercase = command.Option<bool>("uppercase")
    .Description("Print the greeting in uppercase.")
    .Alias('u')
    .Default(false);

var hello = command.Target("hello")
    .Description("Print the requested greetings.")
    .Run(context =>
    {
        var greeting = $"Hello, {context.Value(name)}!";
        var count = context.Value(repeat);

        if (context.Value(uppercase))
        {
            greeting = greeting.ToUpperInvariant();
        }

        for (var index = 0; index < count; index++)
        {
            context.Output.WriteLine(greeting);
        }
    });

return await command.RunAsync(hello, args);
