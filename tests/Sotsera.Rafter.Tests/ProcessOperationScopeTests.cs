namespace Sotsera.Rafter.Tests;

public sealed class ProcessOperationScopeTests
{
    [Fact]
    public async Task ClosingWaitsForAnOperationWhoseTaskHasNotYetBeenAttached()
    {
        ProcessOperationScope scope = new();
        ProcessOperationScope.Operation operation = scope.Register();
        CancellationToken ownership = operation.OwnershipToken;
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<bool> closing = scope.CloseAndSettleAsync();
        try
        {
            ownership.IsCancellationRequested.Should().BeTrue();
            closing.IsCompleted.Should().BeFalse("registration establishes ownership before synchronous process startup");
            operation.Attach(completion.Task);
            closing.IsCompleted.Should().BeFalse("attachment is not process settlement");
            completion.SetException(new IOException("A discarded operation failed."));
            operation.Complete();

            (await closing.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Should().BeTrue();
        }
        finally
        {
            completion.TrySetResult();
        }
    }

    [Fact]
    public async Task CompletedOperationsDoNotCountAsAbandonedAndClosedScopesRejectNewWork()
    {
        ProcessOperationScope scope = new();
        ProcessOperationScope.Operation operation = scope.Register();
        operation.Attach(Task.CompletedTask);
        operation.Complete();

        (await scope.CloseAndSettleAsync()).Should().BeFalse();
        Action register = () => scope.Register();
        register.Should().Throw<InvalidOperationException>();
    }
}
