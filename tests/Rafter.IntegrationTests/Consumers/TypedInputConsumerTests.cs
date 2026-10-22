using AwesomeAssertions;
using Xunit;

namespace Rafter.IntegrationTests.Consumers;

[Trait("LongRunning", "true")]
public sealed class TypedInputConsumerTests
{
    [Fact]
    public async Task Actual_options_example_runs_with_aliases_defaults_and_explicit_values()
    {
        using var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!, "examples/options.cs");
        await consumer.BuildAsync();
        var defaults = await consumer.RunAsync("--name", "Ada", "--quiet");
        defaults.ExitCode.Should().Be(0);
        defaults.StandardOutput.Should().Be("Hello, Ada!" + Environment.NewLine);
        defaults.StandardError.Should().BeEmpty();
        var aliases = await consumer.RunAsync("-n", "Ada", "-r=2", "-u", "--quiet");
        aliases.ExitCode.Should().Be(0);
        aliases.StandardOutput.Should().Be(string.Concat(Enumerable.Repeat("HELLO, ADA!" + Environment.NewLine, 2)));
        var empty = await consumer.RunAsync("--name=", "--uppercase=false", "--quiet");
        empty.ExitCode.Should().Be(0);
        empty.StandardOutput.Should().Be("Hello, !" + Environment.NewLine);
        var help = await consumer.RunAsync("--help");
        help.ExitCode.Should().Be(0);
        help.StandardOutput.Should().Contain("--name, -n, --recipient <string>").And.Contain("Required.")
            .And.Contain("Default: 1.").And.Contain("Default: false.");
        help.StandardError.Should().BeEmpty();
    }

    [Fact]
    public async Task Actual_validation_example_validates_defaults_supplied_values_and_combined_rules()
    {
        using var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!, "examples/validation.cs");
        await consumer.BuildAsync();
        var defaults = await consumer.RunAsync("--quiet");
        defaults.ExitCode.Should().Be(0);
        defaults.StandardOutput.Should().Be("Range: 1..10." + Environment.NewLine);
        var supplied = await consumer.RunAsync("--minimum=3", "--maximum=5", "--label", "[red]range[/]", "--quiet");
        supplied.ExitCode.Should().Be(0);
        supplied.StandardOutput.Should().Be("[red]range[/]" + Environment.NewLine + "Range: 3..5." + Environment.NewLine);
        var individual = await consumer.RunAsync("--minimum=-1", "--maximum=101", "--label=");
        individual.ExitCode.Should().Be(2);
        individual.StandardOutput.Should().BeEmpty();
        individual.StandardError.Should().Contain("--minimum: Must be zero or greater.")
            .And.Contain("--maximum: Must not exceed one hundred.").And.Contain("--label: Must not be blank");
        var combined = await consumer.RunAsync("--minimum=9", "--maximum=2");
        combined.ExitCode.Should().Be(2);
        combined.StandardError.Should().Contain("Minimum must not exceed maximum.");
        var help = await consumer.RunAsync("--help", "--minimum=-1");
        help.ExitCode.Should().Be(0);
        help.StandardError.Should().BeEmpty();
        help.StandardOutput.Should().Contain("Rules when supplied:").And.Contain("Combined input rules:");
    }

    [Fact]
    public async Task Compiler_rejects_optional_nonnullable_assignments_and_repeated_fluent_policies()
    {
        using var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!, "examples/minimal.cs");
        const string source = """
            #:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj
            #:property PublishAot=false
            #:property TreatWarningsAsErrors=true
            using Sotsera.Rafter;
            var command = Rafter.Command("Compile checks.");
            var text = command.Option<string>("text", "Text.");
            var count = command.Option<int>("count", "Count.");
            var flag = command.Option<bool>("flag", "Flag.");
            text.Required().Aliases("t").Validate("Rule.", _ => true).Default("value");
            count.Default(1).Validate("Rule.", _ => true).Aliases("c").Required();
            flag.Required().Required();
            command.Validate("No output API.", inputs => { inputs.WriteLine("forbidden"); return true; });
            return await command.RunAsync(args, context =>
            {
                string invalidText = context.GetValue(text);
                int invalidCount = context.GetValue(count);
                bool invalidFlag = context.GetValue(flag);
            });
            """;
        await File.WriteAllTextAsync(Path.Combine(consumer.WorkingDirectory, "examples/minimal.cs"), source,
            TestContext.Current.CancellationToken);
        var build = await consumer.CompileAsync();
        build.ExitCode.Should().NotBe(0);
        var diagnostics = build.StandardOutput + build.StandardError;
        diagnostics.Should().Contain("CS8600").And.Contain("CS0266")
            .And.Contain("'Default'").And.Contain("'Required'").And.Contain("'WriteLine'");
        // Both optional value types must fail the non-nullable assignment.
        diagnostics.Should().Contain("'int?' to 'int'").And.Contain("'bool?' to 'bool'");
    }
}
