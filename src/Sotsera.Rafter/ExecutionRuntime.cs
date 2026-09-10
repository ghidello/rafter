using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
using static Sotsera.Rafter.CommandModel;
using static Sotsera.Rafter.GraphPlanner;
using static Sotsera.Rafter.PathRuntime;

namespace Sotsera.Rafter;

internal static class ExecutionRuntime
{
    internal enum TargetLifecycle
    {
        Pending,
        Ready,
        Running,
        CleaningUp,
        Settled,
    }

    internal enum TargetOutcome
    {
        Succeeded,
        Skipped,
        Failed,
        Cancelled,
        Blocked,
    }

    internal enum SuccessfulShape
    {
        Executed,
        Aggregate,
        NoWork,
    }

    internal enum FailurePhase
    {
        Condition,
        Execution,
        Cleanup,
    }

    internal sealed record TargetResult(
        TargetDefinition Target,
        int PlanIndex,
        TargetOutcome Outcome,
        SuccessfulShape? Shape,
        FailurePhase? FailurePhase,
        Exception? PrimaryException,
        Exception? CleanupException,
        ImmutableArray<Guid> DirectBlockers,
        ImmutableArray<TargetLifecycle> Transitions);

    internal sealed record ExecutionOutcome(
        GraphPlan Plan,
        ImmutableArray<TargetResult> Targets,
        Exception? InfrastructureException,
        Exception? CommandCleanupException,
        bool InvocationCancellationRequested,
        int ExitCode);

    internal sealed record ExecutionScope
    {
        internal ExecutionScope(
            CommandDefinition model,
            GraphPlan plan,
            BindingEngine.InvocationSnapshot snapshot,
            InvocationPaths paths,
            IFileSystemPrimitives fileSystem,
            CancellationToken cancellationToken,
            InvocationOutput? output = null)
        {
            Model = model;
            Plan = plan;
            Snapshot = snapshot;
            Paths = paths;
            FileSystem = fileSystem;
            CancellationToken = cancellationToken;
            Output = output;
        }

        internal CommandDefinition Model { get; }

        internal GraphPlan Plan { get; }

        internal BindingEngine.InvocationSnapshot Snapshot { get; }

        internal InvocationPaths Paths { get; }

        internal IFileSystemPrimitives FileSystem { get; }

        internal CancellationToken CancellationToken { get; }

        internal InvocationOutput? Output { get; }
    }

    internal static async Task<ExecutionOutcome> ExecuteAsync(ExecutionScope scope)
    {
        ImmutableArray<TargetResult> targets = [];
        Exception? infrastructureException = null;
        Exception? commandCleanupException;
        try
        {
            targets = await ExecuteTargetsAsync(
                scope).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            infrastructureException = exception;
        }

        commandCleanupException = await RunCommandCleanupAsync(
            scope.Model.Cleanup,
            scope.Snapshot,
            scope.Paths,
            scope.FileSystem,
            scope.Output).ConfigureAwait(false);

        bool cancellationRequested = scope.CancellationToken.IsCancellationRequested;
        int exitCode = GetExitCode(
            targets,
            infrastructureException,
            commandCleanupException,
            cancellationRequested);
        return new ExecutionOutcome(
            scope.Plan,
            targets,
            infrastructureException,
            commandCleanupException,
            cancellationRequested,
            exitCode);
    }

    private static async Task<ImmutableArray<TargetResult>> ExecuteTargetsAsync(ExecutionScope scope)
    {
        Dictionary<Guid, TargetNode> nodes = CreateTargetNodes(scope.Plan);
        PriorityQueue<TargetNode, int> ready = CreateReadyQueue(scope.Plan, nodes);
        Dictionary<Task<TargetCompletion>, TargetNode> running = [];
        int settledCount = 0;

        while (settledCount < nodes.Count)
        {
            settledCount += await SettleCompletedTargetsAsync(running, ready, nodes).ConfigureAwait(false);

            if (scope.CancellationToken.IsCancellationRequested)
            {
                if (running.Count > 0)
                {
                    _ = await Task.WhenAny(running.Keys).ConfigureAwait(false);
                    continue;
                }

                settledCount += SettleUnstartedAfterCancellation(scope.Plan, nodes);
                break;
            }

            (bool progressed, int newlySettled) = AdmitReadyTargets(
                scope,
                ready,
                nodes,
                running);
            settledCount += newlySettled;

            if (settledCount == nodes.Count)
            {
                break;
            }

            if (progressed)
            {
                continue;
            }

            if (running.Count == 0)
            {
                throw new InvalidOperationException(
                    "The scheduler could not make progress through a valid execution plan.");
            }

            _ = await Task.WhenAny(running.Keys).ConfigureAwait(false);
        }

        return scope.Plan.Targets.Select(target => nodes[target.Id].CreateResult()).ToImmutableArray();
    }

    private static Dictionary<Guid, TargetNode> CreateTargetNodes(GraphPlan plan)
    {
        Dictionary<Guid, TargetNode> nodes = plan.Targets
            .Select((target, index) => new TargetNode(target, index))
            .ToDictionary(static node => node.Target.Id);
        foreach (TargetNode node in nodes.Values)
        {
            foreach (Guid dependencyId in node.Target.Dependencies)
            {
                nodes[dependencyId].AddDependent(node);
            }
        }

        return nodes;
    }

    private static PriorityQueue<TargetNode, int> CreateReadyQueue(
        GraphPlan plan,
        IReadOnlyDictionary<Guid, TargetNode> nodes)
    {
        PriorityQueue<TargetNode, int> ready = new();
        foreach (TargetDefinition target in plan.Targets)
        {
            TargetNode node = nodes[target.Id];
            if (node.RemainingDependencies == 0)
            {
                MakeReady(node, ready);
            }
        }

        return ready;
    }

    private static async Task<int> SettleCompletedTargetsAsync(
        Dictionary<Task<TargetCompletion>, TargetNode> running,
        PriorityQueue<TargetNode, int> ready,
        IReadOnlyDictionary<Guid, TargetNode> nodes)
    {
        int settledCount = 0;
        Task<TargetCompletion>[] completed = running.Keys.Where(static task => task.IsCompleted).ToArray();
        foreach (Task<TargetCompletion> task in completed)
        {
            TargetNode node = running[task];
            running.Remove(task);
            TargetCompletion completion = await task.ConfigureAwait(false);
            node.Settle(completion);
            settledCount += PropagateSettlement(node, ready, nodes);
        }

        return settledCount;
    }

    private static int PropagateSettlement(
        TargetNode settled,
        PriorityQueue<TargetNode, int> ready,
        IReadOnlyDictionary<Guid, TargetNode> nodes)
    {
        int settledCount = 1;
        Queue<TargetNode> pending = new();
        pending.Enqueue(settled);
        while (pending.TryDequeue(out TargetNode? current))
        {
            foreach (TargetNode dependent in current.Dependents)
            {
                if (!dependent.SettleDependency())
                {
                    continue;
                }

                ImmutableArray<Guid> blockers = GetDirectBlockers(dependent.Target, nodes);
                if (blockers.IsEmpty)
                {
                    MakeReady(dependent, ready);
                    continue;
                }

                dependent.SettleWithoutExecution(TargetOutcome.Blocked, blockers);
                settledCount++;
                pending.Enqueue(dependent);
            }
        }

        return settledCount;
    }

    private static (bool Progressed, int SettledCount) AdmitReadyTargets(
        ExecutionScope scope,
        PriorityQueue<TargetNode, int> ready,
        IReadOnlyDictionary<Guid, TargetNode> nodes,
        IDictionary<Task<TargetCompletion>, TargetNode> running)
    {
        bool progressed = false;
        int settledCount = 0;
        while (ready.TryPeek(out TargetNode? node, out _))
        {
            if (scope.CancellationToken.IsCancellationRequested)
            {
                break;
            }

            TargetDefinition target = node.Target;
            if (target.Conditions.IsEmpty && target.Execution is null)
            {
                _ = ready.Dequeue();
                node.SettleSuccessful(GetCallbackFreeShape(target));
                settledCount += PropagateSettlement(node, ready, nodes);
                progressed = true;
                continue;
            }

            if (running.Count >= scope.Model.Concurrency)
            {
                break;
            }

            _ = ready.Dequeue();
            node.MarkRunning();
            Task<TargetCompletion> task = Task.Run(() => ExecuteTargetAsync(node, scope));
            running.Add(task, node);
            progressed = true;
        }

        return (progressed, settledCount);
    }

    private static void MakeReady(TargetNode node, PriorityQueue<TargetNode, int> ready)
    {
        node.MarkReady();
        ready.Enqueue(node, node.PlanIndex);
    }

    private static async Task<TargetCompletion> ExecuteTargetAsync(
        TargetNode node,
        ExecutionScope scope)
    {
        TargetDefinition target = node.Target;
        RafterContext context = CreateTargetContext(
            scope.Snapshot,
            scope.Paths,
            target.Id,
            scope.FileSystem,
            scope.Output!,
            target.Name,
            scope.CancellationToken);
        using ConsoleOutputCoordinator.TargetLease consoleScope = ConsoleOutputCoordinator.EnterTarget(
            context.OutputScope);
        TargetCompletion executionResult;
        try
        {
            TargetCompletion? conditionResult = await EvaluateConditionsAsync(target, context, scope.CancellationToken)
                .ConfigureAwait(false);
            if (conditionResult is not null)
            {
                return conditionResult;
            }

            if (target.Execution is null)
            {
                return TargetCompletion.Succeeded(GetCallbackFreeShape(target));
            }

            if (scope.CancellationToken.IsCancellationRequested)
            {
                return TargetCompletion.Cancelled();
            }

            executionResult = await InvokeExecutionAsync(
                target.Execution,
                context,
                scope.CancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            context.CloseOutputScope();
        }

        return await RunTargetCleanupAsync(node, scope, executionResult).ConfigureAwait(false);
    }

    private static async Task<TargetCompletion?> EvaluateConditionsAsync(
        TargetDefinition target,
        RafterContext context,
        CancellationToken cancellationToken)
    {
        foreach (NormalizedCondition condition in target.Conditions)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return TargetCompletion.Cancelled();
            }

            try
            {
                bool accepted = await InvokeWithProcessScopeAsync(
                    context,
                    () => condition.Evaluate(context)).ConfigureAwait(false);
                if (!accepted)
                {
                    return TargetCompletion.Skipped();
                }
            }
            catch (Exception exception)
            {
                return ClassifyCallbackException(exception, FailurePhase.Condition, cancellationToken);
            }
            finally
            {
                ConsoleOutputCoordinator.VerifyActiveOwnership();
            }
        }

        return null;
    }

    private static async Task<TargetCompletion> InvokeExecutionAsync(
        NormalizedCallback execution,
        RafterContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            _ = await InvokeWithProcessScopeAsync(
                context,
                async () =>
                {
                    await execution.Invoke(context).ConfigureAwait(false);
                    return true;
                }).ConfigureAwait(false);
            return TargetCompletion.Succeeded(SuccessfulShape.Executed);
        }
        catch (Exception exception)
        {
            return ClassifyCallbackException(exception, FailurePhase.Execution, cancellationToken);
        }
        finally
        {
            ConsoleOutputCoordinator.VerifyActiveOwnership();
        }
    }

    private static async Task<TargetCompletion> RunTargetCleanupAsync(
        TargetNode node,
        ExecutionScope scope,
        TargetCompletion executionResult)
    {
        TargetDefinition target = node.Target;
        NormalizedCallback? cleanup = target.Cleanup;
        if (cleanup is null)
        {
            return executionResult;
        }

        node.MarkCleaningUp();
        RafterContext cleanupContext = CreateTargetContext(
            scope.Snapshot,
            scope.Paths,
            target.Id,
            scope.FileSystem,
            scope.Output!,
            target.Name,
            CancellationToken.None);
        using ConsoleOutputCoordinator.TargetLease consoleScope = ConsoleOutputCoordinator.EnterTarget(
            cleanupContext.OutputScope);
        Exception? cleanupException;
        try
        {
            cleanupException = await TryInvokeCleanupAsync(cleanup, cleanupContext).ConfigureAwait(false);
        }
        finally
        {
            cleanupContext.CloseOutputScope();
        }
        TargetOutcome outcome = executionResult.Outcome;
        FailurePhase? failurePhase = executionResult.FailurePhase;
        Exception? primaryException = executionResult.PrimaryException;
        if (cleanupException is not null && outcome == TargetOutcome.Succeeded)
        {
            outcome = TargetOutcome.Failed;
            failurePhase = FailurePhase.Cleanup;
            primaryException = cleanupException;
        }

        return new TargetCompletion(
            outcome,
            outcome == TargetOutcome.Succeeded ? executionResult.Shape : null,
            failurePhase,
            primaryException,
            cleanupException);
    }

    private static TargetCompletion ClassifyCallbackException(
        Exception exception,
        FailurePhase failurePhase,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException cancellation
            && cancellation.CancellationToken == cancellationToken
            && cancellationToken.IsCancellationRequested)
        {
            return TargetCompletion.Cancelled(exception);
        }

        return TargetCompletion.Failed(failurePhase, exception);
    }

    private static async Task<Exception?> RunCommandCleanupAsync(
        NormalizedCallback? cleanup,
        BindingEngine.InvocationSnapshot snapshot,
        InvocationPaths paths,
        IFileSystemPrimitives fileSystem,
        InvocationOutput? output)
    {
        if (cleanup is null)
        {
            return null;
        }

        RafterContext context = output is null
            ? CreateCommandContext(snapshot, paths, fileSystem, CancellationToken.None)
            : CreateCommandContext(snapshot, paths, fileSystem, output, CancellationToken.None);
        try
        {
            return await Task.Run(() => TryInvokeCleanupAsync(cleanup, context)).ConfigureAwait(false);
        }
        finally
        {
            context.CloseOutputScope();
        }
    }

    private static async Task<Exception?> TryInvokeCleanupAsync(
        NormalizedCallback cleanup,
        RafterContext context)
    {
        try
        {
            _ = await InvokeWithProcessScopeAsync(
                context,
                async () =>
                {
                    await cleanup.Invoke(context).ConfigureAwait(false);
                    return true;
                }).ConfigureAwait(false);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
        finally
        {
            ConsoleOutputCoordinator.VerifyActiveOwnership();
        }
    }

    private static async ValueTask<T> InvokeWithProcessScopeAsync<T>(
        RafterContext context,
        Func<ValueTask<T>> callback)
    {
        ProcessOperationScope processScope = context.OpenProcessScope();
        T result = default!;
        ExceptionDispatchInfo? callbackFailure = null;
        try
        {
            result = await callback().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            callbackFailure = ExceptionDispatchInfo.Capture(exception);
        }

        bool abandoned = await context.CloseProcessScopeAsync(processScope).ConfigureAwait(false);
        callbackFailure?.Throw();
        if (abandoned)
        {
            throw new InvalidOperationException(
                "An invocation callback returned before one or more child-process operations completed.");
        }

        return result;
    }

    private static int SettleUnstartedAfterCancellation(
        GraphPlan plan,
        IReadOnlyDictionary<Guid, TargetNode> nodes)
    {
        int settledCount = 0;
        foreach (TargetDefinition target in plan.Targets)
        {
            TargetNode node = nodes[target.Id];
            if (node.IsSettled)
            {
                continue;
            }

            ImmutableArray<Guid> blockers = GetDirectBlockers(target, nodes);
            node.SettleWithoutExecution(
                blockers.IsEmpty ? TargetOutcome.Cancelled : TargetOutcome.Blocked,
                blockers);
            settledCount++;
        }

        return settledCount;
    }

    private static ImmutableArray<Guid> GetDirectBlockers(
        TargetDefinition target,
        IReadOnlyDictionary<Guid, TargetNode> nodes)
        => target.Dependencies
            .Where(dependencyId => nodes[dependencyId].Outcome is TargetOutcome.Failed or TargetOutcome.Blocked)
            .ToImmutableArray();

    private static SuccessfulShape GetCallbackFreeShape(TargetDefinition target)
        => target.Dependencies.IsEmpty ? SuccessfulShape.NoWork : SuccessfulShape.Aggregate;

    private static int GetExitCode(
        ImmutableArray<TargetResult> targets,
        Exception? infrastructureException,
        Exception? commandCleanupException,
        bool cancellationRequested)
    {
        if (infrastructureException is not null
            || targets.Any(static target =>
                target.Outcome == TargetOutcome.Failed
                && target.FailurePhase is FailurePhase.Condition or FailurePhase.Execution))
        {
            return 1;
        }

        if (cancellationRequested)
        {
            return 130;
        }

        return commandCleanupException is not null
            || targets.Any(static target => target.Outcome == TargetOutcome.Failed)
                ? 1
                : 0;
    }

    private sealed class TargetNode
    {
        private readonly List<TargetNode> _dependents = [];
        private readonly List<TargetLifecycle> _transitions = [TargetLifecycle.Pending];
        private ImmutableArray<Guid> _directBlockers = [];
        private Exception? _cleanupException;
        private FailurePhase? _failurePhase;
        private TargetOutcome? _outcome;
        private Exception? _primaryException;
        private SuccessfulShape? _shape;

        internal TargetNode(TargetDefinition target, int planIndex)
        {
            Target = target;
            PlanIndex = planIndex;
            RemainingDependencies = target.Dependencies.Length;
        }

        internal IEnumerable<TargetNode> Dependents => _dependents;

        internal TargetDefinition Target { get; }

        internal int PlanIndex { get; }

        internal int RemainingDependencies { get; private set; }

        internal TargetLifecycle Lifecycle => _transitions[^1];

        internal bool IsSettled => Lifecycle == TargetLifecycle.Settled;

        internal TargetOutcome? Outcome => _outcome;

        internal void AddDependent(TargetNode dependent) => _dependents.Add(dependent);

        internal bool SettleDependency()
        {
            if (RemainingDependencies <= 0)
            {
                throw new InvalidOperationException("A target dependency settled more than once.");
            }

            RemainingDependencies--;
            return RemainingDependencies == 0;
        }

        internal void MarkReady() => Transition(TargetLifecycle.Ready);

        internal void MarkRunning() => Transition(TargetLifecycle.Running);

        internal void MarkCleaningUp() => Transition(TargetLifecycle.CleaningUp);

        internal void SettleSuccessful(SuccessfulShape shape)
            => Settle(new TargetCompletion(TargetOutcome.Succeeded, shape, null, null, null));

        internal void SettleWithoutExecution(
            TargetOutcome outcome,
            ImmutableArray<Guid> directBlockers = default)
            => Settle(new TargetCompletion(
                outcome,
                null,
                null,
                null,
                null,
                DirectBlockers: directBlockers.IsDefault ? null : directBlockers));

        internal void Settle(TargetCompletion completion)
        {
            _outcome = completion.Outcome;
            _shape = completion.Shape;
            _failurePhase = completion.FailurePhase;
            _primaryException = completion.PrimaryException;
            _cleanupException = completion.CleanupException;
            if (completion.DirectBlockers is not null)
            {
                _directBlockers = completion.DirectBlockers.Value;
            }

            Transition(TargetLifecycle.Settled);
        }

        internal TargetResult CreateResult()
            => new(
                Target,
                PlanIndex,
                _outcome ?? throw new InvalidOperationException("The target has not settled."),
                _shape,
                _failurePhase,
                _primaryException,
                _cleanupException,
                _directBlockers,
                [.. _transitions]);

        private void Transition(TargetLifecycle lifecycle)
        {
            if (IsSettled)
            {
                throw new InvalidOperationException("A settled target cannot transition again.");
            }

            _transitions.Add(lifecycle);
        }
    }

    private sealed record TargetCompletion(
        TargetOutcome Outcome,
        SuccessfulShape? Shape,
        FailurePhase? FailurePhase,
        Exception? PrimaryException,
        Exception? CleanupException,
        ImmutableArray<Guid>? DirectBlockers = null)
    {
        internal static TargetCompletion Succeeded(SuccessfulShape shape)
            => new(TargetOutcome.Succeeded, shape, null, null, null);

        internal static TargetCompletion Skipped()
            => new(TargetOutcome.Skipped, null, null, null, null);

        internal static TargetCompletion Failed(FailurePhase phase, Exception exception)
            => new(TargetOutcome.Failed, null, phase, exception, null);

        internal static TargetCompletion Cancelled(Exception? exception = null)
            => new(TargetOutcome.Cancelled, null, null, exception, null);
    }
}
