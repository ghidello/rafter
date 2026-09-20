using static Sotsera.Rafter.ExecutionRuntime;

namespace Sotsera.Rafter;

internal sealed class ExecutionObserver(Action<TargetNotification> callback, InvocationOutput? output = null)
{
    private readonly Lock _sync = new();
    private Action<TargetNotification>? _callback = callback;
    private Exception? _failure;

    internal Exception? Failure
    {
        get
        {
            lock (_sync)
            {
                return _failure;
            }
        }
    }

    internal void Publish(TargetNotification notification)
    {
        lock (_sync)
        {
            if (_callback is null)
            {
                return;
            }

            try
            {
                _callback(notification);
            }
            catch (Exception exception)
            {
                // Disable under the same lock as delivery, before another target can notify the observer.
                _callback = null;
                _failure = exception;
                output?.Fail(exception);
            }
        }
    }
}
