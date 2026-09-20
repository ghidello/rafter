namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class InvocationBoundaryTests
{
    [Theory]
    [InlineData("success", 0)]
    [InlineData("failure", 1)]
    [InlineData("cleanup", 1)]
    [InlineData("help", 0)]
    [InlineData("input", 2)]
    [InlineData("cancel", 130)]
    public async Task RetainsOverlapGuardThroughReportingAndSignalTeardownThenPermitsReuse(string outcome, int expected)
    {
        using CancellationTokenSource cancellation = new();
        bool first = true;
        int reportProbes = 0;
        int teardownProbes = 0;
        ProbeWriter writer = new();
        Command command = PhaseFiveTestSupport.CreateCommand();
        command.InvocationServicesFactory = () => new InvocationServices(_ => null, writer, writer,
            OutputCapabilities.Plain, OutputCapabilities.Plain, "test");
        Target work = command.Target("work").Description("Work.").Run(context =>
        {
            if (first && outcome is "failure")
            {
                throw new IOException("execution failure");
            }
            if (first && outcome is "cancel")
            {
                cancellation.Cancel();
                context.CancellationToken.ThrowIfCancellationRequested();
            }
        }).Finally(() =>
        {
            if (first && outcome is "cleanup")
            {
                throw new IOException("cleanup failure");
            }
        });
        writer.Probe = () =>
        {
            RejectOverlap();
            reportProbes++;
        };
        command.CancellationCoordinator = new ConsoleCancellationCoordinator(new ProbeSignalSource(() =>
        {
            RejectOverlap();
            teardownProbes++;
        }));
        string[] arguments = outcome switch { "help" => ["--help"], "input" => ["--unknown"], _ => [] };

        (await command.RunAsync(work, arguments, cancellation.Token)).Should().Be(expected);
        first = false;
        (await command.RunAsync(work, [], TestContext.Current.CancellationToken)).Should().Be(0);

        reportProbes.Should().BeGreaterThanOrEqualTo(2);
        teardownProbes.Should().Be(outcome is "help" ? 1 : 2);

        void RejectOverlap()
        {
            Action overlapping = () => _ = command.RunAsync(work, [], CancellationToken.None);
            overlapping.Should().Throw<InvalidOperationException>();
        }
    }

    private sealed class ProbeWriter : StringWriter
    {
        internal Action? Probe { get; set; }

        public override void Write(string? value)
        {
            Probe?.Invoke();
            base.Write(value);
        }
    }

    private sealed class ProbeSignalSource(Action onDispose) : IConsoleSignalSource
    {
        public IDisposable Subscribe(Action<ConsoleSignal> handler) => new Subscription(onDispose);
    }

    private sealed class Subscription(Action onDispose) : IDisposable
    {
        private Action? _onDispose = onDispose;

        public void Dispose() => Interlocked.Exchange(ref _onDispose, null)?.Invoke();
    }
}
