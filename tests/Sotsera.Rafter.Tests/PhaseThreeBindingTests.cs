using System.Globalization;
using static Sotsera.Rafter.BindingEngine;

namespace Sotsera.Rafter.Tests;

public sealed class PhaseThreeBindingTests
{
    [Fact]
    public async Task BindsEveryOptionShapeOnceIntoAnImmutableSnapshot()
    {
        InstrumentedValue.Reset();
        int environmentReads = 0;
        int scalarValidations = 0;
        int repeatedValidations = 0;
        Command command = NewCommand();
        Option<string> optional = command.Option<string>("optional").Description("Optional.");
        RequiredOption<InstrumentedValue> required = command.RequiredOption<InstrumentedValue>("required")
            .Description("Required.")
            .FromEnvironment("REQUIRED")
            .Validate(value =>
            {
                scalarValidations++;
                return value.Value.Length > 0;
            }, "Required value must contain text.");
        DefaultedOption<int> defaulted = command.Option<int>("count").Description("Count.").Default(3);
        RepeatedOption<string> repeated = command.RepeatedOption<string>("item")
            .Description("Item.")
            .Validate(values =>
            {
                repeatedValidations++;
                return values.Count > 0;
            }, "Supply an item.");
        Target entry = command.Target("entry").Description("Entry.");
        ConfigureServices(command, name =>
        {
            environmentReads++;
            name.Should().Be("REQUIRED");
            return "from-environment";
        });

        var assertion = await FluentActions.Awaiting(() => command.RunAsync(
            entry,
            ["--optional=", "--item", "first", "--item=second"])).Should().ThrowAsync<NotSupportedException>();

        assertion.Which.Message.Should().Contain("later Rafter phase");
        environmentReads.Should().Be(1);
        InstrumentedValue.TryParseCalls.Should().Be(1);
        InstrumentedValue.ParseCalls.Should().Be(0);
        scalarValidations.Should().Be(1);
        repeatedValidations.Should().Be(1);
        BindingResult result = command.LastBindingResult!;
        result.Status.Should().Be(BindingStatus.Success);
        RafterContext context = new(result.Snapshot!);
        context.Value(optional).Should().BeEmpty();
        context.Value(required).Value.Should().Be("from-environment");
        context.Value(defaulted).Should().Be(3);
        context.Value(repeated).Should().Equal("first", "second");
        context.Value(required).Value.Should().Be("from-environment");
        environmentReads.Should().Be(1);
        InstrumentedValue.TryParseCalls.Should().Be(1);
    }

    [Fact]
    public async Task SelectsAllSourcesBeforeAConverterCanThrow()
    {
        ThrowingParsable.Expected = new InvalidDataException("original converter failure");
        int environmentReads = 0;
        Command command = NewCommand();
        command.RequiredOption<ThrowingParsable>("first").Description("First.");
        command.RequiredOption<string>("token").Description("Token.").FromEnvironment("TOKEN").Sensitive();
        Target entry = command.Target("entry").Description("Entry.");
        StringWriter error = new();
        ConfigureServices(command, _ =>
        {
            environmentReads++;
            return "later-secret";
        }, error: error);

        int exitCode = await command.RunAsync(entry, ["--first", "throw"]);

        exitCode.Should().Be(1);
        environmentReads.Should().Be(1);
        command.LastBindingResult!.Exception.Should().BeSameAs(ThrowingParsable.Expected);
        command.LastBindingResult.Redactor.ContainsPattern("later-secret").Should().BeTrue();
        error.ToString().Should().NotContain("later-secret");
    }

    [Fact]
    public async Task EnvironmentFailureUsesTheAlreadyAccumulatedRedactionBoundary()
    {
        InvalidOperationException expected = new("reader details must stay internal");
        Command command = NewCommand();
        command.RequiredOption<string>("token").Description("Token.").Sensitive();
        command.Option<string>("fallback").Description("Fallback.").FromEnvironment("FAIL");
        Target entry = command.Target("entry").Description("Entry.");
        StringWriter error = new();
        ConfigureServices(command, _ => throw expected, error: error);

        int exitCode = await command.RunAsync(entry, ["--token=error"]);

        exitCode.Should().Be(1);
        command.LastBindingResult!.Status.Should().Be(BindingStatus.InfrastructureFailure);
        command.LastBindingResult.Exception.Should().BeSameAs(expected);
        error.ToString().Should().NotContain("error");
        error.ToString().Should().NotContain(expected.Message);
    }

    [Fact]
    public async Task ContextRejectsAHandleFromAnotherCommand()
    {
        Command command = NewCommand();
        RequiredOption<string> owned = command.RequiredOption<string>("owned").Description("Owned.");
        Target entry = command.Target("entry").Description("Entry.");
        ConfigureServices(command);
        Command other = NewCommand();
        RequiredOption<string> foreign = other.RequiredOption<string>("foreign").Description("Foreign.");
        other.Target("entry").Description("Entry.");
        await FluentActions.Awaiting(() => command.RunAsync(entry, ["--owned", "value"]))
            .Should().ThrowAsync<NotSupportedException>();
        RafterContext context = new(command.LastBindingResult!.Snapshot!);

        Action readForeign = () => context.Value(foreign);

        context.Value(owned).Should().Be("value");
        readForeign.Should().Throw<InvalidOperationException>().WithMessage("*different command*");
    }

    [Fact]
    public async Task OrdinaryRejectionContinuesIndependentOptionsButThrownValidationAbortsThem()
    {
        InstrumentedValue.Reset();
        Command rejected = NewCommand();
        rejected.RequiredOption<string>("first").Description("First.").Validate(static _ => false, "Rejected.");
        rejected.RequiredOption<InstrumentedValue>("later").Description("Later.");
        Target rejectedEntry = rejected.Target("entry").Description("Entry.");
        ConfigureServices(rejected);

        int rejectedExit = await rejected.RunAsync(
            rejectedEntry,
            ["--first", "value", "--later", "converted"]);

        rejectedExit.Should().Be(2);
        InstrumentedValue.TryParseCalls.Should().Be(1);

        InstrumentedValue.Reset();
        InvalidDataException expected = new("validator details");
        Command throwing = NewCommand();
        throwing.RequiredOption<string>("first")
            .Description("First.")
            .Sensitive()
            .Validate(_ => throw expected, "Not used.");
        throwing.RequiredOption<InstrumentedValue>("later").Description("Later.");
        Target throwingEntry = throwing.Target("entry").Description("Entry.");
        StringWriter error = new();
        ConfigureServices(throwing, error: error);

        int throwingExit = await throwing.RunAsync(
            throwingEntry,
            ["--first", "secret", "--later", "not-converted"]);

        throwingExit.Should().Be(1);
        InstrumentedValue.TryParseCalls.Should().Be(0);
        throwing.LastBindingResult!.Exception.Should().BeSameAs(expected);
        error.ToString().Should().NotContain("secret");
        error.ToString().Should().NotContain(expected.Message);
    }

    [Fact]
    public async Task ASuccessfulNullReferenceConversionIsAnAuthorFailure()
    {
        Command command = NewCommand();
        command.RequiredOption<NullParsable>("value").Description("Value.");
        Target entry = command.Target("entry").Description("Entry.");
        ConfigureServices(command);

        int exitCode = await command.RunAsync(entry, ["--value", "anything"]);

        exitCode.Should().Be(1);
        command.LastBindingResult!.Status.Should().Be(BindingStatus.AuthorFailure);
        command.LastBindingResult.Failure!.Stage.Should().Be("conversion");
    }

    [Fact]
    public void OrdersSyntaxFirstThenBindingDiagnosticsByOptionDeclaration()
    {
        Command command = NewCommand();
        command.RequiredOption<int>("first").Description("First.");
        command.RequiredOption<string>("second").Description("Second.");
        command.RequiredOption<string>("third").Description("Third.").Validate(static _ => false, "Rejected third.");
        command.Target("entry").Description("Entry.");
        InvocationServices services = new(
            _ => null,
            new StringWriter(CultureInfo.InvariantCulture),
            new StringWriter(CultureInfo.InvariantCulture),
            false,
            false,
            "test-command");

        BindingResult result = BindingEngine.Bind(
            command.FreezeResult.Model!,
            ["--unknown", "--first=bad", "--third=value"],
            services);

        result.Diagnostics.Select(static diagnostic => diagnostic.Message).Should().SatisfyRespectively(
            message => message.Should().StartWith("Unknown option"),
            message => message.Should().Contain("Invalid value").And.Contain("--first"),
            message => message.Should().Contain("--second").And.Contain("missing"),
            message => message.Should().Contain("--third").And.Contain("Rejected third"));
        result.Diagnostics.Select(static diagnostic => diagnostic.ArgumentPosition).Should().Equal(0, 1, null, 2);
    }

    [Fact]
    public async Task UsesFallbackPrecedenceAndKeepsRepeatedAbsenceImmutable()
    {
        int environmentReads = 0;
        Command command = NewCommand();
        RequiredOption<string> commandLine = command.RequiredOption<string>("command-line")
            .Description("Command line.")
            .FromEnvironment("COMMAND_LINE");
        DefaultedOption<string> environment = command.Option<string>("environment")
            .Description("Environment.")
            .FromEnvironment("ENVIRONMENT")
            .Default("default-not-used");
        DefaultedOption<int> authoredDefault = command.Option<int>("default").Description("Default.").Default(7);
        Option<string> absent = command.Option<string>("absent").Description("Absent.");
        RepeatedOption<string> repeated = command.RepeatedOption<string>("item").Description("Item.");
        Target entry = command.Target("entry").Description("Entry.");
        ConfigureServices(command, name =>
        {
            environmentReads++;
            return string.Equals(name, "ENVIRONMENT", StringComparison.Ordinal) ? string.Empty : "must-not-win";
        });

        await FluentActions.Awaiting(() => command.RunAsync(entry, ["--command-line="]))
            .Should().ThrowAsync<NotSupportedException>();

        RafterContext context = new(command.LastBindingResult!.Snapshot!);
        context.Value(commandLine).Should().BeEmpty();
        context.Value(environment).Should().BeEmpty();
        context.Value(authoredDefault).Should().Be(7);
        context.Value(absent).Should().BeNull();
        IReadOnlyList<string> values = context.Value(repeated);
        values.Should().BeEmpty();
        values.Should().BeAssignableTo<System.Collections.Immutable.IImmutableList<string>>();
        environmentReads.Should().Be(1);
    }

    private static Command NewCommand()
        => global::Sotsera.Rafter.Rafter.Command(Root.Invocation).Description("Test command.");

    private static void ConfigureServices(
        Command command,
        Func<string, string?>? environment = null,
        StringWriter? output = null,
        StringWriter? error = null,
        bool ansi = false)
    {
        command.InvocationServicesFactory = () => new InvocationServices(
            environment ?? (_ => null),
            output ?? new StringWriter(CultureInfo.InvariantCulture),
            error ?? new StringWriter(CultureInfo.InvariantCulture),
            ansi,
            ansi,
            "test-command");
    }

    private sealed class InstrumentedValue(string value) : IParsable<InstrumentedValue>
    {
        internal static int ParseCalls { get; private set; }

        internal static int TryParseCalls { get; private set; }

        internal string Value { get; } = value;

        public static InstrumentedValue Parse(string value, IFormatProvider? provider)
        {
            ParseCalls++;
            return new InstrumentedValue(value);
        }

        public static bool TryParse(string? value, IFormatProvider? provider, out InstrumentedValue result)
        {
            TryParseCalls++;
            provider.Should().BeSameAs(CultureInfo.InvariantCulture);
            result = new InstrumentedValue(value ?? string.Empty);
            return value is not null;
        }

        internal static void Reset()
        {
            ParseCalls = 0;
            TryParseCalls = 0;
        }
    }

    private sealed class ThrowingParsable : IParsable<ThrowingParsable>
    {
        internal static Exception Expected { get; set; } = null!;

        public static ThrowingParsable Parse(string value, IFormatProvider? provider) => throw Expected;

        public static bool TryParse(string? value, IFormatProvider? provider, out ThrowingParsable result)
        {
            result = null!;
            throw Expected;
        }
    }

    private sealed class NullParsable : IParsable<NullParsable>
    {
        public static NullParsable Parse(string value, IFormatProvider? provider) => null!;

        public static bool TryParse(string? value, IFormatProvider? provider, out NullParsable result)
        {
            result = null!;
            return true;
        }
    }
}
