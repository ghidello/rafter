namespace Sotsera.Rafter;

internal sealed class ConsoleCancellationCoordinator(IConsoleSignalSource signalSource)
{
    private readonly HashSet<Registration> _registrations = [];
    private readonly IConsoleSignalSource _signalSource = signalSource;
    private readonly Lock _sync = new();
    private IDisposable? _subscription;

    internal static ConsoleCancellationCoordinator Shared { get; } = new(ConsoleSignalSource.Instance);

    internal Lease Register(CancellationToken callerToken)
    {
        CancellationTokenSource signalCancellation = new();
        CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            callerToken,
            signalCancellation.Token);
        Registration registration = new(signalCancellation, linkedCancellation);
        try
        {
            lock (_sync)
            {
                if (_registrations.Count == 0)
                {
                    _subscription = _signalSource.Subscribe(HandleSignal);
                }

                _registrations.Add(registration);
            }
        }
        catch
        {
            linkedCancellation.Dispose();
            signalCancellation.Dispose();
            throw;
        }

        return new Lease(this, registration);
    }

    private void HandleSignal(ConsoleSignal signal)
    {
        List<CancellationTokenSource> cancellations = [];
        lock (_sync)
        {
            foreach (Registration registration in _registrations)
            {
                if (!registration.LinkedCancellation.IsCancellationRequested && !registration.SignalRequested)
                {
                    registration.SignalRequested = true;
                    cancellations.Add(registration.SignalCancellation);
                }
            }
        }

        signal.Handled = cancellations.Count > 0;
        foreach (CancellationTokenSource cancellation in cancellations)
        {
            try
            {
                cancellation.Cancel();
            }
            catch (AggregateException)
            {
                // A callback registered by application code must not escape the process-wide signal handler.
            }
            catch (ObjectDisposedException)
            {
                // The invocation settled after the signal snapshot was taken.
            }
        }
    }

    private void Unregister(Registration registration)
    {
        IDisposable? subscription = null;
        lock (_sync)
        {
            _registrations.Remove(registration);
            if (_registrations.Count == 0)
            {
                subscription = _subscription;
                _subscription = null;
            }
        }

        subscription?.Dispose();
        registration.LinkedCancellation.Dispose();
        registration.SignalCancellation.Dispose();
    }

    internal sealed class Registration(
        CancellationTokenSource signalCancellation,
        CancellationTokenSource linkedCancellation)
    {
        internal CancellationTokenSource SignalCancellation { get; } = signalCancellation;

        internal CancellationTokenSource LinkedCancellation { get; } = linkedCancellation;

        internal bool SignalRequested { get; set; }
    }

    internal sealed class Lease : IDisposable
    {
        private readonly ConsoleCancellationCoordinator _owner;
        private Registration? _registration;

        internal Lease(ConsoleCancellationCoordinator owner, Registration registration)
        {
            _owner = owner;
            _registration = registration;
        }

        internal CancellationToken Token => _registration?.LinkedCancellation.Token
            ?? throw new ObjectDisposedException(nameof(Lease));

        public void Dispose()
        {
            Registration? registration = Interlocked.Exchange(ref _registration, null);
            if (registration is not null)
            {
                _owner.Unregister(registration);
            }
        }
    }

    private sealed class ConsoleSignalSource : IConsoleSignalSource
    {
        internal static ConsoleSignalSource Instance { get; } = new();

        public IDisposable Subscribe(Action<ConsoleSignal> handler) => new Subscription(handler);

        private sealed class Subscription : IDisposable
        {
            private ConsoleCancelEventHandler? _handler;

            internal Subscription(Action<ConsoleSignal> handler)
            {
                _handler = (_, arguments) =>
                {
                    ConsoleSignal signal = new();
                    handler(signal);
                    arguments.Cancel = signal.Handled;
                };
                Console.CancelKeyPress += _handler;
            }

            public void Dispose()
            {
                ConsoleCancelEventHandler? handler = Interlocked.Exchange(ref _handler, null);
                if (handler is not null)
                {
                    Console.CancelKeyPress -= handler;
                }
            }
        }
    }
}
