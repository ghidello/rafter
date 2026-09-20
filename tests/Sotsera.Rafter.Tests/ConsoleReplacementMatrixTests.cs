namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class ConsoleReplacementMatrixTests
{
    [Theory]
    [InlineData("output", false)]
    [InlineData("error", false)]
    [InlineData("both", false)]
    [InlineData("output", true)]
    [InlineData("error", true)]
    [InlineData("both", true)]
    public async Task ReplacementFailsEveryOverlappingInvocationAndRestoresEntryWriters(string stream, bool duringReport)
    {
        TextWriter originalOutput = Console.Out;
        TextWriter originalError = Console.Error;
        CountingWriter replacement = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Command second = PhaseFiveTestSupport.CreateCommand();
        Target secondWork = second.Target("second").Description("Second.").Run(async () =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)
                .ConfigureAwait(false);
        });
        Task<int> secondRun = second.RunAsync(secondWork, [], TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Command first = PhaseFiveTestSupport.CreateCommand();
            ReportWriter sink = new(duringReport ? () => Replace(stream, replacement) : null);
            first.InvocationServicesFactory = () => new InvocationServices(_ => null, sink, sink,
                OutputCapabilities.Plain, OutputCapabilities.Plain, "test");
            Target firstWork = first.Target("first").Description("First.").Run(() =>
            {
                if (!duringReport)
                {
                    Replace(stream, replacement);
                }
            });

            (await first.RunAsync(firstWork, [], TestContext.Current.CancellationToken)).Should().Be(1);
            release.TrySetResult();
            (await secondRun).Should().Be(1);

            first.LastOutputFailure.Should().NotBeNull();
            second.LastOutputFailure.Should().NotBeNull();
            first.LastExecutionOutcome!.ExitCode.Should().Be(0);
            second.LastExecutionOutcome!.ExitCode.Should().Be(0);
            replacement.Writes.Should().Be(0);
            Console.Out.Should().BeSameAs(originalOutput);
            Console.Error.Should().BeSameAs(originalError);
        }
        finally
        {
            release.TrySetResult();
            try
            {
                await secondRun.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
            finally
            {
                Console.SetOut(originalOutput);
                Console.SetError(originalError);
            }
        }

    }

    private static void Replace(string stream, TextWriter replacement)
    {
        if (stream is "output" or "both")
        {
            Console.SetOut(replacement);
        }
        if (stream is "error" or "both")
        {
            Console.SetError(replacement);
        }
    }

    private sealed class CountingWriter : StringWriter
    {
        internal int Writes { get; private set; }

        public override void Write(string? value) => Writes++;
    }

    private sealed class ReportWriter(Action? replace) : StringWriter
    {
        public override void Write(string? value)
        {
            if (value?.Contains("Command succeeded", StringComparison.Ordinal) == true)
            {
                replace?.Invoke();
            }
            base.Write(value);
        }
    }
}
