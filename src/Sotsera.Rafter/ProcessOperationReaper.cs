using System.Collections.Concurrent;

namespace Sotsera.Rafter;

internal static class ProcessOperationReaper
{
    private static readonly ConcurrentQueue<Exception> Failures = new();
    private static readonly ConcurrentDictionary<Guid, Task> Operations = new();

    internal static int Count => Operations.Count;

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
            Failures.Enqueue(exception);
        }
        finally
        {
            try
            {
                resource.Dispose();
            }
            catch (Exception exception)
            {
                Failures.Enqueue(exception);
            }
            finally
            {
                Operations.TryRemove(id, out _);
            }
        }
    }
}
