using System.Globalization;
using static Sotsera.Rafter.ExecutionRuntime;

namespace Sotsera.Rafter.Tests;

public sealed class PhaseFivePresentationTests
{
    [Fact]
    public async Task ReportsTargetFailuresInPlanOrderWithoutExceptionMessages()
    {
        StringWriter error = new(CultureInfo.InvariantCulture);
        Command command = PhaseFiveTestSupport.CreateCommand(concurrency: 2);
        PhaseFiveTestSupport.ConfigureServices(command, error: error);
        Target first = command.Target("first")
            .Description("First.")
            .Run(() => throw new InvalidDataException("first-secret"));
        Target second = command.Target("second")
            .Description("Second.")
            .Run(() => throw new IOException("second-secret"));
        Target entry = command.Target("entry").Description("Entry.").DependsOn(second, first);

        int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);
        string report = error.ToString();

        exitCode.Should().Be(1);
        report.Should().Contain("Target 'second' failed during execution (IOException).");
        report.Should().Contain("Target 'first' failed during execution (InvalidDataException).");
        report.IndexOf("Target 'second'", StringComparison.Ordinal).Should().BeLessThan(
            report.IndexOf("Target 'first'", StringComparison.Ordinal));
        report.Should().NotContain("first-secret").And.NotContain("second-secret");
    }

    [Fact]
    public async Task ReportsCleanupFailuresAsSecondaryToCancellation()
    {
        StringWriter error = new(CultureInfo.InvariantCulture);
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        Command command = PhaseFiveTestSupport.CreateCommand();
        PhaseFiveTestSupport.ConfigureServices(command, error: error);
        Target entry = command.Target("entry")
            .Description("Entry.")
            .Run(() => cancellation.Cancel())
            .Finally(() => throw new IOException("cleanup-secret"));
        command.Finally(() => throw new FormatException("command-secret"));

        int exitCode = await command.RunAsync(entry, [], cancellation.Token);
        string report = error.ToString();

        exitCode.Should().Be(130);
        report.Should().ContainAll(
            "Command cancelled",
            "Cleanup also failed",
            "Target 'entry' cleanup failed (IOException).",
            "Command cleanup failed (FormatException).");
        report.Should().NotContain("cleanup-secret").And.NotContain("command-secret");
    }

    [Fact]
    public async Task DoesNotRepeatCleanupOnlyFailuresInAMixedExecutionFailureReport()
    {
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target entry = command.Target("entry")
            .Description("Entry.")
            .Run(() => cancellation.Cancel())
            .Finally(() => throw new IOException("cleanup-secret"));

        _ = await command.RunAsync(entry, [], cancellation.Token);

        ExecutionOutcome cancelled = command.LastExecutionOutcome!;
        TargetResult cleanupFailure = cancelled.Targets.Should().ContainSingle().Which;
        TargetResult executionFailure = cleanupFailure with
        {
            FailurePhase = FailurePhase.Execution,
            PrimaryException = new InvalidDataException("execution-secret"),
            CleanupException = null,
        };
        ExecutionOutcome mixed = cancelled with
        {
            Targets = [cleanupFailure, executionFailure],
            ExitCode = 1,
        };

        CommandPresentation.Report report = CommandPresentation.CreateExecutionFailure(mixed);

        report.Lines.Should().ContainSingle(line => line.Text.Contains("failed during cleanup", StringComparison.Ordinal));
        report.Lines.Should().NotContain(line => line.Text == "Cleanup also failed");
    }
}
