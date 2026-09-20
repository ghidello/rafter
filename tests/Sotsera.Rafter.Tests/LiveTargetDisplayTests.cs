using System.Text.Json;
using static Sotsera.Rafter.ExecutionRuntime;

namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class LiveTargetDisplayTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LifecycleFramesMatchAcceptedDocumentsAndDisappearBeforeSummary(bool unicode, bool color)
    {
        string profileName = $"live-{(unicode ? "unicode" : "ascii")}-{(color ? "color" : "no-color")}";
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "PresentationFixtures", "live-lifecycle.json")));
        JsonElement document = fixture.RootElement.GetProperty("documents").EnumerateArray().Single(item =>
            string.Equals(item.GetProperty("profile").GetProperty("name").GetString(), profileName, StringComparison.Ordinal));
        string[] expected = document.GetProperty("stdoutFrames").EnumerateArray()
            .Select(frame => frame.GetProperty("document").GetString()!).ToArray();
        TerminalSurfaceWriter stdout = new();
        TerminalSurfaceWriter stderr = new();
        List<string> frames = [];
        OutputCapabilities capabilities = new(false, true, true, color, unicode, true, 80);
        Command command = PhaseFiveTestSupport.CreateCommand();
        command.InvocationServicesFactory = () => new InvocationServices(_ => null, stdout, stderr,
            capabilities, capabilities, "test")
        {
            ExecutionObserver = notification =>
            {
                if (notification.PlanIndex == 0 && notification.Lifecycle != TargetLifecycle.Pending
                    || notification.PlanIndex == 1 && notification.Lifecycle is TargetLifecycle.Pending or TargetLifecycle.Settled)
                {
                    frames.Add(stdout.ToString());
                }
            },
        };
        Target work = command.Target("work").Description("Work.").Run(() => { }).Finally(() => { });
        Target entry = command.Target("entry").Description("Entry.").DependsOn(work);

        (await command.RunAsync(entry, [], TestContext.Current.CancellationToken)).Should().Be(0);

        frames.Should().Equal(expected);
        stdout.ToString().Should().NotContain("Targets").And.Contain("Command succeeded");
        stderr.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task SemanticAndConsoleOutputSurviveLiveRedrawAndCleanup()
    {
        TerminalSurfaceWriter stdout = new();
        TerminalSurfaceWriter stderr = new();
        OutputCapabilities capabilities = new(false, true, true, false, true, true, 80);
        Command command = PhaseFiveTestSupport.CreateCommand();
        command.InvocationServicesFactory = () => new InvocationServices(_ => null, stdout, stderr,
            capabilities, capabilities, "test");
        Target target = command.Target("work").Description("Work.").Run(context =>
        {
            context.Output.Line("semantic");
            Console.WriteLine("console");
            context.Output.Warning("warning");
        }).Finally(context => context.Output.Line("cleanup"));

        (await command.RunAsync(target, [], TestContext.Current.CancellationToken)).Should().Be(0);

        stdout.ToString().Should().Be("[work] semantic\n[work] console\n[work] cleanup\n\nCommand succeeded\n  ✓ [work] Succeeded\n");
        stderr.ToString().Should().Be("[work] warning: warning\n");
    }

    [Fact]
    public async Task HostPartialLinesAreNeverErasedByLaterLifecycleTransitions()
    {
        TextWriter original = Console.Out;
        TerminalSurfaceWriter terminal = new();
        try
        {
            Console.SetOut(terminal);
            Command command = CreateLiveCommand(terminal);
            Target work = command.Target("work").Description("Work.").Run(async () =>
            {
                Task host;
                using (ExecutionContext.SuppressFlow())
                {
                    host = Task.Run(() => Console.Write("host fragment"));
                }
                await host.ConfigureAwait(false);
            });

            (await command.RunAsync(work, [], TestContext.Current.CancellationToken)).Should().Be(0);

            terminal.ToString().Should().Be("host fragment\nCommand succeeded\n  ✓ [work] Succeeded\n");
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    [Fact]
    public async Task OverlappingCommandsRetainBothDisplaysAndPublishEachSummaryOnce()
    {
        TerminalSurfaceWriter terminal = new();
        Command first = CreateLiveCommand(terminal);
        Command second = CreateLiveCommand(terminal);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Target firstWork = first.Target("first").Description("First.").Run(async context =>
        {
            context.Output.Line("first payload");
            entered.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)
                .ConfigureAwait(false);
        });
        Target secondWork = second.Target("second").Description("Second.").Run(context =>
        {
            terminal.ToString().Should().Contain("[first] Running").And.Contain("[second] Running");
            context.Output.Line("second payload");
        });
        Task<int> pending = first.RunAsync(firstWork, [], TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            (await second.RunAsync(secondWork, [], TestContext.Current.CancellationToken)).Should().Be(0);
            terminal.ToString().Should().Contain("[first] Running").And.NotContain("[second] Running");
        }
        finally
        {
            release.TrySetResult();
            (await pending).Should().Be(0);
        }

        terminal.ToString().Should().Be("[first] first payload\n[second] second payload\n"
            + "\nCommand succeeded\n  ✓ [second] Succeeded\n\nCommand succeeded\n  ✓ [first] Succeeded\n");
    }

    [Fact]
    public async Task LiveSinkFailurePreservesExecutionAndCleanupAndReleasesTheDisplay()
    {
        int calls = 0;
        FailOnceWriter writer = new();
        Command command = CreateLiveCommand(writer);
        Target work = command.Target("work").Description("Work.")
            .Run(() => calls++).Finally(() => calls++);

        (await command.RunAsync(work, [], TestContext.Current.CancellationToken)).Should().Be(1);
        calls.Should().Be(2);
        command.LastOutputFailure.Should().BeOfType<IOException>();
        command.LastExecutionOutcome!.ExitCode.Should().Be(0);

        TerminalSurfaceWriter next = new();
        Command following = CreateLiveCommand(next);
        Target entry = following.Target("next").Description("Next.");
        (await following.RunAsync(entry, [], TestContext.Current.CancellationToken)).Should().Be(0);
        next.ToString().Should().Be("\nCommand succeeded\n  ✓ [next] No work\n");
    }

    private static Command CreateLiveCommand(TextWriter writer)
    {
        OutputCapabilities capabilities = new(false, true, true, false, true, true, 80);
        Command command = PhaseFiveTestSupport.CreateCommand();
        command.InvocationServicesFactory = () => new InvocationServices(_ => null, writer, writer,
            capabilities, capabilities, "test");
        return command;
    }

    private sealed class FailOnceWriter : StringWriter
    {
        private bool _failed;

        public override void Write(string? value)
        {
            if (!_failed)
            {
                _failed = true;
                throw new IOException("live sink failed");
            }

            base.Write(value);
        }
    }
}
