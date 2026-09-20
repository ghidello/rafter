using System.Diagnostics;

namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class ProcessDeadlineTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SettlingADrainDisposesItsUnusedDeadlineBeforeTheOperationReturns(bool authoredTimeout)
    {
        TrackingTimeProvider clock = new();
        DeferredDrain process = new();
        IProcessAdapterFactory originalFactory = ProcessRuntime.AdapterFactory;
        ProcessRuntimePolicy originalPolicy = ProcessRuntime.Policy;
        ProcessRuntime.AdapterFactory = new Factory(process);
        ProcessRuntime.Policy = originalPolicy with { TimeProvider = clock };
        Task<int>? run = null;
        try
        {
            Command command = PhaseFiveTestSupport.CreateCommand();
            Target work = command.Target("work").Description("Drain.")
                .Run(context =>
                {
                    ProcessBuilder builder = context.Process("synthetic");
                    return (authoredTimeout ? builder.Timeout(TimeSpan.FromHours(1)) : builder).Capture();
                });
            run = command.RunAsync(work, [], TestContext.Current.CancellationToken);
            await clock.DrainDeadlineCreated.Task.WaitAsync(TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
            clock.ActiveTimers.Should().Be(1);
            process.Release();

            (await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Should().Be(0);

            clock.ActiveTimers.Should().Be(0);
            process.Disposed.Should().BeTrue();
            ProcessOperationReaper.Count.Should().Be(0);
        }
        finally
        {
            process.Release();
            try
            {
                if (run is not null)
                {
                    await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                }
            }
            finally
            {
                ProcessRuntime.AdapterFactory = originalFactory;
                ProcessRuntime.Policy = originalPolicy;
            }
        }
    }

    private sealed class Factory(DeferredDrain process) : IProcessAdapterFactory
    {
        public IProcessAdapter Create(ProcessStartInfo startInfo) => process;
    }

    private sealed class DeferredDrain : IProcessAdapter
    {
        private readonly PendingStream _stream = new();

        public Stream StandardOutput => _stream;

        public Stream StandardError => Stream.Null;

        public int Id => 1;

        public bool HasExited => true;

        public int ExitCode => 0;

        internal bool Disposed { get; private set; }

        public bool Start() => true;

        public Task WaitForExitAsync() => Task.CompletedTask;

        public void KillTree() => throw new InvalidOperationException("Already exited.");

        public void CloseOutput() => Release();

        public void Dispose()
        {
            Disposed = true;
            _stream.Dispose();
        }

        internal void Release() => _stream.Release.TrySetResult();
    }

    private sealed class PendingStream : MemoryStream
    {
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }
    }

    private sealed class TrackingTimeProvider : TimeProvider
    {
        private int _activeTimers;

        internal int ActiveTimers => Volatile.Read(ref _activeTimers);

        internal TaskCompletionSource DrainDeadlineCreated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Interlocked.Increment(ref _activeTimers);
            if (dueTime < TimeSpan.FromHours(1))
            {
                DrainDeadlineCreated.TrySetResult();
            }
            return new TrackedTimer(() => Interlocked.Decrement(ref _activeTimers));
        }
    }

    private sealed class TrackedTimer(Action dispose) : ITimer
    {
        private Action? _dispose = dispose;

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
