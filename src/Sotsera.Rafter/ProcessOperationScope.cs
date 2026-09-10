namespace Sotsera.Rafter;

internal sealed class ProcessOperationScope
{
    private readonly HashSet<Operation> _operations = [];
    private readonly Lock _sync = new();
    private bool _closed;

    internal Operation Register()
    {
        lock (_sync)
        {
            if (_closed)
            {
                throw new InvalidOperationException("The process-operation scope is no longer active.");
            }

            Operation operation = new(this);
            _operations.Add(operation);
            return operation;
        }
    }

    internal void EnsureActive()
    {
        lock (_sync)
        {
            if (_closed)
            {
                throw new InvalidOperationException("The process-operation scope is no longer active.");
            }
        }
    }

    internal async Task<bool> CloseAndSettleAsync()
    {
        Operation[] active;
        lock (_sync)
        {
            _closed = true;
            active = [.. _operations];
        }

        foreach (Operation operation in active)
        {
            operation.CancelOwnership();
        }

        foreach (Operation operation in active)
        {
            await operation.ObserveAsync().ConfigureAwait(false);
        }

        return active.Length != 0;
    }

    private void Complete(Operation operation)
    {
        lock (_sync)
        {
            _operations.Remove(operation);
        }
    }

    internal sealed class Operation : IDisposable
    {
        private readonly CancellationTokenSource _ownershipCancellation = new();
        private readonly ProcessOperationScope _owner;
        private Task? _task;
        private int _completed;

        internal Operation(ProcessOperationScope owner)
        {
            _owner = owner;
        }

        internal CancellationToken OwnershipToken => _ownershipCancellation.Token;

        internal void Attach(Task task)
        {
            _task = task;
            _ = task.ContinueWith(
                static completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            if (Volatile.Read(ref _completed) != 0)
            {
                _owner.Complete(this);
            }
        }

        internal void Complete()
        {
            if (Interlocked.Exchange(ref _completed, 1) == 0)
            {
                _owner.Complete(this);
            }

            Dispose();
        }

        internal void CancelOwnership()
        {
            try
            {
                _ownershipCancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        internal async Task ObserveAsync()
        {
            Task? task = _task;
            if (task is null)
            {
                return;
            }

            try
            {
                await task.ConfigureAwait(false);
            }
            catch
            {
                // Scope settlement observes discarded task failures; callback classification is handled by its caller.
            }
        }

        public void Dispose() => _ownershipCancellation.Dispose();
    }
}
