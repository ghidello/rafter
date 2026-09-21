using System.Diagnostics;

namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class ProcessFailureAggregationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task BothDrainFailuresRemainAvailable(bool timeout, bool capture)
    {
        FaultingProcess process = new(timeout);
        IProcessAdapterFactory original = ProcessRuntime.AdapterFactory;
        ProcessRuntime.AdapterFactory = new Factory(process);
        try
        {
            Command command = PhaseFiveTestSupport.CreateCommand();
            Target work = command.Target("work").Description("Drain both streams.").Run(async context =>
            {
                ProcessBuilder builder = context.Process("synthetic").Timeout(TimeSpan.FromMilliseconds(30));
                if (capture)
                {
                    await builder.Capture().ConfigureAwait(false);
                }
                else
                {
                    await builder.Run().ConfigureAwait(false);
                }
            });

            int exitCode = await command.RunAsync(work, [], TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            exitCode.Should().Be(1);
            Exception failure = command.LastExecutionOutcome!.Targets.Single().PrimaryException!;
            failure.Should().BeOfType(timeout ? typeof(ProcessTimeoutException) : typeof(ProcessException));
            Flatten(failure.InnerException!).Should().Equal(process.OutputFailure, process.ErrorFailure);
            process.DisposeCount.Should().Be(1);
            ProcessOperationReaper.Count.Should().Be(0);
        }
        finally
        {
            ProcessRuntime.AdapterFactory = original;
        }
    }

    [Fact]
    public async Task TheReaperRetainsEveryCombinedLateFailure()
    {
        IOException first = new("first late failure");
        IOException second = new("second late failure");
        TaskCompletionSource firstOperation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondOperation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Resource resource = new();
        long before = ProcessOperationReaper.FailureCount;
        ProcessOperationReaper.Observe(Task.WhenAll(firstOperation.Task, secondOperation.Task), resource);

        firstOperation.SetException(first);
        secondOperation.SetException(second);
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(5));
        await ProcessOperationReaper.WaitForEmptyAsync(deadline.Token);

        ProcessOperationReaper.FailureCount.Should().Be(before + 1);
        Flatten(ProcessOperationReaper.FailureSamples[^1]).Should().BeEquivalentTo([first, second]);
        resource.DisposeCount.Should().Be(1);
    }

    private static IEnumerable<Exception> Flatten(Exception exception)
        => exception is AggregateException aggregate
            ? aggregate.InnerExceptions.SelectMany(Flatten)
            : [exception];

    private sealed class Factory(FaultingProcess process) : IProcessAdapterFactory
    {
        public IProcessAdapter Create(ProcessStartInfo startInfo) => process;
    }

    private sealed class Resource : IDisposable
    {
        internal int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }

    private sealed class FaultingProcess : IProcessAdapter
    {
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal FaultingProcess(bool timeout)
        {
            StandardOutput = new FaultingStream(OutputFailure);
            StandardError = new FaultingStream(ErrorFailure);
            if (!timeout)
            {
                _exit.SetResult();
            }
        }

        public Stream StandardOutput { get; }

        public Stream StandardError { get; }

        public int Id => 1;

        public bool HasExited => _exit.Task.IsCompleted;

        public int ExitCode => 0;

        internal InvalidOperationException OutputFailure { get; } = new("private stdout detail");

        internal InvalidOperationException ErrorFailure { get; } = new("private stderr detail");

        internal int DisposeCount { get; private set; }

        public bool Start() => true;

        public Task WaitForExitAsync() => _exit.Task;

        public void KillTree() => _exit.TrySetResult();

        public void CloseOutput()
        {
            StandardOutput.Dispose();
            StandardError.Dispose();
        }

        public void Dispose()
        {
            DisposeCount++;
            CloseOutput();
        }
    }

    private sealed class FaultingStream(Exception failure) : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => ValueTask.FromException<int>(failure);
    }
}
