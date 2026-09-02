using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Sotsera.Rafter.Tests;

public sealed class PhaseThreePresentationTests
{
    [Fact]
    public async Task HelpIsSuccessfulSideEffectFreeAndOrganizedByAudience()
    {
        int environmentReads = 0;
        int validatorCalls = 0;
        int conditionCalls = 0;
        Command command = NewCommand();
        command.Option<string>("configuration")
            .Description("Build configuration.")
            .Alias('c')
            .FromEnvironment("CONFIGURATION")
            .Default("Release")
            .Validate(_ =>
            {
                validatorCalls++;
                return true;
            }, "Valid configuration.");
        command.Option<string>("token").Description("Token.").Default("hidden-default").Sensitive();
        Target prepare = command.Target("prepare").Description("Prepare.");
        Target entry = command.Target("build")
            .Description("Build everything.")
            .DependsOn(prepare)
            .When(() =>
            {
                conditionCalls++;
                return true;
            });
        StringWriter output = new(CultureInfo.InvariantCulture);
        StringWriter error = new(CultureInfo.InvariantCulture);
        command.InvocationServicesFactory = () => new InvocationServices(
            _ =>
            {
                environmentReads++;
                return "environment-value";
            },
            output,
            error,
            false,
            false,
            "build-script");

        int exitCode = await command.RunAsync(entry, ["--bad=value", "--plain", "--help"]);

        exitCode.Should().Be(0);
        environmentReads.Should().Be(0);
        validatorCalls.Should().Be(0);
        conditionCalls.Should().Be(0);
        error.ToString().Should().BeEmpty();
        output.ToString().Should().ContainAll(
            "Usage",
            "build-script [options]",
            "Command options",
            "Common options",
            "Targets",
            "configuration",
            "environment: CONFIGURATION",
            "default: \"Release\"",
            "build [entry, conditional, aggregate]");
        output.ToString().Should().NotContain("hidden-default");
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public async Task ExactHelpWinsOverMalformedInputButAttachedTextDoesNotActivateHelp(string helpToken)
    {
        Command command = NewCommand();
        Target entry = command.Target("entry").Description("Entry.");
        StringWriter output = new(CultureInfo.InvariantCulture);
        StringWriter error = new(CultureInfo.InvariantCulture);
        ConfigureServices(command, output, error);

        int helpExit = await command.RunAsync(entry, ["--unknown=secret", helpToken]);

        helpExit.Should().Be(0);
        output.ToString().Should().Contain("Usage");
        error.ToString().Should().BeEmpty();

        output.GetStringBuilder().Clear();
        int malformedExit = await command.RunAsync(entry, ["--help=secret"]);

        malformedExit.Should().Be(2);
        output.ToString().Should().BeEmpty();
        error.ToString().Should().Contain("Malformed common option").And.NotContain("secret");
    }

    [Fact]
    public async Task BindingBarrierPreventsEveryGraphCallbackFromRunning()
    {
        int callbacks = 0;
        Command command = NewCommand();
        command.RequiredOption<int>("count").Description("Count.");
        Target entry = command.Target("entry")
            .Description("Entry.")
            .When(() =>
            {
                callbacks++;
                return true;
            })
            .Run(() => callbacks++)
            .Finally(() => callbacks++);
        command.Finally(() => callbacks++);
        StringWriter output = new(CultureInfo.InvariantCulture);
        StringWriter error = new(CultureInfo.InvariantCulture);
        ConfigureServices(command, output, error);

        int rejectedExit = await command.RunAsync(entry, ["--count=invalid"]);
        await FluentActions.Awaiting(() => command.RunAsync(entry, ["--count=1"]))
            .Should().ThrowAsync<NotSupportedException>();

        rejectedExit.Should().Be(2);
        callbacks.Should().Be(0);
    }

    [Fact]
    public async Task InputFailureWritesErrorsAndOneSafeHelpBlockToStandardError()
    {
        Command command = NewCommand();
        command.RequiredOption<int>("count").Description("Count.");
        Target entry = command.Target("entry").Description("Entry.");
        StringWriter output = new(CultureInfo.InvariantCulture);
        StringWriter error = new(CultureInfo.InvariantCulture);
        ConfigureServices(command, output, error);

        int exitCode = await command.RunAsync(entry, ["--count=bad", "unexpected"]);

        exitCode.Should().Be(2);
        output.ToString().Should().BeEmpty();
        error.ToString().Should().Contain("Invalid value \"bad\" for '--count'.");
        error.ToString().Should().NotContain("unexpected");
        Count(error.ToString(), "Usage").Should().Be(1);
    }

    [Fact]
    public async Task ModelFailureDoesNotRenderHelp()
    {
        Command command = global::Sotsera.Rafter.Rafter.Command(Root.Invocation);
        Target entry = command.Target("entry").Description("Entry.");
        StringWriter output = new(CultureInfo.InvariantCulture);
        StringWriter error = new(CultureInfo.InvariantCulture);
        ConfigureServices(command, output, error);

        int exitCode = await command.RunAsync(entry, ["--help"]);

        exitCode.Should().Be(2);
        output.ToString().Should().BeEmpty();
        error.ToString().Should().Contain("requires a description");
        error.ToString().Should().NotContain("Usage");
    }

    [Fact]
    public async Task RichVerificationFailsClosedWhenAnsiReintroducesARegisteredPattern()
    {
        Command command = NewCommand();
        command.RequiredOption<string>("token").Description("Token.").Sensitive();
        Target entry = command.Target("entry").Description("Entry.");
        StringWriter output = new(CultureInfo.InvariantCulture);
        StringWriter error = new(CultureInfo.InvariantCulture);
        command.InvocationServicesFactory = () => new InvocationServices(
            _ => null,
            output,
            error,
            true,
            true,
            "test-command");

        int exitCode = await command.RunAsync(entry, [string.Concat("--token=", '\u001B'), "unexpected"]);

        exitCode.Should().Be(1);
        output.ToString().Should().BeEmpty();
        error.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task RichHelpUsesSpectreAnsiRenderingWhenTheOutputSupportsIt()
    {
        Command command = NewCommand();
        Target entry = command.Target("entry").Description("Entry.");
        StringWriter output = new(CultureInfo.InvariantCulture);
        StringWriter error = new(CultureInfo.InvariantCulture);
        command.InvocationServicesFactory = () => new InvocationServices(
            _ => null,
            output,
            error,
            true,
            true,
            "test-command");

        int exitCode = await command.RunAsync(entry, ["--help"]);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("\u001B[");
        error.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task PlainHelpHasAStableInteractiveNeutralSnapshot()
    {
        Command command = NewCommand();
        Target entry = command.Target("entry").Description("Entry.");
        StringWriter output = new(CultureInfo.InvariantCulture);
        StringWriter error = new(CultureInfo.InvariantCulture);
        ConfigureServices(command, output, error);

        int exitCode = await command.RunAsync(entry, ["--plain", "--help"]);

        exitCode.Should().Be(0);
        output.ToString().Should().Be(
            """
            Test command.

            Usage
              test-command [options]

            Command options
              (none)

            Common options
              --plain
                  Use deterministic plain-text output.
              -h, --help
                  Show command help.

            Targets
              Targets describe the contained execution graph; they are not command-line selections.
              entry [entry, no work]
                  Entry.

            """.ReplaceLineEndings("\n"));
        error.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(20, false)]
    [InlineData(21, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(20, true)]
    [InlineData(21, true)]
    public async Task InputReportSerializationIsBoundedInPlainAndRichModes(int diagnosticCount, bool rich)
    {
        Command command = NewCommand();
        Target entry = command.Target("entry").Description("Entry.");
        CommandModel.CommandDefinition model = command.FreezeResult.Model!;
        var diagnostics = Enumerable.Range(0, diagnosticCount)
            .Select(index => new BindingEngine.InputDiagnostic(
                BindingEngine.InputDiagnosticKind.UnknownOption,
                $"Unknown option \"--unknown{index}\".",
                index))
            .ToImmutableArray();
        CommandPresentation.Report report = CommandPresentation.CreateInputFailure(
            model,
            entry.Authored.Id,
            "test-command",
            diagnostics);
        StringWriter writer = new(CultureInfo.InvariantCulture);

        bool success = await CommandPresentation.WriteAsync(report, writer, rich, TextRedactor.Empty);

        success.Should().BeTrue();
        Count(writer.ToString(), "Unknown option").Should().Be(Math.Min(diagnosticCount, 20));
        Count(writer.ToString(), "Usage").Should().Be(1);
        writer.ToString().Contains("\u001B[", StringComparison.Ordinal).Should().Be(rich);
        if (diagnosticCount > 20)
        {
            writer.ToString().Should().Contain("... and 1 more errors.");
        }
        else
        {
            writer.ToString().Should().NotContain("more errors");
        }
    }

    [Fact]
    public async Task InputPresentationCapsDisplayedDiagnosticsButRetainsTheCompleteSet()
    {
        Command command = NewCommand();
        Target entry = command.Target("entry").Description("Entry.");
        StringWriter output = new(CultureInfo.InvariantCulture);
        StringWriter error = new(CultureInfo.InvariantCulture);
        ConfigureServices(command, output, error);
        string[] arguments = Enumerable.Range(0, 21).Select(index => $"--unknown{index}").ToArray();

        int exitCode = await command.RunAsync(entry, arguments);

        exitCode.Should().Be(2);
        command.LastBindingResult!.Diagnostics.Should().HaveCount(21);
        error.ToString().Should().Contain("... and 1 more errors.");
        Count(error.ToString(), "error: Unknown option").Should().Be(20);
        Count(error.ToString(), "Usage").Should().Be(1);
    }

    [Fact]
    public async Task SensitiveOneCharacterDefaultIsAbsentFromHelpSerialization()
    {
        Command command = NewCommand();
        command.Option<string>("token").Description("Token.").Default("e").Sensitive();
        Target entry = command.Target("entry").Description("Entry.");
        StringWriter output = new(CultureInfo.InvariantCulture);
        StringWriter error = new(CultureInfo.InvariantCulture);
        ConfigureServices(command, output, error);

        int exitCode = await command.RunAsync(entry, ["--help", "--plain"]);

        exitCode.Should().Be(0);
        output.ToString().Should().NotContain("e");
    }

    [Fact]
    public async Task PresentationFailureReturnsOneAndStillAllowsSequentialReuse()
    {
        Command command = NewCommand();
        Target entry = command.Target("entry").Description("Entry.");
        command.InvocationServicesFactory = () => new InvocationServices(
            _ => null,
            new ThrowingWriter(),
            new StringWriter(CultureInfo.InvariantCulture),
            false,
            false,
            "test-command");

        int failedExit = await command.RunAsync(entry, ["--help"]);

        failedExit.Should().Be(1);
        command.LastInvocationStatus.Should().Be(Command.InvocationStatus.InfrastructureFailure);

        StringWriter output = new(CultureInfo.InvariantCulture);
        ConfigureServices(command, output, new StringWriter(CultureInfo.InvariantCulture));
        int successfulExit = await command.RunAsync(entry, ["--help"]);

        successfulExit.Should().Be(0);
    }

    [Theory]
    [InlineData("C:/work/build.cs", "dotnet", "assembly", "ignored", "build")]
    [InlineData(null, "C:/tools/rafter.exe", "assembly", "ignored", "rafter")]
    [InlineData(null, "dotnet", "Build.Assembly", "ignored", "Build.Assembly")]
    [InlineData(null, "dotnet", null, "C:/work/script.cs", "script")]
    [InlineData(".cs", "C:/tools/rafter.exe", "assembly", "ignored", "rafter")]
    [InlineData(null, null, null, null, "command")]
    public void ResolvesInvocationNamesWithoutExposingDirectories(
        string? filePath,
        string? processPath,
        string? assembly,
        string? launchToken,
        string expected)
    {
        string result = InvocationServices.InvocationNameResolver.Resolve(filePath, processPath, assembly, launchToken);

        result.Should().Be(expected);
    }

    private static int Count(string value, string search)
    {
        int count = 0;
        int index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }

    private static void ConfigureServices(Command command, StringWriter output, StringWriter error)
    {
        command.InvocationServicesFactory = () => new InvocationServices(
            _ => null,
            output,
            error,
            false,
            false,
            "test-command");
    }

    private static Command NewCommand()
        => global::Sotsera.Rafter.Rafter.Command(Root.Invocation).Description("Test command.");

    private sealed class ThrowingWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override Task WriteAsync(string? value) => Task.FromException(new IOException("Synthetic writer failure."));
    }
}
