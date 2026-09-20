using System.Collections.Concurrent;
using static Sotsera.Rafter.ExecutionRuntime;

namespace Sotsera.Rafter.Tests;

public sealed class GraphContractMatrixTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(8, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(8, true)]
    public async Task BoundsWholeCallbackLifetimesAndAdmitsAvailableParallelism(int concurrency, bool asynchronous)
    {
        const int width = 4;
        int expected = Math.Min(concurrency, width);
        int active = 0;
        int maximum = 0;
        int entered = 0;
        int cleaned = 0;
        TaskCompletionSource firstWave = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim synchronousWave = new();
        Command command = PhaseFiveTestSupport.CreateCommand(concurrency);
        Target[] leaves = Enumerable.Range(0, width).Select(index =>
        {
            Target target = command.Target($"leaf-{index}").Description("Concurrent leaf.");
            if (asynchronous)
            {
                target.Run(async () =>
                {
                    Enter();
                    await firstWave.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)
                        .ConfigureAwait(false);
                    await Task.Yield();
                    Interlocked.Decrement(ref active);
                });
            }
            else
            {
                target.Run(() =>
                {
                    Enter();
                    synchronousWave.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).Should().BeTrue();
                    Interlocked.Decrement(ref active);
                });
            }

            return target.Finally(() => Interlocked.Increment(ref cleaned));
        }).ToArray();
        Target entry = command.Target("entry").Description("Fan in.").DependsOn(leaves);

        int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        maximum.Should().Be(expected);
        active.Should().Be(0);
        entered.Should().Be(width);
        cleaned.Should().Be(width);

        void Enter()
        {
            int current = Interlocked.Increment(ref active);
            UpdateMaximum(ref maximum, current);

            if (Interlocked.Increment(ref entered) == expected)
            {
                firstWave.TrySetResult();
                synchronousWave.Set();
            }
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public async Task FalseOrThrowingConditionsStopEveryCallbackFamily(int family, bool throws)
    {
        int conditions = 0;
        ConcurrentQueue<string> calls = new();
        InvalidDataException failure = new("condition failure");
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target shared = command.Target("shared").Description("Shared.").Run(() => calls.Enqueue("shared"));
        Target guarded = command.Target("guarded").Description("Guarded.").DependsOn(shared);
        _ = family switch
        {
            0 => guarded.When(false),
            1 => guarded.When(Condition),
            2 => guarded.When(_ => Condition()),
            _ => guarded.When(async _ =>
            {
                await Task.Yield();
                return Condition();
            }),
        };
        guarded.When(() => { calls.Enqueue("later-condition"); return true; })
            .Run(() => calls.Enqueue("guarded"))
            .Finally(() => calls.Enqueue("guarded-cleanup"));
        Target independent = command.Target("independent").Description("Independent.")
            .DependsOn(shared).Run(() => calls.Enqueue("independent"));
        Target entry = command.Target("entry").Description("Entry.").DependsOn(guarded, independent);
        command.Finally(() => calls.Enqueue("command-cleanup"));

        int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(throws ? 1 : 0);
        conditions.Should().Be(family == 0 ? 0 : 1);
        calls.Should().Equal("shared", "independent", "command-cleanup");
        TargetResult result = command.LastExecutionOutcome!.Targets.Single(t =>
            string.Equals(t.Target.Name, "guarded", StringComparison.Ordinal));
        result.Outcome.Should().Be(throws ? TargetOutcome.Failed : TargetOutcome.Skipped);
        result.PrimaryException.Should().BeSameAs(throws ? failure : null);

        bool Condition()
        {
            conditions++;
            return throws ? throw failure : false;
        }
    }

    [Fact]
    public async Task RepeatedConcurrentFailuresKeepPlanOrderAndPermitSequentialReuse()
    {
        Command command = PhaseFiveTestSupport.CreateCommand(concurrency: 8);
        int cleanupCalls = 0;
        Target[] leaves = Enumerable.Range(0, 4).Select(index => command.Target($"leaf-{index}")
            .Description("Failing leaf.")
            .Run(async () =>
            {
                await Task.Yield();
                throw new InvalidDataException($"failure-{index}");
            })
            .Finally(() =>
            {
                Interlocked.Increment(ref cleanupCalls);
                throw new IOException($"cleanup-{index}");
            })).ToArray();
        Target entry = command.Target("entry").Description("Entry.").DependsOn(leaves);

        for (int iteration = 0; iteration < 25; iteration++)
        {
            int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);
            exitCode.Should().Be(1);
            ExecutionOutcome outcome = command.LastExecutionOutcome!;
            outcome.Targets.Select(t => t.Target.Name).Should().Equal("leaf-0", "leaf-1", "leaf-2", "leaf-3", "entry");
            outcome.Targets.Take(4).Select(t => t.PrimaryException!.Message)
                .Should().Equal("failure-0", "failure-1", "failure-2", "failure-3");
            outcome.Targets.Take(4).Select(t => t.CleanupException!.Message)
                .Should().Equal("cleanup-0", "cleanup-1", "cleanup-2", "cleanup-3");
            outcome.Targets[^1].DirectBlockers.Should().Equal(leaves.Select(t => t.Authored.Id));
        }

        cleanupCalls.Should().Be(100);
    }

    private static void UpdateMaximum(ref int maximum, int current)
    {
        int observed;
        do
        {
            observed = Volatile.Read(ref maximum);
        }
        while (current > observed && Interlocked.CompareExchange(ref maximum, current, observed) != observed);
    }
}
