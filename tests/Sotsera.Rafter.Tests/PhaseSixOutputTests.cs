using System.Collections;
using System.Globalization;

namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class PhaseSixOutputTests
{
    private static readonly string[] PropertyPaths = ["src", "tests"];

    [Fact]
    public async Task RoutesAndFormatsSemanticOutputWithoutChangingSuccess()
    {
        StringWriter output = new(CultureInfo.InvariantCulture);
        StringWriter error = new(CultureInfo.InvariantCulture);
        Command command = PhaseFiveTestSupport.CreateCommand();
        PhaseFiveTestSupport.ConfigureServices(command, output, error);
        Target entry = command.Target("present")
            .Description("Present output.")
            .Run(context =>
            {
                context.Output.Line("Starting.");
                context.Output.Property("missing", null);
                context.Output.Property("paths", PropertyPaths);
                context.Output.Success("Finished.");
                context.Output.Warning("Optional input is missing.");
                context.Output.Error("A package is missing.", recovery: "Pack it and retry.");
            });

        int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        output.ToString().Should().Be(
            "[present] Starting.\n"
            + "[present] missing=null\n"
            + "[present] paths=[\"src\",\"tests\"]\n"
            + "[present] success: Finished.\n");
        error.ToString().Should().Be(
            "[present] warning: Optional input is missing.\n"
            + "[present] error: A package is missing.\n"
            + "[present] recovery: Pack it and retry.\n");
    }

    [Fact]
    public async Task RedactsSensitiveValuesBeforeSemanticOutputReachesTheSink()
    {
        StringWriter output = new(CultureInfo.InvariantCulture);
        Command command = PhaseFiveTestSupport.CreateCommand();
        PhaseFiveTestSupport.ConfigureServices(command, output);
        RequiredOption<string> token = command.RequiredOption<string>("token")
            .Description("A token.")
            .Sensitive();
        Target entry = command.Target("redact")
            .Description("Redact output.")
            .Run(context => context.Output.Line($"token={context.Value(token)}"));

        int exitCode = await command.RunAsync(
            entry,
            ["--token", "disposable-secret"],
            TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        output.ToString().Should().Be("[redact] token=<redacted>\n");
        output.ToString().Should().NotContain("disposable-secret");
    }

    [Fact]
    public async Task FallsBackToCommandScopeThenRejectsOutputAfterSettlement()
    {
        StringWriter output = new(CultureInfo.InvariantCulture);
        Command command = PhaseFiveTestSupport.CreateCommand();
        PhaseFiveTestSupport.ConfigureServices(command, output);
        RafterOutput? retained = null;
        Target entry = command.Target("work")
            .Description("Retain output.")
            .Run(context => retained = context.Output);
        command.Finally(() => retained!.Line("Cleaning command."));

        int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        output.ToString().Should().Be("[command] Cleaning command.\n");
        Action afterSettlement = () => retained!.Line("Too late.");
        afterSettlement.Should().Throw<InvalidOperationException>();
        output.ToString().Should().NotContain("Too late.");
    }

    [Fact]
    public async Task PreservesPropertyEnumerationFailureAsTargetFailure()
    {
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target entry = command.Target("property")
            .Description("Format a property.")
            .Run(context => context.Output.Property("items", new ThrowingEnumerable()));

        int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(1);
        command.LastExecutionOutcome!.Targets.Should().ContainSingle()
            .Which.PrimaryException.Should().BeOfType<InvalidDataException>();
    }

    [Fact]
    public async Task ConvertsSinkFailureIntoInfrastructureFailureWithoutThrowingThroughTheCallback()
    {
        Command command = PhaseFiveTestSupport.CreateCommand();
        PhaseFiveTestSupport.ConfigureServices(command, output: new ThrowingWriter());
        bool continued = false;
        Target entry = command.Target("sink")
            .Description("Fail the sink.")
            .Run(context =>
            {
                context.Output.Line("Cannot be written.");
                continued = true;
            });

        int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

        continued.Should().BeTrue();
        exitCode.Should().Be(1);
        command.LastOutputFailure.Should().BeOfType<IOException>();
        command.LastInvocationStatus.Should().Be(Command.InvocationStatus.InfrastructureFailure);
    }

    [Fact]
    public async Task AttributesPartialConsoleWritesAcrossAsyncFlowsAndRestoresTheHostWriters()
    {
        TextWriter hostOutput = Console.Out;
        TextWriter hostError = Console.Error;
        StringWriter output = new(CultureInfo.InvariantCulture);
        StringWriter error = new(CultureInfo.InvariantCulture);
        Command command = PhaseFiveTestSupport.CreateCommand();
        PhaseFiveTestSupport.ConfigureServices(command, output, error);
        Target entry = command.Target("console")
            .Description("Write to the console.")
            .Run(async () =>
            {
                Console.Write("part");
                await Task.Yield();
                Console.WriteLine("ial");
                await Task.Run(() => Console.Error.WriteLine("failure stream")).ConfigureAwait(false);
            });

        int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        output.ToString().Should().Be("[console] partial\n");
        error.ToString().Should().Be("[console] failure stream\n");
        Console.Out.Should().BeSameAs(hostOutput);
        Console.Error.Should().BeSameAs(hostError);
    }

    [Fact]
    public async Task RedactsASecretSplitAcrossConsoleWrites()
    {
        StringWriter output = new(CultureInfo.InvariantCulture);
        Command command = PhaseFiveTestSupport.CreateCommand();
        PhaseFiveTestSupport.ConfigureServices(command, output);
        _ = command.RequiredOption<string>("token")
            .Description("A token.")
            .Sensitive();
        Target entry = command.Target("console")
            .Description("Write a split secret.")
            .Run(() =>
            {
                Console.Write("disposable-");
                Console.WriteLine("secret");
            });

        int exitCode = await command.RunAsync(
            entry,
            ["--token", "disposable-secret"],
            TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        output.ToString().Should().Be("[console] <redacted>\n");
    }

    [Fact]
    public async Task RedactsMultilineAndSegmentBoundarySecretsIncrementally()
    {
        StringWriter output = new(CultureInfo.InvariantCulture);
        Command command = PhaseFiveTestSupport.CreateCommand();
        PhaseFiveTestSupport.ConfigureServices(command, output);
        _ = command.RequiredOption<string>("multiline")
            .Description("A multiline token.")
            .Sensitive();
        _ = command.RequiredOption<string>("token")
            .Description("A boundary token.")
            .Sensitive();
        string prefix = new('a', 65_530);
        Target entry = command.Target("console")
            .Description("Write boundary secrets.")
            .Run(() =>
            {
                Console.WriteLine("top");
                Console.WriteLine("secret");
                Console.Write(prefix);
                Console.Write("disposable-");
                Console.WriteLine("secret");
            });

        int exitCode = await command.RunAsync(
            entry,
            ["--multiline", "top\nsecret", "--token", "disposable-secret"],
            TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        output.ToString().Should().NotContain("top").And.NotContain("secret");
        output.ToString().Should().Contain("<redacted>");
    }

    [Fact]
    public async Task DetectsWriterReplacementAndRestoresTheTrueHostWriter()
    {
        TextWriter hostOutput = Console.Out;
        StringWriter configuredOutput = new(CultureInfo.InvariantCulture);
        StringWriter replacement = new(CultureInfo.InvariantCulture);
        Command command = PhaseFiveTestSupport.CreateCommand();
        PhaseFiveTestSupport.ConfigureServices(command, configuredOutput);
        Target entry = command.Target("replace")
            .Description("Replace output.")
            .Run(() =>
            {
                Console.SetOut(replacement);
                Console.WriteLine("unsupported replacement output");
            });

        int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(1);
        command.LastInvocationStatus.Should().Be(Command.InvocationStatus.InfrastructureFailure);
        command.LastOutputFailure.Should().BeOfType<InvalidOperationException>();
        Console.Out.Should().BeSameAs(hostOutput);
    }

    [Fact]
    public async Task KeepsOverlappingCommandsInTheirOwnConsoleSinks()
    {
        StringWriter firstOutput = new(CultureInfo.InvariantCulture);
        StringWriter secondOutput = new(CultureInfo.InvariantCulture);
        TaskCompletionSource firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        (Command First, Target FirstEntry) = CreateWaitingConsoleCommand("first", firstOutput, firstStarted, release);
        (Command Second, Target SecondEntry) = CreateWaitingConsoleCommand("second", secondOutput, secondStarted, release);

        Task<int> firstRun = First.RunAsync(FirstEntry, [], TestContext.Current.CancellationToken);
        await firstStarted.Task;
        Task<int> secondRun = Second.RunAsync(SecondEntry, [], TestContext.Current.CancellationToken);
        await secondStarted.Task;
        release.SetResult();

        int[] exitCodes = await Task.WhenAll(firstRun, secondRun);

        exitCodes.Should().Equal(0, 0);
        firstOutput.ToString().Should().Be("[first] first\n");
        secondOutput.ToString().Should().Be("[second] second\n");
    }

    [Fact]
    public async Task KeepsARetainedCoordinatingWriterSafeAfterRestoration()
    {
        TextWriter originalOutput = Console.Out;
        StringWriter hostOutput = new(CultureInfo.InvariantCulture);
        TextWriter? retained = null;
        try
        {
            Console.SetOut(hostOutput);
            TextWriter installedHostOutput = Console.Out;
            Command command = PhaseFiveTestSupport.CreateCommand();
            Target entry = command.Target("retain")
                .Description("Retain the coordinating writer.")
                .Run(() => retained = Console.Out);

            int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);
            retained!.WriteLine("late host output");

            exitCode.Should().Be(0);
            Console.Out.Should().BeSameAs(installedHostOutput);
            hostOutput.ToString().Should().Contain("late host output");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    [Fact]
    public async Task AllowsAConfiguredSinkToWriteToTheHostConsoleWithoutRecursion()
    {
        TextWriter originalOutput = Console.Out;
        StringWriter hostOutput = new(CultureInfo.InvariantCulture);
        try
        {
            Console.SetOut(hostOutput);
            ReentrantWriter configuredOutput = new();
            Command command = PhaseFiveTestSupport.CreateCommand();
            PhaseFiveTestSupport.ConfigureServices(command, configuredOutput);
            Target entry = command.Target("sink")
                .Description("Write through a reentrant sink.")
                .Run(context => context.Output.Line("managed output"));

            int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

            exitCode.Should().Be(0);
            configuredOutput.ToString().Should().Be("[sink] managed output\n");
            hostOutput.ToString().Should().Be($"sink host output{Environment.NewLine}");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    [Fact]
    public async Task FlushesManagedPartialOutputBeforeHostPassThrough()
    {
        TextWriter originalOutput = Console.Out;
        List<string> writes = [];
        RecordingWriter hostOutput = new("host", writes);
        RecordingWriter configuredOutput = new("managed", writes);
        try
        {
            Console.SetOut(hostOutput);
            Command command = PhaseFiveTestSupport.CreateCommand();
            PhaseFiveTestSupport.ConfigureServices(command, configuredOutput);
            Target entry = command.Target("order")
                .Description("Preserve same-stream ordering.")
                .Run(async () =>
                {
                    Console.Write("partial");
                    Task hostWrite;
                    using (ExecutionContext.SuppressFlow())
                    {
                        hostWrite = Task.Run(() => Console.WriteLine("outside"));
                    }

                    await hostWrite.ConfigureAwait(false);
                });

            int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

            exitCode.Should().Be(0);
            writes.Should().HaveCount(2);
            writes[0].Should().StartWith("managed:[order] partial");
            writes[1].Should().StartWith("host:outside");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    private static (Command Command, Target Entry) CreateWaitingConsoleCommand(
        string name,
        StringWriter output,
        TaskCompletionSource started,
        TaskCompletionSource release)
    {
        Command command = PhaseFiveTestSupport.CreateCommand();
        PhaseFiveTestSupport.ConfigureServices(command, output);
        Target entry = command.Target(name)
            .Description("Write while another command is active.")
            .Run(async () =>
            {
                started.SetResult();
                await release.Task.ConfigureAwait(false);
                Console.WriteLine(name);
            });
        return (command, entry);
    }

    private sealed class ThrowingEnumerable : IEnumerable
    {
        public IEnumerator GetEnumerator() => new ThrowingEnumerator();

        private sealed class ThrowingEnumerator : IEnumerator
        {
            public object Current => "unused";

            public bool MoveNext() => throw new InvalidDataException("Expected enumeration failure.");

            public void Reset() => throw new NotSupportedException();
        }
    }

    private sealed class ThrowingWriter : StringWriter
    {
        public override void Write(string? value) => throw new IOException("Expected sink failure.");
    }

    private sealed class ReentrantWriter : StringWriter
    {
        public override void Write(string? value)
        {
            base.Write(value);
            Console.WriteLine("sink host output");
        }
    }

    private sealed class RecordingWriter(string name, List<string> writes) : StringWriter
    {
        public override void Write(string? value) => writes.Add($"{name}:{value}");
    }
}
