using System.Collections.Concurrent;
using static Sotsera.Rafter.ExecutionRuntime;

namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class ExecutionObserverTests
{
    [Fact]
    public async Task PublishesRealTransitionsBeforeExecutionAndCleanupCallbacks()
    {
        ConcurrentQueue<TargetNotification> notifications = new();
        TaskCompletionSource entered = NewSignal();
        TaskCompletionSource release = NewSignal();
        TaskCompletionSource cleanupEntered = NewSignal();
        TaskCompletionSource cleanupRelease = NewSignal();
        Command command = CreateCommand(notifications.Enqueue);
        Target work = command.Target("work").Description("Work.").Run(async () =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        }).Finally(async () =>
        {
            cleanupEntered.SetResult();
            await cleanupRelease.Task.WaitAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        });
        Target entry = command.Target("entry").Description("Entry.").DependsOn(work);
        Task<int> run = command.RunAsync(entry, [], TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            TargetNotification[] initial = notifications.ToArray();
            initial.Take(2).Select(static item => item.TargetId).Should().Equal(work.Authored.Id, entry.Authored.Id);
            initial.Take(2).Should().OnlyContain(static item => item.Lifecycle == TargetLifecycle.Pending);
            initial.Where(item => item.TargetId == work.Authored.Id).Select(static item => item.Lifecycle)
                .Should().Equal(TargetLifecycle.Pending, TargetLifecycle.Ready, TargetLifecycle.Running);
            initial.Should().OnlyContain(static item => item.Outcome == null && item.Shape == null
                && item.DirectBlockers.IsEmpty);

            release.SetResult();
            await cleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            notifications.Last(item => item.TargetId == work.Authored.Id).Lifecycle
                .Should().Be(TargetLifecycle.CleaningUp);
            run.IsCompleted.Should().BeFalse();

            cleanupRelease.SetResult();
            (await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Should().Be(0);
            AssertMatchesOutcome(notifications, command.LastExecutionOutcome!);
            initial.Should().OnlyContain(static item => item.Outcome == null && item.DirectBlockers.IsEmpty);
        }
        finally
        {
            release.TrySetResult();
            cleanupRelease.TrySetResult();
            await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task SettledNotificationsCarryFinalShapesAndAuthoredDirectBlockers()
    {
        ConcurrentQueue<TargetNotification> notifications = new();
        Command command = CreateCommand(notifications.Enqueue, concurrency: 2);
        Target first = command.Target("first").Description("Fail.").Run(() => throw new IOException("first"));
        Target second = command.Target("second").Description("Fail.").Run(() => throw new IOException("second"));
        Target blocked = command.Target("blocked").Description("Blocked.").DependsOn(second, first);
        Target noWork = command.Target("empty").Description("No work.");
        Target aggregate = command.Target("aggregate").Description("Aggregate.").DependsOn(noWork);
        Target skipped = command.Target("skipped").Description("Skipped.").When(() => false).Run(() => { });
        Target executed = command.Target("executed").Description("Execute.").Run(() => { });
        Target entry = command.Target("entry").Description("Entry.").DependsOn(blocked, aggregate, skipped, executed);

        (await command.RunAsync(entry, [], TestContext.Current.CancellationToken)).Should().Be(1);

        AssertMatchesOutcome(notifications, command.LastExecutionOutcome!);
        TargetNotification[] settled = notifications.Where(static item => item.Lifecycle == TargetLifecycle.Settled)
            .ToArray();
        settled.Single(item => item.TargetId == blocked.Authored.Id).DirectBlockers
            .Should().Equal(second.Authored.Id, first.Authored.Id);
        settled.Single(item => item.TargetId == noWork.Authored.Id).Shape.Should().Be(SuccessfulShape.NoWork);
        settled.Single(item => item.TargetId == aggregate.Authored.Id).Shape.Should().Be(SuccessfulShape.Aggregate);
        settled.Single(item => item.TargetId == executed.Authored.Id).Shape.Should().Be(SuccessfulShape.Executed);
        settled.Single(item => item.TargetId == skipped.Authored.Id).Outcome.Should().Be(TargetOutcome.Skipped);
    }

    [Theory]
    [InlineData(TargetLifecycle.Pending)]
    [InlineData(TargetLifecycle.Ready)]
    [InlineData(TargetLifecycle.Running)]
    [InlineData(TargetLifecycle.CleaningUp)]
    [InlineData(TargetLifecycle.Settled)]
    public async Task ObserverFailureDisablesDeliveryWithoutChangingExecution(int failureLifecycle)
    {
        IOException failure = new("observer failed");
        ConcurrentQueue<TargetNotification> notifications = new();
        Command command = CreateCommand(notification =>
        {
            notifications.Enqueue(notification);
            if ((int)notification.Lifecycle == failureLifecycle)
            {
                throw failure;
            }
        });
        int executionCount = 0;
        int cleanupCount = 0;
        int commandCleanupCount = 0;
        Target work = command.Target("work").Description("Work.").Run(() => executionCount++)
            .Finally(() => cleanupCount++);
        Target entry = command.Target("entry").Description("Entry.").DependsOn(work);
        command.Finally(() => commandCleanupCount++);

        (await command.RunAsync(entry, [], TestContext.Current.CancellationToken)).Should().Be(1);

        command.LastInvocationStatus.Should().Be(Command.InvocationStatus.InfrastructureFailure);
        command.LastOutputFailure.Should().BeSameAs(failure);
        command.LastExecutionOutcome!.ExitCode.Should().Be(0);
        command.LastExecutionOutcome.Targets.Should().OnlyContain(static item => item.Outcome == TargetOutcome.Succeeded);
        command.LastExecutionOutcome.Targets[0].Transitions.Should().Equal(TargetLifecycle.Pending, TargetLifecycle.Ready,
            TargetLifecycle.Running, TargetLifecycle.CleaningUp, TargetLifecycle.Settled);
        executionCount.Should().Be(1);
        cleanupCount.Should().Be(1);
        commandCleanupCount.Should().Be(1);
        ((int)notifications.Last().Lifecycle).Should().Be(failureLifecycle);
        notifications.Count(item => (int)item.Lifecycle == failureLifecycle).Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObservesCancellationAndCleanupWithoutChangingCancellationClassification(bool fail)
    {
        using CancellationTokenSource cancellation = new();
        ConcurrentQueue<TargetNotification> notifications = new();
        Command command = CreateCommand(notification =>
        {
            notifications.Enqueue(notification);
            if (fail && notification.Lifecycle == TargetLifecycle.CleaningUp)
            {
                throw new IOException("observer failed");
            }
        }, concurrency: 1);
        bool cleaned = false;
        Target work = command.Target("work").Description("Cancel.").Run(context =>
        {
            cancellation.Cancel();
            context.CancellationToken.ThrowIfCancellationRequested();
        }).Finally(() => cleaned = true);
        Target entry = command.Target("entry").Description("Entry.").DependsOn(work);

        (await command.RunAsync(entry, [], cancellation.Token)).Should().Be(fail ? 1 : 130);

        cleaned.Should().BeTrue();
        command.LastExecutionOutcome!.Targets.Should().OnlyContain(static item => item.Outcome == TargetOutcome.Cancelled);
        command.LastExecutionOutcome.ExitCode.Should().Be(130);
        if (!fail)
        {
            AssertMatchesOutcome(notifications, command.LastExecutionOutcome);
        }
        notifications.Should().Contain(item => item.TargetId == work.Authored.Id
            && item.Lifecycle == TargetLifecycle.CleaningUp);
    }

    [Fact]
    public async Task ObserverFailurePreservesCallbackAndCleanupFailures()
    {
        IOException observerFailure = new("observer failed");
        IOException callbackFailure = new("callback failed");
        IOException cleanupFailure = new("cleanup failed");
        Command command = CreateCommand(_ => throw observerFailure);
        Target work = command.Target("work").Description("Work.").Run(() => throw callbackFailure)
            .Finally(() => throw cleanupFailure);
        Target entry = command.Target("entry").Description("Entry.").DependsOn(work);

        (await command.RunAsync(entry, [], TestContext.Current.CancellationToken)).Should().Be(1);

        TargetResult result = command.LastExecutionOutcome!.Targets[0];
        result.PrimaryException.Should().BeSameAs(callbackFailure);
        result.CleanupException.Should().BeSameAs(cleanupFailure);
        result.FailurePhase.Should().Be(FailurePhase.Execution);
        command.LastExecutionOutcome.InfrastructureException.Should().BeNull();
        command.LastExecutionOutcome.Targets[1].Outcome.Should().Be(TargetOutcome.Blocked);
        command.LastOutputFailure.Should().BeSameAs(observerFailure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaticAndPlainOutputEmitNoTransientLifecycleLines(bool plain)
    {
        ConcurrentQueue<TargetNotification> notifications = new();
        Command command = CreateCommand(notifications.Enqueue);
        StringWriter output = new();
        StringWriter error = new();
        Func<InvocationServices> factory = command.InvocationServicesFactory;
        command.InvocationServicesFactory = () => factory() with
        {
            StandardOutput = output,
            StandardError = error,
            StandardOutputCapabilities = PhaseFiveTestSupport.RichCapabilities,
            StandardErrorCapabilities = PhaseFiveTestSupport.RichCapabilities,
        };
        Target entry = command.Target("work").Description("Work.").Run(context => context.Output.Line("payload"));

        (await command.RunAsync(entry, plain ? ["--plain"] : [], TestContext.Current.CancellationToken)).Should().Be(0);

        notifications.Should().NotBeEmpty();
        output.ToString().ReplaceLineEndings("\n").Should().Be("[work] payload\n");
        error.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuardSerializesConcurrentDeliveryAndStopsAfterTheFirstException(bool fail)
    {
        int active = 0;
        int calls = 0;
        int overlapping = 0;
        IOException failure = new("observer failed");
        ExecutionObserver observer = new(_ =>
        {
            if (Interlocked.Increment(ref active) != 1)
            {
                Interlocked.Increment(ref overlapping);
            }

            Interlocked.Increment(ref calls);
            Thread.SpinWait(10_000);
            Interlocked.Decrement(ref active);
            if (fail)
            {
                throw failure;
            }
        });
        TargetNotification notification = new(Guid.NewGuid(), "work", 0, TargetLifecycle.Pending, null, null, []);

        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => observer.Publish(notification),
            TestContext.Current.CancellationToken)));

        calls.Should().Be(fail ? 1 : 32);
        overlapping.Should().Be(0);
        observer.Failure.Should().BeSameAs(fail ? failure : null);
    }

    [Fact]
    public async Task ObserverFailureIsScopedToOneInvocation()
    {
        int calls = 0;
        int executions = 0;
        Command command = CreateCommand(_ =>
        {
            calls++;
            throw new IOException("observer failed");
        });
        Target entry = command.Target("entry").Description("Entry.").Run(() => executions++);

        (await command.RunAsync(entry, [], TestContext.Current.CancellationToken)).Should().Be(1);
        (await command.RunAsync(entry, [], TestContext.Current.CancellationToken)).Should().Be(1);

        calls.Should().Be(2);
        executions.Should().Be(2);
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static Command CreateCommand(Action<TargetNotification> observer, int? concurrency = null)
    {
        Command command = PhaseFiveTestSupport.CreateCommand(concurrency);
        Func<InvocationServices> factory = command.InvocationServicesFactory;
        command.InvocationServicesFactory = () => factory() with { ExecutionObserver = observer };
        return command;
    }

    private static void AssertMatchesOutcome(IEnumerable<TargetNotification> notifications, ExecutionOutcome outcome)
    {
        foreach (TargetResult target in outcome.Targets)
        {
            TargetNotification[] targetNotifications = notifications.Where(item => item.TargetId == target.Target.Id)
                .ToArray();
            targetNotifications.Select(static item => item.Lifecycle).Should().Equal(target.Transitions);
            targetNotifications.Should().OnlyContain(item => item.PlanIndex == target.PlanIndex
                && item.TargetName == target.Target.Name);
            TargetNotification settled = targetNotifications[^1];
            settled.Outcome.Should().Be(target.Outcome);
            settled.Shape.Should().Be(target.Shape);
            settled.DirectBlockers.Should().Equal(target.DirectBlockers);
            targetNotifications[..^1].Should().OnlyContain(static item => item.Outcome == null && item.Shape == null
                && item.DirectBlockers.IsEmpty);
        }
    }
}
