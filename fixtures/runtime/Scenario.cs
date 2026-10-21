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

        if (scenario is "targets" or "targets-failure")
        {
            var name = command.Option<string>("name", "Name.").Default("Rafter");
            var first = command.Target("first", "Prepare a result.").Run(context =>
            {
                context.WriteLine("First: " + context.GetValue(name));
                if (scenario == "targets-failure")
                {
                    throw new InvalidOperationException("Target failed after output.");
                }
            });
            var second = command.Target("second", "Await and write the next result.").DependsOn(first)
                .Run(async context =>
                {
                    await Task.Yield();
                    context.WriteLine("Second: " + context.GetValue(name));
                });
            var group = command.Target("group", "Run both bodies.").DependsOn(first, second);
            result = await command.RunAsync(controls, group, second, first);
        }
        else if (scenario == "process")
        {
            result = await command.RunAsync(controls, async context =>
            {
                var process = await context.Process("dotnet").Arguments("--version").RunAsync();
                context.WriteLine($"Process result: {process.ExitCode}:{process.Succeeded}");
            });
        }
        else if (scenario == "typed")
        {
            var text = command.Option<string>("text", "Optional text.");
            var number = command.Option<int>("number", "Optional integer.");
            var flag = command.Option<bool>("flag", "Optional Boolean.");
            var name = command.Option<string>("name", "Required text.").Required()
                .Aliases("n").Validate("Nonempty.", value => value.Length > 0);
            var count = command.Option<int>("count", "Defaulted integer.").Default(4)
                .Aliases("c").Validate("Positive.", value => value > 0);
            var enabled = command.Option<bool>("enabled", "Required Boolean.").Required();
            command.Validate("Types are available during validation.", inputs =>
            {
                string? optionalText = inputs.GetValue(text);
                int? optionalNumber = inputs.GetValue(number);
                bool? optionalFlag = inputs.GetValue(flag);
                string requiredName = inputs.GetValue(name);
                int defaultCount = inputs.GetValue(count);
                bool requiredFlag = inputs.GetValue(enabled);
                return optionalText is null && optionalNumber is null && optionalFlag is null
                    && requiredName.Length > 0 && defaultCount > 0 && !requiredFlag;
            });
            result = await command.RunAsync(controls, context =>
            {
                string? optionalText = context.GetValue(text);
                int? optionalNumber = context.GetValue(number);
                bool? optionalFlag = context.GetValue(flag);
                string requiredName = context.GetValue(name);
                int defaultCount = context.GetValue(count);
                bool requiredFlag = context.GetValue(enabled);
                context.WriteLine($"{requiredName}:{defaultCount}:{requiredFlag}:{optionalText}:{optionalNumber}:{optionalFlag}");
            });
        }
        else if (scenario == "invalid")
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
