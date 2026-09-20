using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class ProcessCallbackScopeTests
{
    [Fact]
    public async Task EveryCallbackGetsItsOwnAuthorityAndCompletedDiscardedWorkDoesNotFailIt()
    {
        Factory factory = new(wait: false);
        IProcessAdapterFactory original = ProcessRuntime.AdapterFactory;
        ProcessRuntime.AdapterFactory = factory;
        try
        {
            ProcessBuilder? priorScope = null;
            Command command = PhaseFiveTestSupport.CreateCommand();
            Target work = command.Target("work").Description("Scope.").When(async context =>
            {
                priorScope = context.Process("synthetic");
                await priorScope.Capture().ConfigureAwait(false);
                return true;
            }).Run(async context =>
            {
                Action expired = () => _ = priorScope!.Capture();
                expired.Should().Throw<InvalidOperationException>();
                ProcessBuilder builder = context.Process("synthetic");
                await Task.WhenAll(builder.Capture(), builder.Capture()).ConfigureAwait(false);
                Task<ProcessCapture> completed = builder.Capture();
                completed.IsCompletedSuccessfully.Should().BeTrue();
            }).Finally(context => context.Process("synthetic").Run());
            command.Finally(context => context.Process("synthetic").Capture());

            (await command.RunAsync(work, [], TestContext.Current.CancellationToken)).Should().Be(0);

            factory.Processes.Length.Should().Be(6);
            factory.Processes.Should().OnlyContain(process => process.DisposeCount == 1 && process.HasExited);
            ProcessOperationReaper.Count.Should().Be(0);
        }
        finally
        {
            ProcessRuntime.AdapterFactory = original;
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ScopeClosureStopsDiscardedWorkBeforeFalseConditionsOrCallbackFailureSettle(bool condition, bool capture)
    {
        Factory factory = new(wait: true);
        IProcessAdapterFactory original = ProcessRuntime.AdapterFactory;
        ProcessRuntime.AdapterFactory = factory;
        try
        {
            int executionCalls = 0;
            int targetCleanup = 0;
            int commandCleanup = 0;
            IOException primary = new("callback failure");
            Command command = PhaseFiveTestSupport.CreateCommand();
            Target work = command.Target("work").Description("Discard.");
            if (condition)
            {
                work.When(context =>
                {
                    Discard(context);
                    return false;
                }).Run(() => executionCalls++);
            }
            else
            {
                work.Run(context =>
                {
                    Discard(context);
                    throw primary;
                });
            }
            work.Finally(() => targetCleanup++);
            command.Finally(() => commandCleanup++);

            (await command.RunAsync(work, [], TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Should().Be(1);

            executionCalls.Should().Be(0);
            targetCleanup.Should().Be(condition ? 0 : 1);
            commandCleanup.Should().Be(1);
            ExecutionRuntime.TargetResult result = command.LastExecutionOutcome!.Targets.Single();
            result.Outcome.Should().Be(ExecutionRuntime.TargetOutcome.Failed);
            result.FailurePhase.Should().Be(condition
                ? ExecutionRuntime.FailurePhase.Condition
                : ExecutionRuntime.FailurePhase.Execution);
            if (!condition)
            {
                result.PrimaryException.Should().BeSameAs(primary);
            }
            factory.Processes.Should().ContainSingle().Which.DisposeCount.Should().Be(1);
            factory.Processes.Should().OnlyContain(process => process.HasExited && process.KillCount == 1);
            ProcessOperationReaper.Count.Should().Be(0);

            void Discard(RafterContext context)
            {
                ProcessBuilder builder = context.Process("synthetic");
                _ = capture ? builder.Capture() : (Task)builder.Run();
            }
        }
        finally
        {
            ProcessRuntime.AdapterFactory = original;
        }
    }

    private sealed class Factory(bool wait) : IProcessAdapterFactory
    {
        private readonly ConcurrentBag<ImmediateProcess> _processes = [];

        internal ImmutableArray<ImmediateProcess> Processes => [.. _processes];

        public IProcessAdapter Create(ProcessStartInfo startInfo)
        {
            ImmediateProcess process = new(wait);
            _processes.Add(process);
            return process;
        }
    }

    private sealed class ImmediateProcess(bool wait) : IProcessAdapter
    {
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Stream StandardOutput => Stream.Null;

        public Stream StandardError => Stream.Null;

        public int Id => 1;

        public bool HasExited => _exit.Task.IsCompleted;

        public int ExitCode => 0;

        internal int DisposeCount { get; private set; }

        internal int KillCount { get; private set; }

        public bool Start()
        {
            if (!wait)
            {
                _exit.TrySetResult();
            }
            return true;
        }

        public Task WaitForExitAsync() => _exit.Task;

        public void KillTree()
        {
            KillCount++;
            _exit.TrySetResult();
        }

        public void CloseOutput() { }

        public void Dispose() => DisposeCount++;
    }
}
