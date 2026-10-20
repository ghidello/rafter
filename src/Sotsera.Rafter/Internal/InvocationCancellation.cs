namespace Sotsera.Rafter.Internal;

internal sealed class InvocationCancellation : IDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _source = new();
    private readonly InvocationOutput _output;
    private Task _callbacks = Task.CompletedTask;
    private bool _stopped;
    private bool _requested;

    public CancellationToken Token => _source.Token;

    public bool Requested
    {
        get
        {
            lock (_gate)
            {
                return _requested;
            }
        }
    }

    public InvocationCancellation(InvocationOutput output)
    {
        _output = output;
    }

    public void Request()
    {
        lock (_gate)
        {
            if (_requested)
            {
                return;
            }
            _requested = true;
            if (_stopped)
            {
                // Work has settled. Keep accepting interrupts during final output settlement, but do not
                // start user registrations or write notifications into destinations that are closing.
                return;
            }
            _callbacks = _source.CancelAsync();
            try
            {
                _output.CancellationRequested();
            }
            catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
            {
                // The destination retains the failure. Never throw a reporting error on the console event thread.
            }
        }
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            _stopped = true;
            return _callbacks;
        }
    }

    public void Dispose() => _source.Dispose();
}
