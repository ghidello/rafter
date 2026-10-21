namespace Sotsera.Rafter.Internal;

/// <summary>Tracks full operations, including failure callbacks, until this specific body has finished.</summary>
internal sealed class BodyProcesses(ProcessInvocation invocation, string name)
{
    private readonly object _gate = new();
    private readonly List<Task<ProcessResult>> _operations = [];
    private bool _closed;

    public ProcessInvocation Invocation { get; } = invocation;
    public ShutdownWindow Shutdown { get; } = new();

    public Task<ProcessResult> Run(ProcessSettings settings)
    {
        lock (_gate)
        {
            if (_closed)
            {
                throw new InvalidOperationException($"Cannot start a process after {name} has finished.");
            }
            Invocation.Cancellation.Token.ThrowIfCancellationRequested();
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var operation = ExecuteAsync(start.Task, settings);
            _operations.Add(operation);
            start.SetResult();
            return operation;
        }
    }

    public async Task FinishAsync()
    {
        Task<ProcessResult>[] unfinished;
        lock (_gate)
        {
            _closed = true;
            unfinished = _operations.Where(task => !task.IsCompleted).ToArray();
        }
        if (unfinished.Length == 0)
        {
            return;
        }
        Invocation.Fail(new InvalidOperationException(
            $"{name} finished with {unfinished.Length} unfinished process operation(s). Await or return all process work."));
        Shutdown.Request();
        foreach (var operation in unfinished)
        {
            try
            {
                await operation.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Stopping abandoned operations is expected; the unfinished-work diagnostic remains sticky.
            }
            catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
            {
                Invocation.Fail(exception);
            }
        }
    }

    private async Task<ProcessResult> ExecuteAsync(Task start, ProcessSettings settings)
    {
        await start.ConfigureAwait(false);
        return await ProcessOperation.RunAsync(settings, this).ConfigureAwait(false);
    }
}
