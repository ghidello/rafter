using Sotsera.Rafter;

internal static class Scenario
{
    public static async Task<int> RunAsync(string[] args)
    {
        var scenario = args.FirstOrDefault() ?? "sync";
        var controls = args.Skip(1).ToArray();
        var command = Rafter.Command("Print a greeting.");
        var output = Console.Out;
        var error = Console.Error;
        var cwd = Directory.GetCurrentDirectory();
        int result;

        if (scenario == "invalid")
        {
            result = await Rafter.Command(" ").RunAsync(controls, context => context.WriteLine("NOT EXECUTED"));
        }
        else if (scenario == "async")
        {
            result = await command.RunAsync(controls, async context =>
            {
                await Task.Delay(25, context.CancellationToken);
                context.WriteLine("Async body finished.");
            });
        }
        else if (scenario == "failure")
        {
            result = await command.RunAsync(controls, (Action<RafterContext>)(context =>
            {
                context.WriteLine("Before failure.");
                try
                {
                    throw new ArgumentException("Inner [red]literal[/] cause.");
                }
                catch (Exception inner)
                {
                    throw new InvalidOperationException("Outer cause.", inner);
                }
            }));
        }
        else if (scenario == "cancel")
        {
            result = await command.RunAsync(controls, async context =>
            {
                context.WriteLine("Ready for Ctrl+C.");
                try
                {
                    await Task.Delay(Timeout.Infinite, context.CancellationToken);
                }
                finally
                {
                    context.WriteLine("Managed cleanup finished.");
                }
            });
        }
        else
        {
            result = await command.RunAsync(controls, context =>
                context.WriteLine("  Hello [red]literal[/].\n\n界面 cafe\u0301"));
        }

        if (!ReferenceEquals(output, Console.Out) || !ReferenceEquals(error, Console.Error)
            || cwd != Directory.GetCurrentDirectory())
        {
            throw new InvalidOperationException("Rafter changed host state.");
        }
        // Optional test evidence for terminal hosts that translate a native exit code after Ctrl+C.
        if (Environment.GetEnvironmentVariable("RAFTER_FIXTURE_RESULT") is { Length: > 0 } resultPath)
        {
            await File.WriteAllTextAsync(resultPath, result.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        return result;
    }
}
