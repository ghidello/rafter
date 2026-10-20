#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Validate greeting inputs before executing any targets.");

var name = command.Option<string>("name")
    .Description("The name to greet.")
    .Validate("Must contain a non-whitespace name.", value => !string.IsNullOrWhiteSpace(value))
    .Required();

var repeat = command.Option<int>("repeat")
    .Description("How many greetings to print.")
    .Validate("Must be zero or greater.", value => value >= 0)
    .Validate("Must not exceed five.", value => value <= 5)
    .Default(1);

var suffix = command.Option<string>("suffix")
    .Description("An optional greeting suffix; omit it for no suffix.")
    .Validate("Must not be blank when supplied.", value => !string.IsNullOrWhiteSpace(value));

var hello = command.Target("hello")
    .Description("Print greetings using the validated inputs.")
    .Run(context =>
    {
        var recipient = context.Value(name);
        var count = context.Value(repeat);
        var suffixValue = context.Value(suffix);
        var ending = suffixValue is null ? "" : $" {suffixValue}";

        for (var index = 0; index < count; index++)
        {
            context.Output.WriteLine($"Hello, {recipient}!{ending}");
        }
    });

return await command.RunAsync(hello, args);
