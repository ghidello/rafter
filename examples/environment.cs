#!/usr/bin/env dotnet
#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj
#:property PublishAot=false

// Syntax draft for discussion. FromEnvironment is agreed but not implemented yet.
// See docs/design/environment-inputs.md for agreed behavior and open syntax questions.
var command = Rafter.Command("Read greeting inputs from the command line, environment, or defaults.");

var name = command.Option<string>("name", "The person to greet; an empty name is allowed.")
    .FromEnvironment("RAFTER_NAME")
    .Required();

var repeat = command.Option<int>("repeat", "Number of greetings.")
    .FromEnvironment("RAFTER_REPEAT")
    .Validate("Must be zero or greater.", value => value >= 0)
    .Default(1);

var uppercase = command.Option<bool>("uppercase", "Use uppercase text when supplied as true.")
    .FromEnvironment("RAFTER_UPPERCASE");

return await command.RunAsync(args, context =>
{
    var recipient = context.GetValue(name);
    var count = context.GetValue(repeat);
    var useUppercase = context.GetValue(uppercase);

    var greeting = $"Hello, {recipient}!";

    if (useUppercase is true)
    {
        greeting = greeting.ToUpperInvariant();
    }

    for (var index = 0; index < count; index++)
    {
        context.WriteLine(greeting);
    }
});
