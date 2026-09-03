using System.Collections.Concurrent;
using System.Collections.Immutable;
using static Sotsera.Rafter.CommandModel;
using static Sotsera.Rafter.ExecutionRuntime;

namespace Sotsera.Rafter.Tests;

public sealed class PhaseFiveGraphTests
{
    [Fact]
    public async Task PlansDiamondsDependencyFirstAndRunsSharedDependenciesOnce()
    {
        ConcurrentQueue<string> calls = new();
        Command command = PhaseFiveTestSupport.CreateCommand(concurrency: 2);
        Target shared = command.Target("shared").Description("Shared.").Run(() => calls.Enqueue("shared"));
        Target left = command.Target("left").Description("Left.").DependsOn(shared).Run(() => calls.Enqueue("left"));
        Target right = command.Target("right")
            .Description("Right.")
            .DependsOn(shared)
            .Run(() => calls.Enqueue("right"));
        Target entry = command.Target("entry").Description("Entry.").DependsOn(left, right);

        int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        command.LastGraphPlanningResult!.Plan!.Targets.Select(static target => target.Name)
            .Should().Equal("shared", "left", "right", "entry");
        calls.Should().ContainSingle(static call => call == "shared");
        command.LastExecutionOutcome!.Targets.Select(static result => result.Shape)
            .Should().Equal(
                SuccessfulShape.Executed,
                SuccessfulShape.Executed,
                SuccessfulShape.Executed,
                SuccessfulShape.Aggregate);
    }

    [Fact]
    public async Task IgnoresDisconnectedCyclesAndWorkingDirectories()
    {
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target entry = command.Target("entry").Description("Entry.");
        Target first = command.Target("first").Description("First.").WorkingDirectory("../escape");
        Target second = command.Target("second").Description("Second.");
        first.DependsOn(second);
        second.DependsOn(first);

        int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        command.LastGraphPlanningResult!.Plan!.Targets.Should().ContainSingle().Which.Name.Should().Be("entry");
        command.LastInvocationPaths!.TargetDirectories.Keys.Should().ContainSingle()
            .Which.Should().Be(entry.Authored.Id);
    }

    [Fact]
    public async Task ReportsReachableCyclesBeforeBindingOrCleanup()
    {
        int bindingCallbacks = 0;
        int executionCallbacks = 0;
        int cleanupCallbacks = 0;
        Command command = PhaseFiveTestSupport.CreateCommand();
        command.Option<int>("value").Description("Value.").Default(1).Validate(value =>
        {
            bindingCallbacks++;
            return value > 0;
        }, "Positive.");
        Target first = command.Target("first").Description("First.").Run(() => executionCallbacks++);
        Target second = command.Target("second").Description("Second.").Run(() => executionCallbacks++);
        first.DependsOn(second);
        second.DependsOn(first);
        command.Finally(() => cleanupCallbacks++);

        int exitCode = await command.RunAsync(first, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(2);
        command.LastGraphPlanningResult!.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be("RAFTER1401");
        bindingCallbacks.Should().Be(0);
        executionCallbacks.Should().Be(0);
        cleanupCallbacks.Should().Be(0);
        command.LastBindingResult.Should().BeNull();
        command.LastInvocationPaths.Should().BeNull();
    }

    [Fact]
    public async Task OrdersMultipleCycleDiagnosticsByEarliestDeclaration()
    {
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target first = command.Target("first").Description("First.");
        Target second = command.Target("second").Description("Second.");
        Target third = command.Target("third").Description("Third.");
        Target fourth = command.Target("fourth").Description("Fourth.");
        Target entry = command.Target("entry").Description("Entry.").DependsOn(third, first);
        first.DependsOn(second);
        second.DependsOn(first);
        third.DependsOn(fourth);
        fourth.DependsOn(third);

        int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(2);
        command.LastGraphPlanningResult!.Diagnostics.Select(static diagnostic => diagnostic.Message)
            .Should().SatisfyRespectively(
            message => message.Should().Contain("'first'").And.Contain("'second'"),
            message => message.Should().Contain("'third'").And.Contain("'fourth'"));
    }

    [Fact]
    public async Task PlansADeepGraphWithoutRecursiveTraversal()
    {
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target current = command.Target("target-0").Description("Target 0.");
        const int targetCount = 20_000;
        for (int index = 1; index < targetCount; index++)
        {
            current = command.Target($"target-{index}").Description($"Target {index}.").DependsOn(current);
        }

        int exitCode = await command.RunAsync(current, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        command.LastGraphPlanningResult!.Plan!.Targets.Should().HaveCount(targetCount);
    }

    [Fact]
    public void TreatsAMissingDependencyAsAnInfrastructureInvariantFailure()
    {
        Guid entryId = Guid.NewGuid();
        TargetDefinition entry = new(
            entryId,
            "entry",
            "Entry.",
            [Guid.NewGuid()],
            [],
            null,
            null,
            null);
        CommandDefinition model = new(
            Guid.NewGuid(),
            new RootDefinition(Guid.NewGuid(), RootKind.Invocation, null),
            "Command.",
            1,
            [],
            ImmutableArray.Create(entry),
            null);

        Action plan = () => GraphPlanner.Plan(model, entryId);

        plan.Should().Throw<InvalidOperationException>().WithMessage("*dependency is missing*");
    }
}
