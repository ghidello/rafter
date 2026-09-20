using System.Text.Json;

namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class PropertyPresentationTests
{
    private static readonly string[] IncludedPaths = ["src", "tests"];
    private static readonly string[] LongPaths = ["source/library", "tests/integration", "artifacts/packages"];

    public static IEnumerable<object[]> FixtureCases()
    {
        foreach (string scenario in new[]
        {
            "presentation-success", "presentation-failure", "narrow-properties", "semantic-scopes", "console-continuation",
            "concurrent-attribution",
        })
        {
            using JsonDocument fixture = ReadFixture(scenario);
            foreach (JsonElement document in fixture.RootElement.GetProperty("documents").EnumerateArray())
            {
                yield return [scenario, document.GetProperty("profile").GetProperty("name").GetString()!];
            }
        }
    }

    [Theory]
    [MemberData(nameof(FixtureCases))]
    public async Task SemanticOutputMatchesAcceptedFixtures(string scenario, string profileName)
    {
        using JsonDocument fixture = ReadFixture(scenario);
        JsonElement document = fixture.RootElement.GetProperty("documents").EnumerateArray()
            .Single(item => string.Equals(item.GetProperty("profile").GetProperty("name").GetString(),
                profileName, StringComparison.Ordinal));
        JsonElement profile = document.GetProperty("profile");
        OutputCapabilities capabilities = new(false, profile.GetProperty("rich").GetBoolean(), true,
            profile.GetProperty("color").GetBoolean(), profile.GetProperty("unicode").GetBoolean(),
            profile.GetProperty("live").GetBoolean(), profile.GetProperty("width").GetInt32());
        TerminalSurfaceWriter stdout = new();
        TerminalSurfaceWriter stderr = new();
        Command command = PhaseFiveTestSupport.CreateCommand(concurrency: 2);
        command.InvocationServicesFactory = () => new InvocationServices(_ => null, stdout, stderr,
            capabilities, capabilities, "test-command");
        bool presentation = scenario.StartsWith("presentation-", StringComparison.Ordinal);
        Target target = scenario is "concurrent-attribution" ? DefineConcurrent(command)
            : command.Target(presentation ? "present" : "work").Description("Present.")
                .Run(context => Present(context.Output, scenario));
        if (scenario is "semantic-scopes")
        {
            command.Finally(context => Present(context.Output, scenario));
        }

        (await command.RunAsync(target, [], TestContext.Current.CancellationToken))
            .Should().Be(scenario is "presentation-failure" ? 1 : 0);

        stdout.ToString().Should().Be(document.GetProperty("stdout").GetString());
        stderr.ToString().Should().Be(document.GetProperty("stderr").GetString());
    }

    [Theory]
    [InlineData(false, "a\"b")]
    [InlineData(true, "a\"b")]
    [InlineData(false, "top\nsecret")]
    [InlineData(true, "top\nsecret")]
    [InlineData(false, "tab\tsecret")]
    [InlineData(true, "tab\tsecret")]
    public async Task PropertiesRedactDecodedValuesBeforeFormatting(bool rich, string secret)
    {
        StringWriter sink = new();
        OutputCapabilities capabilities = rich ? new(false, true, true, false, true, false, 80) : OutputCapabilities.Plain;
        InvocationOutput invocation = new(sink, sink, capabilities, capabilities, TextRedactor.Create([secret]));
        RafterOutput output = new(invocation, new OutputScope("work"));

        output.Property("value", secret);
        output.Property("values", new[] { secret });
        await invocation.SealAsync();

        invocation.Failure.Should().BeNull();
        sink.ToString().Should().Be(rich
            ? "[work] value: \"<redacted>\"\n[work] values: [\"<redacted>\"]\n"
            : "[work] value=\"<redacted>\"\n[work] values=[\"<redacted>\"]\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MultilineSemanticTextRetainsAttributionAndEscapesTerminalControls(bool rich)
    {
        OutputCapabilities capabilities = rich ? new(false, true, true, false, true, false, 80) : OutputCapabilities.Plain;
        string rendered = OutputPresentation.Render(new OutputEvent("work", OutputKind.Line, "first\r\n\u001b[31msecond\rthird"),
            capabilities);
        rendered.Should().Be("[work] first\n[work] \\u001b[31msecond\n[work] third\n");
    }

    private static JsonDocument ReadFixture(string name)
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "PresentationFixtures", name + ".json")));

    private static Target DefineConcurrent(Command command)
    {
        TaskCompletionSource first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource second = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Target alpha = command.Target("alpha").Description("Alpha.").Run(async context =>
        {
            await first.Task.WaitAsync(TimeSpan.FromSeconds(5), context.CancellationToken).ConfigureAwait(false);
            context.Output.Line("Second delivered.");
            context.Output.Warning("Optional step omitted.");
            second.SetResult();
        });
        Target beta = command.Target("beta").Description("Beta.").Run(async context =>
        {
            context.Output.Line("First delivered.");
            first.SetResult();
            await second.Task.WaitAsync(TimeSpan.FromSeconds(5), context.CancellationToken).ConfigureAwait(false);
            context.Output.Success("Complete.");
        });
        return command.Target("entry").Description("Entry.").DependsOn(alpha, beta);
    }

    private static void Present(RafterOutput output, string scenario)
    {
        if (scenario is "console-continuation")
        {
            Console.Write("part <redacted>");
            output.Line("Barrier.");
            Console.WriteLine("rest");
            return;
        }
        if (scenario is "narrow-properties")
        {
            output.Property("paths", LongPaths);
            output.Property("special", "tab\tand\u001b[31m");
            return;
        }
        if (scenario is "semantic-scopes")
        {
            output.Line("A line.");
            output.Success("A result.");
            output.Property("value", "a\"b");
            output.Warning("Optional input is missing.");
            output.Error("A package is missing.", "Pack it and retry.");
            return;
        }
        output.Line("Starting presentation fixture.");
        Console.WriteLine("Managed console output from the presentation fixture.");
        output.Property("fixture", "presentation");
        output.Property("missing", null);
        output.Property("empty", string.Empty);
        output.Property("notes", "First line.\nSecond line.");
        output.Property("include", IncludedPaths);
        output.Property("retries", 3);
        if (scenario is "presentation-failure")
        {
            output.Error("Presentation fixture failed.", "Run without --fail.");
            throw new InvalidOperationException("Requested failure.");
        }
        output.Success("Presentation fixture passed.");
    }
}
