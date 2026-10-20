using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.Evaluation.Tests;

public sealed class InvocationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\n")]
    public async Task Invalid_description_precedes_help_and_argument_errors(string? description)
    {
        using var host = new TestHost();
        var command = host.Command(description!);
        var calls = 0;
        var code = await command.RunAsync(["--help", "private-input"], _ => { calls++; });
        code.Should().Be(2);
        calls.Should().Be(0);
        host.Output.ToString().Should().BeEmpty();
        host.Error.ToString().Should().Contain("Invalid command").And.NotContain("private-input")
            .And.NotContain("Invalid input").And.NotContain("Exception");
        host.ActiveSubscriptions.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Null_bodies_are_invalid_even_for_help(bool asynchronous)
    {
        using var host = new TestHost();
        var command = host.Command();
        var code = asynchronous
            ? await command.RunAsync(["--help"], (Func<RafterContext, Task>)null!)
            : await command.RunAsync(["--help"], (Action<RafterContext>)null!);
        code.Should().Be(2);
        host.Error.ToString().Should().Contain("body must not be null");
        host.Output.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData("--unknown")]
    [InlineData("--help=true")]
    [InlineData("--quiet=false")]
    [InlineData("--plain=")]
    [InlineData("--verbose:yes")]
    [InlineData("--HELP")]
    [InlineData("-h")]
    [InlineData("--version")]
    [InlineData("@private-input")]
    [InlineData("[private-input]")]
    [InlineData("--")]
    [InlineData("")]
    public async Task Malformed_controls_are_safe_errors_even_with_help(string argument)
    {
        using var host = new TestHost();
        var calls = 0;
        (await host.Command().RunAsync(["--help", argument], _ => { calls++; })).Should().Be(2);
        calls.Should().Be(0);
        host.Output.ToString().Should().BeEmpty();
        host.Error.ToString().Should().Contain("Invalid input").And.NotContain("private-input");
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("--quiet")]
    [InlineData("--verbose")]
    [InlineData("--plain")]
    public async Task Repeated_controls_are_invalid(string control)
    {
        using var host = new TestHost();
        (await host.Command().RunAsync([control, control], _ => { })).Should().Be(2);
        host.Error.ToString().Should().Contain($"{control} may only be supplied once.");
    }

    [Fact]
    public async Task Null_arguments_are_validation_errors()
    {
        using var host = new TestHost();
        var command = host.Command();
        (await command.RunAsync(null!, _ => { })).Should().Be(2);
        (await command.RunAsync([null!], _ => { })).Should().Be(2);
    }

    [Fact]
    public async Task Quiet_and_verbose_conflict_even_for_help()
    {
        using var host = new TestHost();
        (await host.Command().RunAsync(["--help", "--quiet", "--verbose"], _ => { })).Should().Be(2);
        host.Error.ToString().Should().Contain("--quiet and --verbose cannot be combined.");
    }

    [Theory]
    [InlineData(48)]
    [InlineData(100)]
    public async Task Help_is_stdout_only_and_preserves_literal_description(int width)
    {
        using var host = new TestHost { Width = width };
        var called = false;
        (await host.Command("Print [red]literal[/] 界面 cafe\u0301.").RunAsync(
            ["--help", "--quiet", "--plain"], _ => { called = true; })).Should().Be(0);
        called.Should().BeFalse();
        host.Output.ToString().Should().Contain("[red]literal[/]").And.Contain("Command options")
            .And.Contain("Execution").And.Contain("Rafter options").And.Contain("--help")
            .And.Contain("--quiet").And.Contain("--verbose").And.Contain("--plain").And.NotContain("\x1b");
        host.Error.ToString().Should().BeEmpty();
        host.Output.Flushes.Should().Be(1);
        host.Error.Flushes.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Results_are_literal_and_quiet_hides_only_lifecycle(bool quiet)
    {
        using var host = new TestHost();
        string[] arguments = quiet ? ["--quiet", "--plain"] : ["--plain"];
        const string text = "  [red]literal[/]\rprogress\n\n界面";
        (await host.Command().RunAsync(arguments, context => context.WriteLine(text))).Should().Be(0);
        host.Output.ToString().Should().Be(text + Environment.NewLine);
        if (quiet)
        {
            host.Error.ToString().Should().BeEmpty();
        }
        else
        {
            host.Error.ToString().Should().StartWith("Print a greeting.")
                .And.Contain("[OK] Completed").And.NotContain(text);
        }
        host.Output.Disposed.Should().BeFalse();
        host.Error.Disposed.Should().BeFalse();
    }

    [Fact]
    public async Task Async_body_is_awaited_and_retains_one_root_for_its_invocation()
    {
        using var host = new TestHost { Root = "root-before" };
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RafterContext? captured = null;
        var command = host.Command();
        host.Root = "root-at-entry";
        var run = command.RunAsync(["--quiet"], async context =>
        {
            captured = context;
            await release.Task;
            context.WriteLine("finished");
        });
        host.Root = "root-after-entry";
        captured!.RootDirectory.Should().Be("root-at-entry");
        run.IsCompleted.Should().BeFalse();
        release.SetResult();
        (await run).Should().Be(0);
        host.Output.ToString().Should().Be("finished" + Environment.NewLine);
        (await command.RunAsync(["--quiet"], context =>
        {
            context.RootDirectory.Should().Be("root-after-entry");
            context.CancellationToken.IsCancellationRequested.Should().BeFalse();
        })).Should().Be(0);
        host.ActiveSubscriptions.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Full_nested_exception_survives_quiet_mode_and_async_boundary(bool asynchronous)
    {
        using var host = new TestHost();
        var exception = new InvalidOperationException("Outer cause", new ArgumentException("Inner [red]cause[/]"));
        var command = host.Command();
        var code = asynchronous
            ? await command.RunAsync(["--quiet", "--plain"], async _ =>
            {
                await Task.Yield();
                throw exception;
            })
            : await command.RunAsync(["--quiet", "--plain"], (Action<RafterContext>)(_ => { throw exception; }));
        code.Should().Be(1);
        host.Error.ToString().Should().Contain(exception.ToString()).And.Contain("Command body failed")
            .And.Contain("[Failed]").And.NotContain("Print a greeting.");
        host.ActiveSubscriptions.Should().Be(0);
    }

    [Fact]
    public async Task Null_task_and_unrelated_cancellation_are_failures()
    {
        using var host = new TestHost();
        (await host.Command().RunAsync([], (Func<RafterContext, Task>)(_ => null!))).Should().Be(1);
        host.Error.ToString().Should().Contain("returned a null task");
        (await host.Command().RunAsync([], (Action<RafterContext>)(_ =>
            throw new OperationCanceledException()))).Should().Be(1);
        host.Error.ToString().Should().Contain("OperationCanceledException");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_waits_for_body_and_cannot_be_caught_into_success(bool catchCancellation)
    {
        using var host = new TestHost();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = host.Command().RunAsync([], async context =>
        {
            await release.Task;
            if (catchCancellation)
            {
                try
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException)
                {
                    context.WriteLine("cleanup");
                }
            }
            else
            {
                context.CancellationToken.ThrowIfCancellationRequested();
            }
        });
        host.Signal();
        host.Signal();
        run.IsCompleted.Should().BeFalse();
        release.SetResult();
        (await run).Should().Be(130);
        host.Error.ToString().Should().Contain("Cancellation requested.").And.Contain("[Cancelled]")
            .And.NotContain("OperationCanceledException");
        host.ActiveSubscriptions.Should().Be(0);
    }

    [Fact]
    public async Task Execution_failure_wins_over_cancellation_and_state_does_not_leak_to_next_run()
    {
        using var host = new TestHost();
        var command = host.Command();
        (await command.RunAsync(["--quiet"], (Action<RafterContext>)(_ =>
        {
            host.Signal();
            throw new IOException("original failure");
        }))).Should().Be(1);
        host.Error.ToString().Should().Contain("original failure").And.Contain("[Failed]")
            .And.NotContain("Cancellation requested.");
        (await command.RunAsync(["--quiet"], context =>
        {
            context.CancellationToken.IsCancellationRequested.Should().BeFalse();
        })).Should().Be(0);
    }

    [Fact]
    public async Task Cancellation_registration_failures_are_awaited_and_contained()
    {
        using var host = new TestHost();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var run = host.Command().RunAsync(["--quiet"], async context =>
        {
            context.CancellationToken.Register(() =>
            {
                entered.SetResult();
                release.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
                throw new IOException("cancellation callback failure");
            });
            host.Signal();
            await entered.Task;
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            run.IsCompleted.Should().BeFalse();
            host.ActiveSubscriptions.Should().Be(1);
        }
        finally
        {
            release.Set();
        }
        (await run).Should().Be(1);
        host.Error.ToString().Should().Contain("cancellation callback failure");
        host.ActiveSubscriptions.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Both_output_destinations_must_settle_and_host_writers_stay_open(bool stderr)
    {
        using var host = new TestHost();
        var writer = stderr ? host.Error : host.Output;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        writer.FlushTask = release.Task;
        var run = host.Command().RunAsync([], context => context.WriteLine("result"));
        await writer.FlushStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        run.IsCompleted.Should().BeFalse();
        host.ActiveSubscriptions.Should().Be(1);
        release.SetResult();
        (await run).Should().Be(0);
        host.Output.Disposed.Should().BeFalse();
        host.Error.Disposed.Should().BeFalse();
        host.ActiveSubscriptions.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Interrupt_during_final_flush_is_contained_and_changes_the_result_to_cancelled(bool stderr)
    {
        using var host = new TestHost();
        var writer = stderr ? host.Error : host.Output;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        writer.FlushTask = release.Task;
        var run = host.Command().RunAsync([], _ => { });
        await writer.FlushStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        host.Signal();
        run.IsCompleted.Should().BeFalse();
        release.SetResult();
        (await run).Should().Be(130);
        host.ActiveSubscriptions.Should().Be(0);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Write_and_flush_failures_in_either_destination_never_escape(bool stderr, bool flush)
    {
        using var host = new TestHost();
        var writer = stderr ? host.Error : host.Output;
        writer.FailWrites = !flush;
        writer.FlushTask = flush ? Task.FromException(new IOException("flush failed")) : Task.CompletedTask;
        (await host.Command().RunAsync([], context => context.WriteLine("result"))).Should().Be(1);
        host.Output.Flushes.Should().Be(1);
        host.Error.Flushes.Should().Be(1);
        host.Output.Disposed.Should().BeFalse();
        host.Error.Disposed.Should().BeFalse();
    }

    [Fact]
    public async Task Caught_output_failure_still_prevents_success_and_late_writes_are_rejected()
    {
        using var host = new TestHost();
        host.Output.FailWrites = true;
        RafterContext? captured = null;
        (await host.Command().RunAsync([], context =>
        {
            captured = context;
            try
            {
                context.WriteLine("fails");
            }
            catch (IOException)
            {
                // A body cannot hide the invocation's output failure by catching it.
            }
            host.Output.FailWrites = false;
        })).Should().Be(1);
        host.Error.ToString().Should().Contain("write failed");
        Action late = () => captured!.WriteLine("too late");
        late.Should().Throw<InvalidOperationException>();
        host.Output.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Failed_subscription_setup_is_contained_without_executing_body()
    {
        using var host = new TestHost { FailSubscription = true };
        var called = false;
        (await host.Command().RunAsync([], _ => { called = true; })).Should().Be(1);
        called.Should().BeFalse();
        host.Error.ToString().Should().Contain("subscription failed");
    }

    [Fact]
    public async Task Broken_reporting_does_not_replace_a_callback_failure_with_an_escaped_exception()
    {
        using var host = new TestHost();
        host.Error.FailWrites = true;
        host.Error.FlushTask = Task.FromException(new IOException("flush failed too"));
        var called = false;
        (await host.Command().RunAsync(["--quiet"], (Action<RafterContext>)(_ =>
        {
            called = true;
            throw new InvalidOperationException("original callback failure");
        }))).Should().Be(1);
        called.Should().BeTrue();
        host.Output.Flushes.Should().Be(1);
        host.Error.Flushes.Should().Be(1);
        host.ActiveSubscriptions.Should().Be(0);
    }

    [Fact]
    public async Task Cancellation_does_not_hide_an_unrelated_cancelled_task_or_output_failure()
    {
        using var host = new TestHost();
        using var unrelated = new CancellationTokenSource();
        unrelated.Cancel();
        (await host.Command().RunAsync([], context =>
        {
            host.Signal();
            return Task.FromCanceled(unrelated.Token);
        })).Should().Be(1);
        host.Error.ToString().Should().Contain("TaskCanceledException");

        host.Output.FlushTask = Task.FromException(new IOException("flush after cancellation"));
        (await host.Command().RunAsync([], _ => { host.Signal(); })).Should().Be(1);
        host.Error.ToString().Should().Contain("flush after cancellation");
    }

    [Fact]
    public async Task Concurrent_invocations_of_one_command_keep_independent_roots_and_cancellation()
    {
        using var first = new TestHost { Root = "first" };
        using var second = new TestHost { Root = "second" };
        var calls = 0;
        var command = new Command("Concurrent invocation check.", () =>
            Interlocked.Increment(ref calls) == 1 ? first.Capture() : second.Capture());
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRun = command.RunAsync(["--quiet"], async context =>
        {
            await release.Task;
            context.RootDirectory.Should().Be("first");
            context.CancellationToken.ThrowIfCancellationRequested();
        });
        var secondRun = command.RunAsync(["--quiet"], async context =>
        {
            await release.Task;
            context.RootDirectory.Should().Be("second");
            context.CancellationToken.IsCancellationRequested.Should().BeFalse();
            context.WriteLine("second completed");
        });
        first.Signal();
        release.SetResult();
        (await firstRun).Should().Be(130);
        (await secondRun).Should().Be(0);
        second.Error.ToString().Should().BeEmpty();
        second.Output.ToString().Should().Contain("second completed");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Runtime_renderer_preserves_literal_output_and_NO_COLOR_emphasis(bool rich, bool noColor)
    {
        using var writer = new StringWriter();
        var destination = new global::Sotsera.Rafter.Internal.RendererDestination(writer,
            new global::Sotsera.Rafter.Internal.DestinationPolicy(rich, noColor), 48);
        destination.Framework("heading", new Spectre.Console.Style(Spectre.Console.Color.Red,
            decoration: Spectre.Console.Decoration.Bold));
        destination.Literal("  [red]literal[/]\n\npartial", "  | ");
        await destination.CompleteAsync();
        var actual = writer.ToString();
        var stripped = System.Text.RegularExpressions.Regex.Replace(actual, "\\x1b\\[[0-9;]*m", "");
        stripped.Should().Be("heading" + Environment.NewLine + (rich
            ? "  |   [red]literal[/]\n  | \n  | partial"
            : "  [red]literal[/]\n\npartial"));
        if (!rich)
        {
            actual.Should().NotContain("\x1b");
        }
        else
        {
            actual.Should().Contain("\x1b[1");
            if (noColor)
            {
                actual.Should().NotContain("38;").And.NotContain("31m");
            }
        }
    }

    private sealed class TestHost : IDisposable
    {
        private Action? _signal;

        public ProbeWriter Output { get; } = new();
        public ProbeWriter Error { get; } = new();
        public int ActiveSubscriptions { get; private set; }
        public string Root { get; set; } = "invocation-root";
        public int Width { get; set; } = 80;
        public bool FailSubscription { get; init; }

        public Command Command(string description = "Print a greeting.") => new(description, Capture);

        public InvocationHost Capture() => new(Root, Output, Error, true, true, false, Width, Subscribe);

        public void Signal() => _signal?.Invoke();

        public void Dispose()
        {
            Output.Dispose();
            Error.Dispose();
        }

        private IDisposable Subscribe(Action signal)
        {
            if (FailSubscription)
            {
                throw new IOException("subscription failed");
            }
            ActiveSubscriptions++;
            _signal = signal;
            return new Subscription(() =>
            {
                ActiveSubscriptions--;
                _signal = null;
            });
        }
    }

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        public void Dispose() => unsubscribe();
    }

    private sealed class ProbeWriter : StringWriter
    {
        public bool FailWrites { get; set; }
        public bool Disposed { get; private set; }
        public int Flushes { get; private set; }
        public Task FlushTask { get; set; } = Task.CompletedTask;
        public TaskCompletionSource FlushStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void Write(string? value)
        {
            if (FailWrites)
            {
                throw new IOException("write failed");
            }
            base.Write(value);
        }

        public override void Write(char value)
        {
            if (FailWrites)
            {
                throw new IOException("write failed");
            }
            base.Write(value);
        }

        public override void Write(ReadOnlySpan<char> value)
        {
            if (FailWrites)
            {
                throw new IOException("write failed");
            }
            base.Write(value);
        }

        public override Task FlushAsync()
        {
            Flushes++;
            FlushStarted.TrySetResult();
            return FlushTask;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
