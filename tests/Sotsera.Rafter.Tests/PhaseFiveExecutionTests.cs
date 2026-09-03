using System.Collections.Concurrent;
using System.Collections.Immutable;
using static Sotsera.Rafter.ExecutionRuntime;
using static Sotsera.Rafter.PathRuntime;

namespace Sotsera.Rafter.Tests;

public sealed class PhaseFiveExecutionTests
{
    [Fact]
    public async Task AppliesConcurrencyToCompleteSynchronousCallbackLifetimes()
    {
        int active = 0;
        int entered = 0;
        int maximum = 0;
        Command command = PhaseFiveTestSupport.CreateCommand(concurrency: 2);
        Target first = command.Target("first").Description("First.").Run(Callback);
        Target second = command.Target("second").Description("Second.").Run(Callback);
        Target entry = command.Target("entry").Description("Entry.").DependsOn(first, second);

        int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        maximum.Should().Be(2);
        active.Should().Be(0);

        void Callback()
        {
            int current = Interlocked.Increment(ref active);
            UpdateMaximum(ref maximum, current);
            Interlocked.Increment(ref entered);
            SpinWait.SpinUntil(() => Volatile.Read(ref entered) == 2, TimeSpan.FromSeconds(5)).Should().BeTrue();
            Interlocked.Decrement(ref active);
        }
    }

    [Fact]
    public async Task RunsDependenciesBeforeConditionsAndStopsAtTheFirstFalseCondition()
    {
        ConcurrentQueue<string> calls = new();
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target dependency = command.Target("dependency")
            .Description("Dependency.")
            .Run(() => calls.Enqueue("dependency"));
        Target entry = command.Target("entry")
            .Description("Entry.")
            .DependsOn(dependency)
            .When(() =>
            {
                calls.Enqueue("condition-1");
                return true;
            })
            .When(() =>
            {
                calls.Enqueue("condition-2");
                return false;
            })
            .When(() =>
            {
                calls.Enqueue("condition-3");
                return true;
            })
            .Run(() => calls.Enqueue("execution"))
            .Finally(() => calls.Enqueue("cleanup"));

        int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        calls.Should().Equal("dependency", "condition-1", "condition-2");
        command.LastExecutionOutcome!.Targets[^1].Outcome.Should().Be(TargetOutcome.Skipped);
    }

    [Fact]
    public async Task BlocksOnlyFailureDependentsAndContinuesIndependentWork()
    {
        int blockedCalls = 0;
        int independentCalls = 0;
        InvalidDataException expected = new("expected");
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target failed = command.Target("failed").Description("Failed.").Run(() => throw expected);
        Target blocked = command.Target("blocked").Description("Blocked.").DependsOn(failed).Run(() => blockedCalls++);
        Target transitive = command.Target("transitive").Description("Transitive.").DependsOn(blocked);
        Target independent = command.Target("independent").Description("Independent.").Run(() => independentCalls++);
        Target entry = command.Target("entry").Description("Entry.").DependsOn(transitive, independent);

        int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(1);
        blockedCalls.Should().Be(0);
        independentCalls.Should().Be(1);
        ExecutionOutcome outcome = command.LastExecutionOutcome!;
        outcome.Targets.Single(result => result.Target.Id == failed.Authored.Id).PrimaryException
            .Should().BeSameAs(expected);
        outcome.Targets.Single(result => result.Target.Id == blocked.Authored.Id).Outcome
            .Should().Be(TargetOutcome.Blocked);
        outcome.Targets.Single(result => result.Target.Id == transitive.Authored.Id).DirectBlockers
            .Should().Equal(blocked.Authored.Id);
    }

    [Fact]
    public async Task CleanupOnlyFailureBlocksDependentsAndCommandCleanupRunsLast()
    {
        ConcurrentQueue<string> calls = new();
        InvalidOperationException expected = new("cleanup");
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target dependency = command.Target("dependency")
            .Description("Dependency.")
            .Run(() => calls.Enqueue("execute"))
            .Finally(() =>
            {
                calls.Enqueue("target-cleanup");
                throw expected;
            });
        Target entry = command.Target("entry")
            .Description("Entry.")
            .DependsOn(dependency)
            .Run(() => calls.Enqueue("dependent"));
        command.Finally(() => calls.Enqueue("command-cleanup"));

        int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(1);
        calls.Should().Equal("execute", "target-cleanup", "command-cleanup");
        TargetResult failed = command.LastExecutionOutcome!.Targets[0];
        failed.Outcome.Should().Be(TargetOutcome.Failed);
        failed.FailurePhase.Should().Be(FailurePhase.Cleanup);
        failed.PrimaryException.Should().BeSameAs(expected);
        command.LastExecutionOutcome.Targets[1].Outcome.Should().Be(TargetOutcome.Blocked);
    }

    [Fact]
    public async Task CleanupReceivesEquivalentScopesAndANonCancellableToken()
    {
        string currentDirectory = Environment.CurrentDirectory;
        RafterContext? executionContext = null;
        RafterContext? targetCleanupContext = null;
        RafterContext? commandCleanupContext = null;
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target entry = command.Target("entry")
            .Description("Entry.")
            .WorkingDirectory("target")
            .Run(context => executionContext = context)
            .Finally(context => targetCleanupContext = context);
        command.Finally(context => commandCleanupContext = context);

        int exitCode = await command.RunAsync(entry, [], cancellation.Token);

        exitCode.Should().Be(0);
        targetCleanupContext.Should().NotBeSameAs(executionContext);
        targetCleanupContext!.Root.Should().Be(executionContext!.Root);
        targetCleanupContext.WorkingDirectory.Should().Be(executionContext.WorkingDirectory);
        executionContext.CancellationToken.CanBeCanceled.Should().BeTrue();
        targetCleanupContext.CancellationToken.Should().Be(CancellationToken.None);
        commandCleanupContext!.CancellationToken.Should().Be(CancellationToken.None);
        commandCleanupContext.WorkingDirectory.Should().Be(commandCleanupContext.Root);
        Environment.CurrentDirectory.Should().Be(currentDirectory);
    }

    [Fact]
    public async Task MatchingRequestedCancellationIsCancellationButTokenlessCancellationIsFailure()
    {
        using CancellationTokenSource matchingCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        Command matching = PhaseFiveTestSupport.CreateCommand();
        Target matchingEntry = matching.Target("entry").Description("Entry.").Run(context =>
        {
            matchingCancellation.Cancel();
            context.CancellationToken.ThrowIfCancellationRequested();
        });

        int matchingExit = await matching.RunAsync(matchingEntry, [], matchingCancellation.Token);

        matchingExit.Should().Be(130);
        matching.LastExecutionOutcome!.Targets.Should().ContainSingle()
            .Which.Outcome.Should().Be(TargetOutcome.Cancelled);

        Command tokenless = PhaseFiveTestSupport.CreateCommand();
        Target tokenlessEntry = tokenless.Target("entry").Description("Entry.").Run(
            () => throw new OperationCanceledException());

        int tokenlessExit = await tokenless.RunAsync(tokenlessEntry, [], TestContext.Current.CancellationToken);

        tokenlessExit.Should().Be(1);
        tokenless.LastExecutionOutcome!.Targets.Should().ContainSingle()
            .Which.Outcome.Should().Be(TargetOutcome.Failed);
    }

    [Fact]
    public async Task CancellationStopsAdmissionWaitsForRunningWorkAndRunsQualifiedCleanup()
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int queuedCalls = 0;
        int targetCleanupCalls = 0;
        int commandCleanupCalls = 0;
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target running = command.Target("running")
            .Description("Running.")
            .Run(async context =>
            {
                started.SetResult();
                await release.Task.WaitAsync(context.CancellationToken).ConfigureAwait(false);
            })
            .Finally(() => targetCleanupCalls++);
        Target queued = command.Target("queued").Description("Queued.").Run(() => queuedCalls++);
        Target entry = command.Target("entry").Description("Entry.").DependsOn(running, queued);
        command.Finally(() => commandCleanupCalls++);

        Task<int> invocation = command.RunAsync(entry, [], cancellation.Token);
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        release.TrySetResult();
        int exitCode = await invocation;

        exitCode.Should().Be(130);
        queuedCalls.Should().Be(0);
        targetCleanupCalls.Should().Be(1);
        commandCleanupCalls.Should().Be(1);
        command.LastExecutionOutcome!.Targets.Single(result => result.Target.Id == queued.Authored.Id).Outcome
            .Should().Be(TargetOutcome.Cancelled);
    }

    [Fact]
    public async Task CallbackFreeTargetsKeepTheirDistinctSuccessfulShapes()
    {
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target noWork = command.Target("no-work").Description("No work.");
        Target aggregate = command.Target("aggregate").Description("Aggregate.").DependsOn(noWork);

        int exitCode = await command.RunAsync(aggregate, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        command.LastExecutionOutcome!.Targets.Select(static result => result.Shape)
            .Should().Equal(SuccessfulShape.NoWork, SuccessfulShape.Aggregate);
        command.LastExecutionOutcome.Targets.SelectMany(static result => result.Transitions).Should().NotContain(
            TargetLifecycle.Running);
    }

    [Fact]
    public async Task OrdinaryFailureWinsConcurrentCancellationAndCleanupFailuresRemainSecondary()
    {
        InvalidDataException executionFailure = new("execution");
        IOException targetCleanupFailure = new("target cleanup");
        FormatException commandCleanupFailure = new("command cleanup");
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target entry = command.Target("entry")
            .Description("Entry.")
            .Run(() =>
            {
                cancellation.Cancel();
                throw executionFailure;
            })
            .Finally(() => throw targetCleanupFailure);
        command.Finally(() => throw commandCleanupFailure);

        int exitCode = await command.RunAsync(entry, [], cancellation.Token);

        exitCode.Should().Be(1);
        ExecutionOutcome outcome = command.LastExecutionOutcome!;
        outcome.InvocationCancellationRequested.Should().BeTrue();
        outcome.Targets.Should().ContainSingle().Which.PrimaryException.Should().BeSameAs(executionFailure);
        outcome.Targets[0].CleanupException.Should().BeSameAs(targetCleanupFailure);
        outcome.CommandCleanupException.Should().BeSameAs(commandCleanupFailure);
    }

    [Fact]
    public async Task ConcurrentFailuresAndAuthoredBlockersRemainInPlanOrder()
    {
        TaskCompletionSource firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Command command = PhaseFiveTestSupport.CreateCommand(concurrency: 2);
        Target first = command.Target("first").Description("First.").Run(async () =>
        {
            firstStarted.SetResult();
            await secondStarted.Task.ConfigureAwait(false);
            throw new InvalidDataException("first");
        });
        Target second = command.Target("second").Description("Second.").Run(async () =>
        {
            secondStarted.SetResult();
            await firstStarted.Task.ConfigureAwait(false);
            throw new InvalidDataException("second");
        });
        Target entry = command.Target("entry").Description("Entry.").DependsOn(second, first);

        int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(1);
        command.LastExecutionOutcome!.Targets.Select(static result => result.Target.Name)
            .Should().Equal("second", "first", "entry");
        command.LastExecutionOutcome.Targets[^1].DirectBlockers.Should().Equal(second.Authored.Id, first.Authored.Id);
    }

    [Fact]
    public async Task PreCancellationSkipsPlanningBindingPathsAndCleanupWhileHelpStillWins()
    {
        int environmentReads = 0;
        int directoryReads = 0;
        int callbacks = 0;
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Command command = PhaseFiveTestSupport.CreateCommand();
        command.Option<string>("value").Description("Value.").FromEnvironment("VALUE");
        Target entry = command.Target("entry").Description("Entry.").Run(() => callbacks++);
        command.Finally(() => callbacks++);
        command.InvocationServicesFactory = () => new InvocationServices(
            _ =>
            {
                environmentReads++;
                return "value";
            },
            TextWriter.Null,
            TextWriter.Null,
            false,
            false,
            "test-command")
        {
            ReadInvocationDirectory = () =>
            {
                directoryReads++;
                return Environment.CurrentDirectory;
            },
        };

        int cancelledExit = await command.RunAsync(entry, [], cancellation.Token);
        int helpExit = await command.RunAsync(entry, ["--help"], cancellation.Token);

        cancelledExit.Should().Be(130);
        helpExit.Should().Be(0);
        environmentReads.Should().Be(0);
        directoryReads.Should().Be(0);
        callbacks.Should().Be(0);
    }

    [Fact]
    public void ExposesTheApprovedCancellationApiShape()
    {
        System.Reflection.MethodInfo run = typeof(Command).GetMethod(nameof(Command.RunAsync))!;
        System.Reflection.ParameterInfo[] parameters = run.GetParameters();

        parameters.Should().HaveCount(3);
        parameters[2].ParameterType.Should().Be<CancellationToken>();
        parameters[2].HasDefaultValue.Should().BeTrue();
        typeof(RafterContext).GetProperty(nameof(RafterContext.CancellationToken)).Should().NotBeNull();
    }

    [Fact]
    public async Task CancellationDuringSuccessfulPathInitializationQualifiesOnlyCommandCleanup()
    {
        int targetCalls = 0;
        int commandCleanupCalls = 0;
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target entry = command.Target("entry").Description("Entry.").Run(() => targetCalls++);
        command.Finally(() => commandCleanupCalls++);
        command.InvocationServicesFactory = () => CreateServices(new CancellingFileSystem(cancellation));

        int exitCode = await command.RunAsync(entry, [], cancellation.Token);

        exitCode.Should().Be(130);
        targetCalls.Should().Be(0);
        commandCleanupCalls.Should().Be(1);
    }

    [Fact]
    public async Task PathFailureDoesNotQualifyCommandCleanup()
    {
        int cleanupCalls = 0;
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target entry = command.Target("entry").Description("Entry.");
        command.Finally(() => cleanupCalls++);
        command.InvocationServicesFactory = () => CreateServices(new MissingRootFileSystem());

        int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(2);
        cleanupCalls.Should().Be(0);
        command.LastExecutionOutcome.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HoldsTheOverlapGuardThroughTargetAndCommandCleanup(bool targetCleanup)
    {
        TaskCompletionSource cleanupStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseCleanup = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target entry = command.Target("entry").Description("Entry.").Run(() => { });
        Func<Task> cleanup = async () =>
        {
            cleanupStarted.TrySetResult();
            await releaseCleanup.Task.ConfigureAwait(false);
        };
        if (targetCleanup)
        {
            entry.Finally(cleanup);
        }
        else
        {
            command.Finally(cleanup);
        }

        Task<int> invocation = command.RunAsync(entry, [], TestContext.Current.CancellationToken);
        await cleanupStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        Action overlap = () => _ = command.RunAsync(entry, [], TestContext.Current.CancellationToken);

        overlap.Should().Throw<InvalidOperationException>();
        releaseCleanup.SetResult();
        (await invocation).Should().Be(0);
        (await command.RunAsync(entry, [], TestContext.Current.CancellationToken)).Should().Be(0);
    }

    [Fact]
    public async Task ExecutesEveryConditionFamilyOnceInAuthoredOrder()
    {
        List<string> calls = [];
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target entry = command.Target("entry")
            .Description("Entry.")
            .When(true)
            .When(() =>
            {
                calls.Add("deferred");
                return true;
            })
            .When(context =>
            {
                context.Root.Should().NotBeEmpty();
                calls.Add("context");
                return true;
            })
            .When(context =>
            {
                context.CancellationToken.CanBeCanceled.Should().BeTrue();
                calls.Add("async");
                return Task.FromResult(true);
            })
            .Run(() => calls.Add("execution"));
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);

        int exitCode = await command.RunAsync(entry, [], cancellation.Token);

        exitCode.Should().Be(0);
        calls.Should().Equal("deferred", "context", "async", "execution");
    }

    [Fact]
    public async Task AThrowingConditionFailsWithoutQualifyingTargetCleanup()
    {
        InvalidDataException expected = new("condition");
        int executionCalls = 0;
        int cleanupCalls = 0;
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target entry = command.Target("entry")
            .Description("Entry.")
            .When(() => throw expected)
            .Run(() => executionCalls++)
            .Finally(() => cleanupCalls++);

        int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(1);
        executionCalls.Should().Be(0);
        cleanupCalls.Should().Be(0);
        TargetResult result = command.LastExecutionOutcome!.Targets.Should().ContainSingle().Which;
        result.FailurePhase.Should().Be(FailurePhase.Condition);
        result.PrimaryException.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task CallbackResultAndOrdinaryCancellationExceptionKeepTheirApprovedPrecedence()
    {
        using CancellationTokenSource ignoredCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        Command ignored = PhaseFiveTestSupport.CreateCommand();
        Target ignoredEntry = ignored.Target("entry").Description("Entry.").Run(() => ignoredCancellation.Cancel());

        int ignoredExit = await ignored.RunAsync(ignoredEntry, [], ignoredCancellation.Token);

        ignoredExit.Should().Be(130);
        ignored.LastExecutionOutcome!.Targets.Should().ContainSingle()
            .Which.Outcome.Should().Be(TargetOutcome.Succeeded);

        using CancellationTokenSource unrelatedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        using CancellationTokenSource unrelatedToken = new();
        OperationCanceledException expected = new(unrelatedToken.Token);
        Command unrelated = PhaseFiveTestSupport.CreateCommand();
        Target unrelatedEntry = unrelated.Target("entry").Description("Entry.").Run(() =>
        {
            unrelatedCancellation.Cancel();
            throw expected;
        });

        int unrelatedExit = await unrelated.RunAsync(unrelatedEntry, [], unrelatedCancellation.Token);

        unrelatedExit.Should().Be(1);
        unrelated.LastExecutionOutcome!.Targets.Should().ContainSingle()
            .Which.PrimaryException.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task CancellationAfterTrueConditionsStopsExecutionButKeepsCallbackFreeSuccessAtomic()
    {
        int executionCalls = 0;
        using CancellationTokenSource executableCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        Command executable = PhaseFiveTestSupport.CreateCommand();
        Target executableEntry = executable.Target("entry")
            .Description("Entry.")
            .When(() =>
            {
                executableCancellation.Cancel();
                return true;
            })
            .Run(() => executionCalls++);

        int executableExit = await executable.RunAsync(executableEntry, [], executableCancellation.Token);

        executableExit.Should().Be(130);
        executionCalls.Should().Be(0);
        executable.LastExecutionOutcome!.Targets.Should().ContainSingle()
            .Which.Outcome.Should().Be(TargetOutcome.Cancelled);

        using CancellationTokenSource noWorkCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        Command noWork = PhaseFiveTestSupport.CreateCommand();
        Target noWorkEntry = noWork.Target("entry").Description("Entry.").When(() =>
        {
            noWorkCancellation.Cancel();
            return true;
        });

        int noWorkExit = await noWork.RunAsync(noWorkEntry, [], noWorkCancellation.Token);

        noWorkExit.Should().Be(130);
        TargetResult noWorkResult = noWork.LastExecutionOutcome!.Targets.Should().ContainSingle().Which;
        noWorkResult.Outcome.Should().Be(TargetOutcome.Succeeded);
        noWorkResult.Shape.Should().Be(SuccessfulShape.NoWork);
    }

    [Fact]
    public async Task FailureAndCancellationBlockDependentsButCancelUnrelatedQueuedWork()
    {
        int dependentCalls = 0;
        int unrelatedCalls = 0;
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target failed = command.Target("failed").Description("Failed.").Run(() =>
        {
            cancellation.Cancel();
            throw new InvalidDataException("expected");
        });
        Target dependent = command.Target("dependent")
            .Description("Dependent.")
            .DependsOn(failed)
            .Run(() => dependentCalls++);
        Target unrelated = command.Target("unrelated").Description("Unrelated.").Run(() => unrelatedCalls++);
        Target entry = command.Target("entry").Description("Entry.").DependsOn(dependent, unrelated);

        int exitCode = await command.RunAsync(entry, [], cancellation.Token);

        exitCode.Should().Be(1);
        dependentCalls.Should().Be(0);
        unrelatedCalls.Should().Be(0);
        command.LastExecutionOutcome!.Targets.Single(result => result.Target.Id == dependent.Authored.Id).Outcome
            .Should().Be(TargetOutcome.Blocked);
        command.LastExecutionOutcome.Targets.Single(result => result.Target.Id == unrelated.Authored.Id).Outcome
            .Should().Be(TargetOutcome.Cancelled);
        command.LastExecutionOutcome.Targets.Single(result => result.Target.Id == entry.Authored.Id).Outcome
            .Should().Be(TargetOutcome.Blocked);
    }

    private static void UpdateMaximum(ref int maximum, int candidate)
    {
        int observed;
        do
        {
            observed = Volatile.Read(ref maximum);
            if (candidate <= observed)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref maximum, candidate, observed) != observed);
    }

    private static InvocationServices CreateServices(IFileSystemPrimitives fileSystem)
        => new(
            _ => null,
            TextWriter.Null,
            TextWriter.Null,
            false,
            false,
            "test-command")
        {
            FileSystem = fileSystem,
        };

    private sealed class CancellingFileSystem(CancellationTokenSource cancellation) : IFileSystemPrimitives
    {
        public FileSystemEntryKind GetEntryKind(string path)
        {
            cancellation.Cancel();
            return FileSystemEntryKind.Directory;
        }

        public ImmutableArray<FileSystemEntry> Enumerate(string path) => [];

        public void CreateDirectory(string path)
        {
        }

        public void DeleteFile(string path)
        {
        }

        public void DeleteDirectory(string path)
        {
        }
    }

    private sealed class MissingRootFileSystem : IFileSystemPrimitives
    {
        public FileSystemEntryKind GetEntryKind(string path) => FileSystemEntryKind.Missing;

        public ImmutableArray<FileSystemEntry> Enumerate(string path) => [];

        public void CreateDirectory(string path)
        {
        }

        public void DeleteFile(string path)
        {
        }

        public void DeleteDirectory(string path)
        {
        }
    }
}
