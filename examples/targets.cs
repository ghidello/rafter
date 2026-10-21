#!/usr/bin/env dotnet
#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj
#:property PublishAot=false

var command = Rafter.Command("Prepare and print a greeting through dependent targets.");
var name = command.Option<string>("name", "The person to greet.").Default("Rafter");
var greeting = "";

var prepare = command.Target("prepare", "Prepare the greeting from validated inputs.")
    .Run(context =>
    {
        greeting = $"Hello, {context.GetValue(name)}!";
    });

var greet = command.Target("greet", "Print the prepared greeting.")
    .DependsOn(prepare)
    .Run(context =>
    {
        context.WriteLine(greeting);
    });

var verify = command.Target("verify", "Prepare and print the greeting.")
    .DependsOn(prepare, greet);

return await command.RunAsync(args, verify);
