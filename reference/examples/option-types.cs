#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Compare optional, required, and defaulted input values.");

var name = command.Option<string>("name")
    .Description("A required name.")
    .Required();

var prefix = command.Option<string>("prefix")
    .Description("A prefix with a constant default.")
    .Default("Hello");

var count = command.Option<int>("count")
    .Description("An optional count; omission differs from zero.");

var flag = command.Option<bool>("flag")
    .Description("An optional Boolean; omission differs from false.");

var mode = command.Option<SampleMode>("mode")
    .Description("An optional mode; omission differs from Casual.");

var show = command.Target("show")
    .Description("Print the captured values, marking omitted optional inputs as unset.")
    .Run(context =>
    {
        var nameValue = context.Value(name); // string
        var prefixValue = context.Value(prefix); // string
        var countValue = context.Value(count); // int?
        var flagValue = context.Value(flag); // bool?
        var modeValue = context.Value(mode); // SampleMode?

        context.Output.WriteLine($"Name: {nameValue}");
        context.Output.WriteLine($"Prefix: {prefixValue}");
        context.Output.WriteLine($"Count: {countValue?.ToString() ?? "(unset)"}");
        context.Output.WriteLine($"Flag: {flagValue?.ToString() ?? "(unset)"}");
        context.Output.WriteLine($"Mode: {modeValue?.ToString() ?? "(unset)"}");
    });

return await command.RunAsync(show, args);

enum SampleMode
{
    Casual,
    Formal
}
