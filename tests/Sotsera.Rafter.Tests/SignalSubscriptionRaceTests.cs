namespace Sotsera.Rafter.Tests;

public sealed class SignalSubscriptionRaceTests
{
    [Fact]
    public void RetiredHandlerSnapshotsCannotCancelTheNextSubscription()
    {
        RetainingSource source = new();
        ConsoleCancellationCoordinator coordinator = new(source);
        using (coordinator.Register(CancellationToken.None))
        {
        }
        Action<ConsoleSignal> retired = source.Handler!;
        using ConsoleCancellationCoordinator.Lease current = coordinator.Register(CancellationToken.None);

        ConsoleSignal stale = new();
        retired(stale);
        stale.Handled.Should().BeFalse();
        current.Token.IsCancellationRequested.Should().BeFalse();
        ConsoleSignal active = new();
        source.Handler!(active);
        active.Handled.Should().BeTrue();
        current.Token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task NewRegistrationCannotInstallASecondHandlerDuringFinalUnsubscription()
    {
        using PausedSource source = new();
        ConsoleCancellationCoordinator coordinator = new(source);
        ConsoleCancellationCoordinator.Lease first = coordinator.Register(CancellationToken.None);
        Task removing = Task.Run(first.Dispose, TestContext.Current.CancellationToken);
        Task<ConsoleCancellationCoordinator.Lease>? registering = null;
        try
        {
            await source.Removing.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            registering = Task.Run(() => coordinator.Register(CancellationToken.None), TestContext.Current.CancellationToken);
            await Task.WhenAny(registering, Task.Delay(100, TestContext.Current.CancellationToken));
        }
        finally
        {
            source.Release.Set();
            await removing.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (registering is not null)
            {
                using ConsoleCancellationCoordinator.Lease next = await registering
                    .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
        }

        source.MaximumHandlers.Should().Be(1);
        source.ActiveHandlers.Should().Be(0);
    }

    private sealed class PausedSource : IConsoleSignalSource, IDisposable
    {
        private int _active;
        private int _maximum;

        internal TaskCompletionSource Removing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal ManualResetEventSlim Release { get; } = new();

        internal int MaximumHandlers => Volatile.Read(ref _maximum);

        internal int ActiveHandlers => Volatile.Read(ref _active);

        public IDisposable Subscribe(Action<ConsoleSignal> handler)
        {
            int count = Interlocked.Increment(ref _active);
            if (count > Volatile.Read(ref _maximum))
            {
                Volatile.Write(ref _maximum, count);
            }
            return new Subscription(() =>
            {
                Removing.TrySetResult();
                Release.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
                Interlocked.Decrement(ref _active);
            });
        }

        public void Dispose() => Release.Dispose();
    }

    private sealed class Subscription(Action callback) : IDisposable
    {
        private Action? _callback = callback;

        public void Dispose() => Interlocked.Exchange(ref _callback, null)?.Invoke();
    }

    private sealed class RetainingSource : IConsoleSignalSource
    {
        internal Action<ConsoleSignal>? Handler { get; private set; }

        public IDisposable Subscribe(Action<ConsoleSignal> handler)
        {
            Handler = handler;
            return new Subscription(() => { });
        }
    }
}
