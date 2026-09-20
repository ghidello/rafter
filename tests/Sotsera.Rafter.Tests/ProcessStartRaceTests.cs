using System.Diagnostics;

namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class ProcessStartRaceTests
{
    [Theory]
    [InlineData("success", false, false)]
    [InlineData("success", true, false)]
    [InlineData("false", false, false)]
    [InlineData("false", true, false)]
    [InlineData("throw", false, false)]
    [InlineData("throw", true, false)]
    [InlineData("success", false, true)]
    [InlineData("success", true, true)]
    [InlineData("false", false, true)]
    [InlineData("false", true, true)]
    [InlineData("throw", false, true)]
    [InlineData("throw", true, true)]
    public async Task RequestsDuringSynchronousStartKeepTheirDefinedPrecedence(string start, bool timeout, bool capture)
    {
        IProcessAdapterFactory originalFactory = ProcessRuntime.AdapterFactory;
        ProcessRuntimePolicy originalPolicy = ProcessRuntime.Policy;
        try
        {
            for (int iteration = 0; iteration < 10; iteration++)
            {
                await RunIterationAsync(start, timeout, capture).ConfigureAwait(true);
            }
        }
        finally
        {
            ProcessRuntime.AdapterFactory = originalFactory;
            ProcessRuntime.Policy = originalPolicy;
        }
    }

    private static async Task RunIterationAsync(string start, bool timeout, bool capture)
    {
        using CancellationTokenSource cancellation = new();
        using ManualResetEventSlim release = new();
        GatedProcess process = new(start, release);
        using ManualClock clock = new();
        ProcessRuntime.AdapterFactory = new Factory(process);
        ProcessRuntime.Policy = ProcessRuntimePolicy.Default with { TimeProvider = clock };
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target work = command.Target("work").Description("Race startup.").Run(context =>
        {
            ProcessBuilder builder = context.Process("synthetic").Timeout(TimeSpan.FromSeconds(10));
            return capture ? builder.Capture() : (Task)builder.Run();
        });
        Task<int> run = command.RunAsync(work, [], cancellation.Token);
        try
        {
            await process.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)
                .ConfigureAwait(false);
            run.IsCompleted.Should().BeFalse();
            if (timeout)
            {
                clock.Fire();
            }
            else
            {
                cancellation.Cancel();
            }
            release.Set();
            int exit = await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)
                .ConfigureAwait(false);

            exit.Should().Be(start is "success" && !timeout ? 130 : 1);
            Exception failure = command.LastExecutionOutcome!.Targets.Single().PrimaryException!;
            failure.Should().BeOfType(start is not "success" ? typeof(ProcessStartException)
                : timeout ? typeof(ProcessTimeoutException) : typeof(OperationCanceledException));
            process.KillCount.Should().Be(start is "success" ? 1 : 0);
            process.DisposeCount.Should().Be(1);
            clock.Disposed.Should().BeTrue();
            ProcessOperationReaper.Count.Should().Be(0);
        }
        finally
        {
            release.Set();
            await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class Factory(GatedProcess process) : IProcessAdapterFactory
    {
        public IProcessAdapter Create(ProcessStartInfo startInfo) => process;
    }

    private sealed class GatedProcess(string start, ManualResetEventSlim release) : IProcessAdapter
    {
        public Stream StandardOutput => Stream.Null;

        public Stream StandardError => Stream.Null;

        public int Id => 1;

        public bool HasExited => true;

        public int ExitCode => 0;

        internal int KillCount { get; private set; }

        internal int DisposeCount { get; private set; }

        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Start()
        {
            Started.TrySetResult();
            release.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
            return start switch { "false" => false, "throw" => throw new IOException("start failure"), _ => true };
        }

        public Task WaitForExitAsync() => Task.CompletedTask;

        public void KillTree() => KillCount++;

        public void CloseOutput() { }

        public void Dispose() => DisposeCount++;
    }

    private sealed class ManualClock : TimeProvider, IDisposable
    {
        private ClockTimer? _timer;

        internal bool Disposed => _timer!.Disposed;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (_timer is not null)
            {
                return System.CreateTimer(callback, state, dueTime, period);
            }
            _timer = new ClockTimer(() => callback(state));
            return _timer;
        }

        public void Dispose() => _timer?.Dispose();

        internal void Fire() => _timer!.Fire();
    }

    private sealed class ClockTimer(Action callback) : ITimer
    {
        internal bool Disposed { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose() => Disposed = true;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        internal void Fire() => callback();
    }
}
