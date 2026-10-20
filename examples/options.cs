#!/usr/bin/env dotnet
#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj
#:property PublishAot=false

using Sotsera.Rafter;

var command = Rafter.Command("Print a personalized greeting.");

var name = command.Option<string>("name", "The person to greet.")
    .Aliases("n", "recipient")
    .Required();

var repeat = command.Option<int>("repeat", "Number of greetings.")
    .Aliases("r")
    .Default(1);

var uppercase = command.Option<bool>("uppercase", "Use uppercase text.")
    .Aliases("u")
    .Default(false);

return await command.RunAsync(args, context =>
{
    var recipient = context.GetValue(name);
    var count = context.GetValue(repeat);
    var useUppercase = context.GetValue(uppercase);

    var greeting = $"Hello, {recipient}!";

    if (useUppercase)
    {
        greeting = greeting.ToUpperInvariant();
    }

    for (var index = 0; index < count; index++)
    {
        context.WriteLine(greeting);
    }
});
