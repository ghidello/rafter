#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Exercise human and machine presentation.");

var fail = command.Option<bool>("fail")
    .Description("Request the failure presentation.")
    .Default(false);

var present = command.Target("present")
    .Description("Produce representative progress and result output.")
    .Run(async context =>
    {
        context.Output.Line("Starting presentation fixture.");
        Console.WriteLine("Managed console output from the presentation fixture.");
        context.Output.Property("fixture", "presentation");
        context.Output.Property("missing", null);
        context.Output.Property("empty", string.Empty);
        context.Output.Property("notes", "First line.\nSecond line.");
        context.Output.Property("include", new[] { "src", "tests" });
        context.Output.Property("retries", 3);
        await Task.Delay(25, context.CancellationToken);

        if (context.Value(fail))
        {
            context.Output.Error(
                "Presentation fixture failed.",
                recovery: "Run without --fail.");
            throw new InvalidOperationException("Requested failure.");
        }

        context.Output.Success("Presentation fixture passed.");
    });

return await command.RunAsync(present, args);
