using System.Diagnostics;

namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class ProcessDrainClosureTests
{
    [Theory]
    [InlineData("retained", false, false)]
    [InlineData("retained", true, false)]
    [InlineData("timeout", false, false)]
    [InlineData("timeout", true, false)]
    [InlineData("cancel", false, false)]
    [InlineData("cancel", true, false)]
    [InlineData("retained", false, true)]
    [InlineData("retained", true, true)]
    [InlineData("timeout", false, true)]
    [InlineData("timeout", true, true)]
    [InlineData("cancel", false, true)]
    [InlineData("cancel", true, true)]
    public async Task ABlockedDrainClosureReturnsWithinItsBudgetAndRemainsOwned(
        string outcome, bool capture, bool blockCancellation)
    {
        using CancellationTokenSource cancellation = new();
        BlockingProcess process = new(outcome, blockCancellation);
        IProcessAdapterFactory originalFactory = ProcessRuntime.AdapterFactory;
        ProcessRuntimePolicy originalPolicy = ProcessRuntime.Policy;
        ProcessRuntime.AdapterFactory = new Factory(process);
        ProcessRuntime.Policy = new(TimeSpan.FromMilliseconds(30), TimeSpan.FromMilliseconds(30),
            TimeSpan.FromMilliseconds(30), TimeSpan.FromMilliseconds(30), TimeProvider.System);
        Task<int>? run = null;
        long failuresBefore = ProcessOperationReaper.FailureCount;
        try
        {
            Command command = PhaseFiveTestSupport.CreateCommand();
            Target entry = command.Target("work").Description("Close blocked streams.").Run(async context =>
            {
                ProcessBuilder builder = context.Process("synthetic");
                if (string.Equals(outcome, "timeout", StringComparison.Ordinal))
                {
                    builder = builder.Timeout(TimeSpan.FromMilliseconds(50));
                }
                if (capture)
                {
                    await builder.Capture().ConfigureAwait(false);
                }
                else
                {
                    await builder.Run().ConfigureAwait(false);
                }
            });
            run = command.RunAsync(entry, [], cancellation.Token);
            await process.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (string.Equals(outcome, "cancel", StringComparison.Ordinal))
            {
                cancellation.Cancel();
            }
            await process.CloseEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            int exitCode = await run.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
            VerifyBoundedOutcome(command, outcome, exitCode, process);
            process.Release();
            using CancellationTokenSource settlement = new(TimeSpan.FromSeconds(5));
            await ProcessOperationReaper.WaitForEmptyAsync(settlement.Token);
            process.DisposeCount.Should().Be(1);
            Action inspectCancellation = () => _ = process.DrainToken.WaitHandle;
            inspectCancellation.Should().Throw<ObjectDisposedException>();
            ProcessOperationReaper.FailureCount.Should().Be(failuresBefore + 1);
            Flatten(ProcessOperationReaper.FailureSamples[^1]).Should().Contain(process.CloseFailure);
        }
        finally
        {
            await RestoreAsync(process, run, originalFactory, originalPolicy);
        }
    }

    private static void VerifyBoundedOutcome(Command command, string outcome, int exitCode, BlockingProcess process)
    {
        exitCode.Should().Be(string.Equals(outcome, "cancel", StringComparison.Ordinal) ? 130 : 1);
        Type expected = outcome switch
        {
            "retained" => typeof(ProcessOutputException),
            "timeout" => typeof(ProcessTimeoutException),
            _ => typeof(OperationCanceledException),
        };
        command.LastExecutionOutcome!.Targets.Single().PrimaryException.Should().BeOfType(expected);
        ProcessOperationReaper.Count.Should().Be(1);
        process.DisposeCount.Should().Be(0);
        Action inspectCancellation = () => _ = process.DrainToken.WaitHandle;
        inspectCancellation.Should().NotThrow("the reaper must retain the cancellation source until closure settles");
    }

    private static async Task RestoreAsync(
        BlockingProcess process, Task<int>? run, IProcessAdapterFactory factory, ProcessRuntimePolicy policy)
    {
        process.Release();
        try
        {
            if (run is not null)
            {
                await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ConfigureAwait(false);
            }
            using CancellationTokenSource settlement = new(TimeSpan.FromSeconds(5));
            await ProcessOperationReaper.WaitForEmptyAsync(settlement.Token).ConfigureAwait(false);
        }
        finally
        {
            ProcessRuntime.AdapterFactory = factory;
            ProcessRuntime.Policy = policy;
        }
    }

    private static IEnumerable<Exception> Flatten(Exception exception)
        => exception is AggregateException aggregate
            ? aggregate.InnerExceptions.SelectMany(Flatten)
            : [exception];

    private sealed class Factory(BlockingProcess process) : IProcessAdapterFactory
    {
        public IProcessAdapter Create(ProcessStartInfo startInfo) => process;
    }

    private sealed class BlockingProcess : IProcessAdapter
    {
        private readonly bool _blockCancellation;
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim _release = new(initialState: false);
        private readonly PendingStream _stream;
        private int _disposeCount;

        internal BlockingProcess(string outcome, bool blockCancellation)
        {
            _blockCancellation = blockCancellation;
            _stream = new PendingStream(this);
            if (string.Equals(outcome, "retained", StringComparison.Ordinal))
            {
                _exit.TrySetResult();
            }
        }

        public Stream StandardOutput => _stream;

        public Stream StandardError => Stream.Null;

        public int Id => 1;

        public bool HasExited => _exit.Task.IsCompleted;

        public int ExitCode => 0;

        internal TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource CloseEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal IOException CloseFailure { get; } = new("late stream closure failure");

        internal CancellationToken DrainToken { get; private set; }

        internal int DisposeCount => Volatile.Read(ref _disposeCount);

        public bool Start() => true;

        public Task WaitForExitAsync() => _exit.Task;

        public void KillTree() => _exit.TrySetResult();

        public void CloseOutput()
        {
            if (!_blockCancellation)
            {
                Block();
            }
            _stream.Complete();
            if (!_blockCancellation)
            {
                throw CloseFailure;
            }
        }

        public void Dispose()
        {
            Interlocked.Increment(ref _disposeCount);
            _stream.Dispose();
            _release.Dispose();
        }

        internal void Release()
        {
            try
            {
                _release.Set();
            }
            catch (ObjectDisposedException)
            {
                // Successful settlement can dispose the gate before final cleanup.
            }
        }

        private void Block()
        {
            CloseEntered.TrySetResult();
            _release.Wait(TimeSpan.FromSeconds(5));
        }

        private sealed class PendingStream(BlockingProcess owner) : MemoryStream
        {
            private readonly TaskCompletionSource<int> _read = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private CancellationTokenRegistration _registration;

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                owner.DrainToken = cancellationToken;
                if (owner._blockCancellation)
                {
                    _registration = cancellationToken.Register(() =>
                    {
                        owner.Block();
                        throw owner.CloseFailure;
                    });
                }
                owner.ReadStarted.TrySetResult();
                return new ValueTask<int>(_read.Task);
            }

            internal void Complete() => _read.TrySetResult(0);

            protected override void Dispose(bool disposing)
            {
                _registration.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
