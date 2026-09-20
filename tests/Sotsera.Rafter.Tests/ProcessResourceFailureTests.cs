using System.Diagnostics;

namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class ProcessResourceFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdapterConstructionFailureUsesTheSafeStartupClassification(bool capture)
    {
        IOException originalFailure = new("private executable and argument details");
        IProcessAdapterFactory original = ProcessRuntime.AdapterFactory;
        ProcessRuntime.AdapterFactory = new Factory(() => throw originalFailure);
        try
        {
            Command command = PhaseFiveTestSupport.CreateCommand();
            Target entry = command.Target("work").Description("Start.").Run(async context =>
            {
                if (capture)
                {
                    await context.Process("synthetic").Capture().ConfigureAwait(false);
                }
                else
                {
                    await context.Process("synthetic").Run().ConfigureAwait(false);
                }
            });

            (await command.RunAsync(entry, [], TestContext.Current.CancellationToken)).Should().Be(1);

            ProcessStartException failure = command.LastExecutionOutcome!.Targets.Single().PrimaryException
                .Should().BeOfType<ProcessStartException>().Subject;
            failure.InnerException.Should().BeSameAs(originalFailure);
            failure.Message.Should().NotContain("private");
            ProcessOperationReaper.Count.Should().Be(0);
        }
        finally
        {
            ProcessRuntime.AdapterFactory = original;
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task KillCloseAndDisposalFailuresRemainSecondaryInLifecycleOrder(bool timeout, bool capture)
    {
        using CancellationTokenSource cancellation = new();
        FaultingProcess process = new(timeout, cancellation);
        IProcessAdapterFactory original = ProcessRuntime.AdapterFactory;
        ProcessRuntime.AdapterFactory = new Factory(() => process);
        try
        {
            Command command = PhaseFiveTestSupport.CreateCommand();
            Target entry = command.Target("work").Description("Stop.").Run(async context =>
            {
                ProcessBuilder builder = context.Process("synthetic").Timeout(TimeSpan.FromMilliseconds(20));
                if (capture)
                {
                    await builder.Capture().ConfigureAwait(false);
                }
                else
                {
                    await builder.Run().ConfigureAwait(false);
                }
            });

            int exitCode = await command.RunAsync(entry, [], cancellation.Token)
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            exitCode.Should().Be(timeout ? 1 : 130);
            Exception failure = command.LastExecutionOutcome!.Targets.Single().PrimaryException!;
            failure.Should().BeOfType(timeout ? typeof(ProcessTimeoutException) : typeof(OperationCanceledException));
            Flatten(failure.InnerException!).Should().Equal(process.KillFailure, process.CloseFailure, process.DisposeFailure);
            process.DisposeCount.Should().Be(1);
            process.HasExited.Should().BeTrue();
            ProcessOperationReaper.Count.Should().Be(0);
        }
        finally
        {
            ProcessRuntime.AdapterFactory = original;
        }
    }

    [Fact]
    public async Task LateFailureHistoryIsBoundedAndEveryResourceIsDisposed()
    {
        long before = ProcessOperationReaper.FailureCount;
        Resource[] resources = Enumerable.Range(0, 200).Select(_ => new Resource()).ToArray();
        foreach (Resource resource in resources)
        {
            ProcessOperationReaper.Observe(Task.FromException(new IOException("late operation")), resource);
        }

        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(5));
        await ProcessOperationReaper.WaitForEmptyAsync(deadline.Token);

        ProcessOperationReaper.FailureCount.Should().Be(before + 400);
        ProcessOperationReaper.FailureSamples.Length.Should().Be(32);
        resources.Should().OnlyContain(resource => resource.DisposeCount == 1);
        ProcessOperationReaper.Count.Should().Be(0);
    }

    private static IEnumerable<Exception> Flatten(Exception exception)
        => exception is AggregateException aggregate
            ? aggregate.InnerExceptions.SelectMany(Flatten)
            : [exception];

    private sealed class Factory(Func<IProcessAdapter> create) : IProcessAdapterFactory
    {
        public IProcessAdapter Create(ProcessStartInfo startInfo) => create();
    }

    private sealed class Resource : IDisposable
    {
        internal int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
            throw new IOException("late disposal");
        }
    }

    private sealed class FaultingProcess(bool timeout, CancellationTokenSource cancellation) : IProcessAdapter
    {
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Stream StandardOutput => Stream.Null;

        public Stream StandardError => Stream.Null;

        public int Id => 1;

        public bool HasExited => _exit.Task.IsCompleted;

        public int ExitCode => 0;

        internal IOException KillFailure { get; } = new("kill detail");

        internal IOException CloseFailure { get; } = new("close detail");

        internal IOException DisposeFailure { get; } = new("dispose detail");

        internal int DisposeCount { get; private set; }

        public bool Start() => true;

        public Task WaitForExitAsync()
        {
            if (!timeout)
            {
                cancellation.Cancel();
            }
            return _exit.Task;
        }

        public void KillTree()
        {
            _exit.TrySetResult();
            throw KillFailure;
        }

        public void CloseOutput() => throw CloseFailure;

        public void Dispose()
        {
            DisposeCount++;
            throw DisposeFailure;
        }
    }
}
