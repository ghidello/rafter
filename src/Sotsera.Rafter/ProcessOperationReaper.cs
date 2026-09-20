using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Sotsera.Rafter;

internal static class ProcessOperationReaper
{
    private const int MaximumFailureSamples = 32;
    private static readonly Queue<Exception> Failures = new();
    private static readonly Lock FailureSync = new();
    private static readonly ConcurrentDictionary<Guid, Task> Operations = new();
    private static long _failureCount;

    internal static int Count => Operations.Count;

    internal static long FailureCount => Interlocked.Read(ref _failureCount);

    internal static ImmutableArray<Exception> FailureSamples
    {
        get
        {
            lock (FailureSync)
            {
                return [.. Failures];
            }
        }
    }

    internal static async Task WaitForEmptyAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task[] operations = [.. Operations.Values];
            if (operations.Length == 0)
            {
                return;
            }

            try
            {
                await Task.WhenAll(operations).WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch when (!cancellationToken.IsCancellationRequested)
            {
                // The observer records late failures and removes every completed operation.
            }

            await Task.Yield();
        }
    }

    internal static void Observe(Task task, IDisposable resource)
    {
        Guid id = Guid.NewGuid();
        Operations[id] = task;
        _ = ObserveCoreAsync(id, task, resource);
    }

    private static async Task ObserveCoreAsync(Guid id, Task task, IDisposable resource)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            RecordFailure(exception);
        }
        finally
        {
            try
            {
                resource.Dispose();
            }
            catch (Exception exception)
            {
                RecordFailure(exception);
            }
            finally
            {
                Operations.TryRemove(id, out _);
            }
        }
    }

    private static void RecordFailure(Exception exception)
    {
        lock (FailureSync)
        {
            if (Failures.Count == MaximumFailureSamples)
            {
                Failures.Dequeue();
            }

            Failures.Enqueue(exception);
            Interlocked.Increment(ref _failureCount);
        }
    }
}
