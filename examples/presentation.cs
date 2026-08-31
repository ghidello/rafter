#:package Sotsera.Rafter@0.1.0

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
