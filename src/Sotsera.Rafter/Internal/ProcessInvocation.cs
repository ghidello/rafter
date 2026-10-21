using System.Collections.Concurrent;

namespace Sotsera.Rafter.Internal;

internal sealed class ProcessInvocation(InvocationCancellation cancellation, InvocationOutput output)
{
    private int _failed;
    private readonly ConcurrentQueue<Exception> _diagnostics = new();

    public InvocationCancellation Cancellation { get; } = cancellation;
    public InvocationOutput Output { get; } = output;
    public bool Failed => Volatile.Read(ref _failed) != 0;
    public IEnumerable<Exception> Diagnostics => _diagnostics;

    public void Fail(Exception? diagnostic = null)
    {
        Interlocked.Exchange(ref _failed, 1);
        if (diagnostic is not null)
        {
            _diagnostics.Enqueue(diagnostic);
        }
    }
}
