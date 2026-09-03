namespace Sotsera.Rafter.Tests;

public sealed class ConsoleCancellationCoordinatorTests
{
    [Fact]
    public void SharesOneSubscriptionCancelsEveryLeaseAndLetsARepeatedSignalEscape()
    {
        FakeSignalSource source = new();
        ConsoleCancellationCoordinator coordinator = new(source);
        using ConsoleCancellationCoordinator.Lease first = coordinator.Register(CancellationToken.None);
        using ConsoleCancellationCoordinator.Lease second = coordinator.Register(CancellationToken.None);

        ConsoleSignal initial = source.Raise();
        ConsoleSignal repeated = source.Raise();

        source.SubscriptionCount.Should().Be(1);
        first.Token.IsCancellationRequested.Should().BeTrue();
        second.Token.IsCancellationRequested.Should().BeTrue();
        initial.Handled.Should().BeTrue();
        repeated.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task LetsARepeatedSignalEscapeWhileTheFirstSignalIsStillCancellingRegistrations()
    {
        FakeSignalSource source = new();
        ConsoleCancellationCoordinator coordinator = new(source);
        using ConsoleCancellationCoordinator.Lease first = coordinator.Register(CancellationToken.None);
        using ConsoleCancellationCoordinator.Lease second = coordinator.Register(CancellationToken.None);
        TaskCompletionSource callbackStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int callbackCount = 0;
        bool releaseCallback = false;

        void BlockFirstCallback()
        {
            if (Interlocked.Increment(ref callbackCount) != 1)
            {
                return;
            }

            callbackStarted.SetResult();
            SpinWait spinner = default;
            while (!Volatile.Read(ref releaseCallback))
            {
                spinner.SpinOnce();
            }
        }

        using CancellationTokenRegistration firstCallback = first.Token.Register(BlockFirstCallback);
        using CancellationTokenRegistration secondCallback = second.Token.Register(BlockFirstCallback);
        Task<ConsoleSignal> initialSignal = Task.Run(source.Raise);
        ConsoleSignal repeated;
        try
        {
            await callbackStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
            repeated = source.Raise();
        }
        finally
        {
            Volatile.Write(ref releaseCallback, true);
        }

        ConsoleSignal initial = await initialSignal.WaitAsync(TestContext.Current.CancellationToken);

        initial.Handled.Should().BeTrue();
        repeated.Handled.Should().BeFalse();
    }

    [Fact]
    public void RestoresTheSubscriptionAfterTheLastLeaseAndDoesNothingWithoutAnActiveLease()
    {
        FakeSignalSource source = new();
        ConsoleCancellationCoordinator coordinator = new(source);
        ConsoleCancellationCoordinator.Lease first = coordinator.Register(CancellationToken.None);
        ConsoleCancellationCoordinator.Lease second = coordinator.Register(CancellationToken.None);

        first.Dispose();
        source.DisposeCount.Should().Be(0);
        second.Dispose();

        source.DisposeCount.Should().Be(1);
        source.HasHandler.Should().BeFalse();
        source.Raise().Handled.Should().BeFalse();
    }

    [Fact]
    public void ACallerCancelledLeaseDoesNotCauseTheSignalToBeHandled()
    {
        FakeSignalSource source = new();
        ConsoleCancellationCoordinator coordinator = new(source);
        using CancellationTokenSource caller = new();
        caller.Cancel();
        using ConsoleCancellationCoordinator.Lease lease = coordinator.Register(caller.Token);

        ConsoleSignal signal = source.Raise();

        lease.Token.IsCancellationRequested.Should().BeTrue();
        signal.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task OneSignalCancelsConcurrentCommandsAndRestoresTheirSharedSubscription()
    {
        FakeSignalSource source = new();
        ConsoleCancellationCoordinator coordinator = new(source);
        TaskCompletionSource firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        (Command first, Target firstTarget) = CreateWaitingCommand(coordinator, firstStarted);
        (Command second, Target secondTarget) = CreateWaitingCommand(coordinator, secondStarted);

        Task<int> firstInvocation = first.RunAsync(
            firstTarget,
            [],
            TestContext.Current.CancellationToken);
        Task<int> secondInvocation = second.RunAsync(
            secondTarget,
            [],
            TestContext.Current.CancellationToken);
        await Task.WhenAll(firstStarted.Task, secondStarted.Task).WaitAsync(TestContext.Current.CancellationToken);

        ConsoleSignal signal = source.Raise();
        int[] exitCodes = await Task.WhenAll(firstInvocation, secondInvocation);

        signal.Handled.Should().BeTrue();
        exitCodes.Should().Equal(130, 130);
        source.SubscriptionCount.Should().Be(1);
        source.DisposeCount.Should().Be(1);
    }

    private static (Command Command, Target Target) CreateWaitingCommand(
        ConsoleCancellationCoordinator coordinator,
        TaskCompletionSource started)
    {
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target target = command.Target("entry").Description("Entry.").Run(async context =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken).ConfigureAwait(false);
        });
        command.CancellationCoordinator = coordinator;
        return (command, target);
    }

    private sealed class FakeSignalSource : IConsoleSignalSource
    {
        private Action<ConsoleSignal>? _handler;

        internal int DisposeCount { get; private set; }

        internal bool HasHandler => _handler is not null;

        internal int SubscriptionCount { get; private set; }

        public IDisposable Subscribe(Action<ConsoleSignal> handler)
        {
            _handler.Should().BeNull();
            _handler = handler;
            SubscriptionCount++;
            return new CallbackDisposable(() =>
            {
                _handler = null;
                DisposeCount++;
            });
        }

        internal ConsoleSignal Raise()
        {
            ConsoleSignal signal = new();
            _handler?.Invoke(signal);
            return signal;
        }
    }

    private sealed class CallbackDisposable(Action callback) : IDisposable
    {
        private Action? _callback = callback;

        public void Dispose() => Interlocked.Exchange(ref _callback, null)?.Invoke();
    }
}
