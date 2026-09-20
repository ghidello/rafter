using System.Text.Json;
using static Sotsera.Rafter.ExecutionRuntime;

namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class ExecutionSummaryTests
{
    public static IEnumerable<object[]> GoldenCases()
    {
        string[] scenarios = ["failure-cleanup", "cleanup-only", "command-cleanup-only", "cancellation-cleanup"];
        foreach (string scenario in scenarios)
        {
            using JsonDocument fixture = ReadFixture(scenario);
            foreach (JsonElement document in fixture.RootElement.GetProperty("documents").EnumerateArray())
            {
                yield return [scenario, document.GetProperty("profile").GetProperty("name").GetString()!];
            }
        }
    }

    [Theory]
    [MemberData(nameof(GoldenCases))]
    public async Task FinalSummaryMatchesAcceptedDocument(string scenario, string profileName)
    {
        using JsonDocument fixture = ReadFixture(scenario);
        JsonElement document = fixture.RootElement.GetProperty("documents").EnumerateArray()
            .Single(item => string.Equals(
                item.GetProperty("profile").GetProperty("name").GetString(), profileName, StringComparison.Ordinal));
        OutputCapabilities capabilities = ReadCapabilities(document.GetProperty("profile"));
        StringWriter output = new();
        StringWriter error = new();
        Command command = PhaseFiveTestSupport.CreateCommand(concurrency: 1);
        command.InvocationServicesFactory = () => new InvocationServices(_ => null, output, error,
            capabilities, capabilities, "test-command");
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        Target entry = DefineScenario(command, scenario, cancellation);

        int exitCode = await command.RunAsync(entry, [], cancellation.Token);

        exitCode.Should().Be(scenario is "cancellation-cleanup" ? 130 : 1);
        output.ToString().Should().Be(document.GetProperty("stdout").GetString());
        error.ToString().Should().Be(document.GetProperty("stderr").GetString());
    }

    [Fact]
    public async Task TerminalShapesMatchAcceptedDocumentsInPlanOrder()
    {
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target executed = command.Target("executed").Description("Execute.").Run(() => { });
        Target empty = command.Target("empty").Description("No work.");
        Target aggregate = command.Target("aggregate").Description("Aggregate.").DependsOn(empty);
        Target skipped = command.Target("skipped").Description("Skip.").When(() => false).Run(() => { });
        Target first = command.Target("first").Description("Fail.").Run(() => throw new IOException());
        Target second = command.Target("second").Description("Fail condition.")
            .When(() => throw new InvalidOperationException()).Run(() => { });
        Target blocked = command.Target("blocked").Description("Block.").DependsOn(second, first);
        Target entry = command.Target("entry").Description("Entry.").DependsOn(executed, aggregate, skipped, blocked);
        (await command.RunAsync(entry, [], TestContext.Current.CancellationToken)).Should().Be(1);
        ExecutionOutcome outcome = command.LastExecutionOutcome!;
        // Exercise the renderer independently of DFS traversal using the fixture's valid topological order.
        string[] order = ["executed", "empty", "aggregate", "skipped", "first", "second", "blocked", "entry"];
        outcome = outcome with
        {
            Targets = [.. outcome.Targets.Select(target => target with
            {
                PlanIndex = Array.IndexOf(order, target.Target.Name),
            }).OrderByDescending(static target => target.PlanIndex)],
        };
        using JsonDocument fixture = ReadFixture("terminal-shapes");
        foreach (JsonElement document in fixture.RootElement.GetProperty("documents").EnumerateArray())
        {
            OutputCapabilities capabilities = ReadCapabilities(document.GetProperty("profile"));
            StringWriter writer = new();
            CommandPresentation.Report report = CommandPresentation.CreateExecutionSummary(outcome, capabilities);
            (await CommandPresentation.WriteAsync(report, writer, capabilities, TextRedactor.Empty)).Should().BeTrue();
            writer.ToString().Should().Be(document.GetProperty("stderr").GetString());
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task SuccessUsesStdoutCapabilitiesAndRespectsColorAndPlainOverrides(bool plain, bool noColor)
    {
        StringWriter output = new();
        StringWriter error = new();
        Command command = PhaseFiveTestSupport.CreateCommand();
        command.InvocationServicesFactory = () => new InvocationServices(_ => noColor ? "1" : null, output, error,
            PhaseFiveTestSupport.RichCapabilities, OutputCapabilities.Plain, "test-command");
        Target work = command.Target("work").Description("Work.").Run(context => context.Output.Line("payload"));

        (await command.RunAsync(work, plain ? ["--plain"] : [], TestContext.Current.CancellationToken)).Should().Be(0);

        error.ToString().Should().BeEmpty();
        string text = output.ToString();
        text.Should().Contain(plain ? "  [work] Succeeded\n" : "  ✓ [work] Succeeded");
        if (plain || noColor)
        {
            text.Should().NotContain("\u001b");
        }
        else
        {
            text.Should().Contain("\u001b[");
        }
        text.IndexOf("payload", StringComparison.Ordinal).Should()
            .BeLessThan(text.IndexOf("Command succeeded", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SummaryRedactsTargetAndBlockerNames()
    {
        StringWriter error = new();
        Command command = PhaseFiveTestSupport.CreateCommand();
        PhaseFiveTestSupport.ConfigureServices(command, error: error);
        _ = command.RequiredOption<string>("secret").Description("Secret.").Sensitive();
        Target failed = command.Target("private-name").Description("Fail.").Run(() => throw new IOException());
        Target entry = command.Target("entry").Description("Entry.").DependsOn(failed);

        (await command.RunAsync(entry, ["--secret", "private-name"], TestContext.Current.CancellationToken))
            .Should().Be(1);

        error.ToString().Should().Contain("[<redacted>] Failed")
            .And.Contain("blocked by: <redacted>").And.NotContain("private-name");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SummarySinkFailurePreservesExecutionAndCleanup(bool cancel)
    {
        IOException failure = new("summary sink failed");
        ThrowingWriter writer = new(failure);
        Command command = PhaseFiveTestSupport.CreateCommand();
        PhaseFiveTestSupport.ConfigureServices(command, writer, writer);
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        int cleanupCount = 0;
        Target entry = command.Target("work").Description("Work.").Run(context =>
        {
            if (cancel)
            {
                cancellation.Cancel();
                context.CancellationToken.ThrowIfCancellationRequested();
            }
        }).Finally(() => cleanupCount++);
        command.Finally(() => cleanupCount++);

        (await command.RunAsync(entry, [], cancellation.Token)).Should().Be(1);

        command.LastInvocationStatus.Should().Be(Command.InvocationStatus.InfrastructureFailure);
        command.LastOutputFailure.Should().BeSameAs(failure);
        command.LastExecutionOutcome!.ExitCode.Should().Be(cancel ? 130 : 0);
        cleanupCount.Should().Be(2);
    }

    [Fact]
    public async Task FinalSummaryFollowsBufferedOutputAndBothCleanupCallbacks()
    {
        StringWriter output = new();
        Command command = PhaseFiveTestSupport.CreateCommand();
        PhaseFiveTestSupport.ConfigureServices(command, output);
        Target entry = command.Target("work").Description("Work.").Run(() => Console.Write("partial"))
            .Finally(() => Console.Write(" target cleanup"));
        command.Finally(() => Console.Write("command cleanup"));

        (await command.RunAsync(entry, [], TestContext.Current.CancellationToken)).Should().Be(0);

        output.ToString().Should().Be("[work] partial target cleanup\n[command] command cleanup\n"
            + "\nCommand succeeded\n  [work] Succeeded\n");
    }

    [Fact]
    public async Task FailureUsesStderrCapabilitiesIndependentlyOfStdout()
    {
        StringWriter output = new();
        StringWriter error = new();
        Command command = PhaseFiveTestSupport.CreateCommand();
        command.InvocationServicesFactory = () => new InvocationServices(_ => null, output, error,
            PhaseFiveTestSupport.RichCapabilities, OutputCapabilities.Plain, "test-command");
        Target entry = command.Target("work").Description("Fail.").Run(() => throw new IOException());

        (await command.RunAsync(entry, [], TestContext.Current.CancellationToken)).Should().Be(1);

        output.ToString().Should().BeEmpty();
        error.ToString().Should().Be("\nCommand failed\n  [work] Failed\n\n"
            + "error: Target 'work' failed during execution (IOException).\n");
    }

    [Fact]
    public async Task SummaryFailsClosedWhenStylingWouldReintroduceASecret()
    {
        StringWriter output = new();
        StringWriter error = new();
        Command command = PhaseFiveTestSupport.CreateCommand();
        command.InvocationServicesFactory = () => new InvocationServices(_ => null, output, error,
            PhaseFiveTestSupport.RichCapabilities, PhaseFiveTestSupport.RichCapabilities, "test-command");
        _ = command.RequiredOption<string>("secret").Description("Secret.").Sensitive();
        Target entry = command.Target("work").Description("Work.").Run(() => { });

        (await command.RunAsync(entry, ["--secret", "\u001b"], TestContext.Current.CancellationToken)).Should().Be(1);

        output.ToString().Should().BeEmpty();
        error.ToString().Should().BeEmpty();
        command.LastExecutionOutcome!.ExitCode.Should().Be(0);
        command.LastOutputFailure.Should().BeOfType<InvalidOperationException>();
        command.LastInvocationStatus.Should().Be(Command.InvocationStatus.InfrastructureFailure);
    }

    private static Target DefineScenario(Command command, string scenario, CancellationTokenSource cancellation)
    {
        if (scenario is "cancellation-cleanup")
        {
            Target done = command.Target("done").Description("Done.").Run(() => { });
            Target work = command.Target("work").Description("Cancel.").Run(context =>
            {
                cancellation.Cancel();
                context.CancellationToken.ThrowIfCancellationRequested();
            }).Finally(() => throw new IOException());
            command.Finally(() => throw new InvalidOperationException());
            return command.Target("entry").Description("Entry.").DependsOn(done, work);
        }

        Target target = command.Target("work").Description("Work.").Run(() =>
        {
            if (scenario is "failure-cleanup")
            {
                throw new InvalidOperationException();
            }
        });
        if (scenario is not "command-cleanup-only")
        {
            target.Finally(() => throw new IOException());
        }
        command.Finally(() => throw (scenario is "failure-cleanup"
            ? new UnauthorizedAccessException()
            : scenario is "command-cleanup-only" ? new IOException() : new InvalidOperationException()));
        return scenario is "failure-cleanup"
            ? command.Target("entry").Description("Entry.").DependsOn(target)
            : target;
    }

    private static JsonDocument ReadFixture(string name)
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "PresentationFixtures",
            $"{name}.json")));

    private static OutputCapabilities ReadCapabilities(JsonElement profile)
        => new(false, profile.GetProperty("rich").GetBoolean(), true, profile.GetProperty("color").GetBoolean(),
            profile.GetProperty("unicode").GetBoolean(), profile.GetProperty("live").GetBoolean(),
            profile.GetProperty("width").GetInt32());

    private sealed class ThrowingWriter(Exception failure) : StringWriter
    {
        public override void Write(string? value) => throw failure;
    }
}
