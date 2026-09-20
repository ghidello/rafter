using System.Diagnostics;

namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class ProcessInitializationTests
{
    [Theory]
    [InlineData("id", false)]
    [InlineData("stdout", false)]
    [InlineData("stderr", false)]
    [InlineData("exit", false)]
    [InlineData("exit-fault", false)]
    [InlineData("id", true)]
    [InlineData("stdout", true)]
    [InlineData("stderr", true)]
    [InlineData("exit", true)]
    [InlineData("exit-fault", true)]
    public async Task TerminatesAnOwnedChildWhenInitializationFails(string failurePoint, bool capture)
    {
        InitializationProcess process = new(failurePoint);
        IProcessAdapterFactory original = ProcessRuntime.AdapterFactory;
        ProcessRuntime.AdapterFactory = new Factory(process);
        try
        {
            Command command = CreateCommand(capture, out Target target);

            int exitCode = await command.RunAsync(target, [], TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

            exitCode.Should().Be(1);
            process.Started.Should().BeTrue();
            process.KillCount.Should().Be(1);
            process.HasExited.Should().BeTrue();
            process.CloseCount.Should().Be(1);
            process.DisposeCount.Should().Be(1);
            process.PendingReads.Should().Be(0);
            ProcessOperationReaper.Count.Should().Be(0);
            ProcessException failure = command.LastExecutionOutcome!.Targets.Single().PrimaryException
                .Should().BeOfType<ProcessException>().Subject;
            failure.InnerException.Should().BeSameAs(process.InitializationFailure);
        }
        finally
        {
            process.Release();
            ProcessRuntime.AdapterFactory = original;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainsOwnershipOfAPartialDrainThatSettlesAfterInitializationFailure(bool capture)
    {
        InitializationProcess process = new("stderr", retainDrain: true);
        IProcessAdapterFactory originalFactory = ProcessRuntime.AdapterFactory;
        ProcessRuntimePolicy originalPolicy = ProcessRuntime.Policy;
        ProcessRuntime.AdapterFactory = new Factory(process);
        ProcessRuntime.Policy = new ProcessRuntimePolicy(
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(50),
            TimeProvider.System);
        try
        {
            Command command = CreateCommand(capture, out Target target);

            int exitCode = await command.RunAsync(target, [], TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

            exitCode.Should().Be(1);
            process.KillCount.Should().Be(1);
            process.HasExited.Should().BeTrue();
            process.DisposeCount.Should().Be(0);
            ProcessOperationReaper.Count.Should().Be(1);
            ProcessException failure = command.LastExecutionOutcome!.Targets.Single().PrimaryException
                .Should().BeOfType<ProcessException>().Subject;
            failure.InnerException.Should().BeOfType<AggregateException>().Which.InnerExceptions
                .Should().Contain(process.InitializationFailure);

            process.Release();
            using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(3));
            await ProcessOperationReaper.WaitForEmptyAsync(deadline.Token);
            process.DisposeCount.Should().Be(1);
            process.PendingReads.Should().Be(0);
        }
        finally
        {
            process.Release();
            ProcessRuntime.AdapterFactory = originalFactory;
            ProcessRuntime.Policy = originalPolicy;
        }
    }

    [Theory]
    [InlineData("exit-fault")]
    [InlineData("exit-canceled")]
    public async Task CancellationStillVerifiesExitWhenTheExitObserverFails(string failurePoint)
    {
        using CancellationTokenSource cancellation = new();
        InitializationProcess process = new(failurePoint)
        {
            OnObserveExit = cancellation.Cancel,
            RetainExit = true,
        };
        IProcessAdapterFactory originalFactory = ProcessRuntime.AdapterFactory;
        ProcessRuntimePolicy originalPolicy = ProcessRuntime.Policy;
        ProcessRuntime.AdapterFactory = new Factory(process);
        ProcessRuntime.Policy = new ProcessRuntimePolicy(
            TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50), TimeProvider.System);
        try
        {
            Command command = CreateCommand(capture: true, out Target target);
            int exitCode = await command.RunAsync(target, [], cancellation.Token)
                .WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

            exitCode.Should().Be(130);
            process.KillCount.Should().Be(1);
            process.HasExited.Should().BeFalse();
            process.DisposeCount.Should().Be(0);
            ProcessOperationReaper.Count.Should().Be(1);
            command.LastExecutionOutcome!.Targets.Single().PrimaryException
                .Should().BeOfType<OperationCanceledException>();

            process.CompleteExit();
            using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(3));
            await ProcessOperationReaper.WaitForEmptyAsync(deadline.Token);
            process.DisposeCount.Should().Be(1);
        }
        finally
        {
            process.CompleteExit();
            process.Release();
            ProcessRuntime.AdapterFactory = originalFactory;
            ProcessRuntime.Policy = originalPolicy;
        }
    }

    private static Command CreateCommand(bool capture, out Target target)
    {
        Command command = PhaseFiveTestSupport.CreateCommand();
        target = command.Target("initialize").Description("Fail after child startup.")
            .Run(async context =>
            {
                ProcessBuilder process = context.Process("synthetic");
                if (capture)
                {
                    _ = await process.Capture().ConfigureAwait(false);
                }
                else
                {
                    _ = await process.Run().ConfigureAwait(false);
                }
            });
        return command;
    }

    private sealed class Factory(InitializationProcess process) : IProcessAdapterFactory
    {
        public IProcessAdapter Create(ProcessStartInfo startInfo) => process;
    }

    private sealed class InitializationProcess(string failurePoint, bool retainDrain = false) : IProcessAdapter
    {
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly PendingStream _stdout = new();
        private readonly PendingStream _stderr = new();

        public Stream StandardOutput => failurePoint is "stdout" ? throw InitializationFailure : _stdout;

        public Stream StandardError => failurePoint is "stderr" ? throw InitializationFailure : _stderr;

        public int Id => failurePoint is "id" ? throw InitializationFailure : 1;

        public bool HasExited => _exit.Task.IsCompleted;

        public int ExitCode => 0;

        internal IOException InitializationFailure { get; } = new("Expected initialization failure.");

        internal bool Started { get; private set; }

        internal int KillCount { get; private set; }

        internal int CloseCount { get; private set; }

        internal int DisposeCount { get; private set; }

        internal int PendingReads => _stdout.PendingReads + _stderr.PendingReads;

        internal Action? OnObserveExit { get; init; }

        internal bool RetainExit { get; init; }

        public bool Start()
        {
            Started = true;
            return true;
        }

        public Task WaitForExitAsync()
        {
            OnObserveExit?.Invoke();
            return failurePoint switch
            {
                "exit" => throw InitializationFailure,
                "exit-fault" => FailExitAsync(),
                "exit-canceled" => Task.FromCanceled(new CancellationToken(canceled: true)),
                _ => _exit.Task,
            };
        }

        public void KillTree()
        {
            KillCount++;
            if (!RetainExit)
            {
                CompleteExit();
            }
        }

        public void CloseOutput()
        {
            CloseCount++;
            if (!retainDrain)
            {
                Release();
            }
        }

        public void Dispose() => DisposeCount++;

        internal void CompleteExit() => _exit.TrySetResult();

        internal void Release()
        {
            _stdout.Release();
            _stderr.Release();
        }

        private async Task FailExitAsync()
        {
            await Task.Yield();
            throw InitializationFailure;
        }
    }

    private sealed class PendingStream : Stream
    {
        private readonly TaskCompletionSource<int> _read = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _reading;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        internal int PendingReads => _reading && !_read.Task.IsCompleted ? 1 : 0;

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _reading = true;
            return new ValueTask<int>(_read.Task);
        }

        internal void Release() => _read.TrySetResult(0);
    }
}
