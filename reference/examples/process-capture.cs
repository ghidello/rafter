#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using System.Text.Json;
using Sotsera.Rafter;

// The child emits data; the parent decides whether to capture it silently or echo it through Rafter.
if (args is ["--child", var childScenario])
{
    if (childScenario == "Overflow")
    {
        Console.Write(new string('x', 2048));
        return 0;
    }

    if (childScenario == "Failure")
    {
        Console.Error.WriteLine("The demonstration child could not produce a greeting.");
        return 7;
    }

    Console.WriteLine("""{"name":"Ada","message":"Hello, Ada!"}""");
    return 0;
}

var command = Rafter.Command(Root.Invocation)
    .Description("Capture structured child output within explicit bounds, with optional live echo.");

var scenario = command.Option<CaptureScenario>("scenario")
    .Description("Choose successful JSON, capture overflow, or a child failure.")
    .Default(CaptureScenario.Success);

var limit = command.Option<int>("limit")
    .Description("Maximum UTF-16 characters retained in each stream.")
    .Validate("Must be positive.", value => value > 0)
    .Default(1024);

var echo = command.Option<bool>("echo")
    .Description("Also display the captured child output live.")
    .Default(false);

var inspect = command.Target("inspect")
    .Description("Capture the child result and read its JSON.")
    .Run(async context =>
    {
        var result = await context.Process(Environment.ProcessPath)
            .Description("Produce a greeting as structured text.")
            .Argument("--child", context.Value(scenario).ToString())
            .Capture(maxCharactersPerStream: context.Value(limit), echo: context.Value(echo))
            .Timeout(TimeSpan.FromSeconds(10))
            .RunAsync();

        if (!result.Succeeded)
        {
            return;
        }

        using var document = JsonDocument.Parse(result.Capture!.StandardOutput);
        var name = document.RootElement.GetProperty("name").GetString();
        var message = document.RootElement.GetProperty("message").GetString();
        context.Output.WriteLine($"Captured greeting for {name}: {message}");
    });

return await command.RunAsync(inspect, args);

enum CaptureScenario
{
    Success,
    Overflow,
    Failure
}
