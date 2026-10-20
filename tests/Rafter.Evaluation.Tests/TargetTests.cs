using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.Evaluation.Tests;

public sealed class TargetTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Prerequisites_override_entry_order_and_repeated_references_execute_once(int selection)
    {
        using var host = new TargetHost();
        var order = new List<string>();
        var build = host.Command.Target("build", "Build.").Run(_ => { order.Add("build"); });
        var test = host.Command.Target("test", "Test.").DependsOn(build, build)
            .DependsOn(build).Run(_ => { order.Add("test"); });
        var code = selection switch
        {
            0 => await host.Command.RunAsync([], test),
            1 => await host.Command.RunAsync([], build, test),
            _ => await host.Command.RunAsync([], test, build, test)
        };
        code.Should().Be(0);
        order.Should().Equal("build", "test");
        host.Error.ToString().Should().Contain("2 target bodies completed");
        if (selection == 2)
        {
            host.Error.ToString().Should().Contain("> test, build").And.Contain("[OK] test, build");
        }
    }

    [Fact]
    public async Task Diamond_dependencies_nested_groups_and_unrelated_entries_keep_depth_first_order()
    {
        using var host = new TargetHost();
        var order = new List<string>();
        Target Body(string name) => host.Command.Target(name, name).Run(_ => { order.Add(name); });
        var shared = Body("shared");
        var left = Body("left").DependsOn(shared);
        var right = Body("right").DependsOn(shared);
        var nested = host.Command.Target("nested", "Nested group.").DependsOn(left, right);
        var group = host.Command.Target("group", "Group.").DependsOn(nested, shared);
        var last = Body("last");
        Body("unused");
        (await host.Command.RunAsync([], group, last)).Should().Be(0);
        order.Should().Equal("shared", "left", "right", "last");
        host.Error.ToString().Should().Contain("4 target bodies completed").And.NotContain("[nested]")
            .And.NotContain("[group]").And.NotContain("unused");
    }

    [Fact]
    public async Task Deep_graphs_are_validated_and_planned_without_recursive_stack_growth()
    {
        using var host = new TargetHost();
        var calls = 0;
        var entry = host.Command.Target("leaf", "Leaf.").Run(_ => { calls++; });
        for (var index = 0; index < 12000; index++)
        {
            entry = host.Command.Target("group-" + index, "Group.").DependsOn(entry);
        }
        (await host.Command.RunAsync(["--quiet"], entry)).Should().Be(0);
        calls.Should().Be(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Build")]
    [InlineData("-build")]
    [InlineData("--build")]
    [InlineData("1build")]
    [InlineData("build-")]
    [InlineData("build--all")]
    [InlineData("build_all")]
    [InlineData("bùild")]
    public async Task Invalid_target_names_are_rejected_before_help(string? name)
    {
        using var host = new TargetHost();
        var target = host.Command.Target(name!, "Description.").Run(_ => { });
        (await host.Command.RunAsync(["--help"], target)).Should().Be(2);
        host.Error.ToString().Should().Contain("Target names must");
        host.Output.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public async Task Target_description_is_validated_before_help(string? description)
    {
        using var host = new TargetHost();
        var target = host.Command.Target("target", description!).Run(_ => { });
        (await host.Command.RunAsync(["--help"], target)).Should().Be(2);
        host.Error.ToString().Should().Contain("requires a nonblank description");
    }

    [Fact]
    public async Task Targets_and_options_have_separate_namespaces_and_control_names_are_valid_targets()
    {
        using var host = new TargetHost();
        var value = host.Command.Option<int>("build", "Build value.").Default(1);
        var build = host.Command.Target("build", "Build.").Run(context =>
        {
            context.GetValue(value).Should().Be(1);
        });
        var help = host.Command.Target("help", "Help group.").DependsOn(build);
        var quiet = host.Command.Target("quiet", "Quiet group.").DependsOn(help);
        var verbose = host.Command.Target("verbose", "Verbose group.").DependsOn(quiet);
        var plain = host.Command.Target("plain", "Plain group.").DependsOn(verbose);
        (await host.Command.RunAsync([], plain)).Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Empty_unused_targets_are_rejected_even_for_body_invocations_and_help(bool help)
    {
        using var host = new TargetHost();
        host.Command.Target("empty", "Empty.").DependsOn();
        var calls = 0;
        host.Command.Validate("Do not validate inputs.", _ =>
        {
            calls++;
            return true;
        });
        (await host.Command.RunAsync(help ? ["--help"] : [], _ => { calls++; })).Should().Be(2);
        calls.Should().Be(0);
        host.Error.ToString().Should().Contain("requires a body or dependencies");
        host.Output.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Null_and_repeated_bodies_are_invalid_even_with_dependencies(bool asynchronous)
    {
        using var host = new TargetHost();
        var valid = host.Command.Target("valid", "Valid.").Run(_ => { });
        var repeated = host.Command.Target("repeated", "Repeated.").Run(_ => { });
        var missing = host.Command.Target("missing", "Missing.").DependsOn(valid);
        if (asynchronous)
        {
            repeated.Run(_ => Task.CompletedTask);
            missing.Run((Func<RafterContext, Task>)null!);
        }
        else
        {
            repeated.Run((Action<RafterContext>)(_ => { }));
            missing.Run((Action<RafterContext>)null!);
        }
        (await host.Command.RunAsync(["--help"], valid)).Should().Be(2);
        host.Error.ToString().Should().Contain("'repeated' may declare its body only once")
            .And.Contain("'missing' has a null body");
    }

    [Fact]
    public async Task Duplicate_target_names_are_invalid_even_if_one_is_unused()
    {
        using var host = new TargetHost();
        var entry = host.Command.Target("build", "First.").Run(_ => { });
        host.Command.Target("build", "Second.").Run(_ => { });
        (await host.Command.RunAsync(["--help"], entry)).Should().Be(2);
        host.Error.ToString().Should().Contain("'build' is declared more than once");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Null_arrays_entries_and_foreign_targets_are_invalid(int scenario)
    {
        using var host = new TargetHost();
        var entry = host.Command.Target("entry", "Entry.").Run(_ => { });
        var foreign = Sotsera.Rafter.Rafter.Command("Other.").Target("foreign", "Foreign.").Run(_ => { });
        int code;
        switch (scenario)
        {
            case 0:
                code = await host.Command.RunAsync(["--help"], (Target)null!);
                break;
            case 1:
                code = await host.Command.RunAsync(["--help"], entry, (Target[])null!);
                break;
            case 2:
                code = await host.Command.RunAsync(["--help"], entry, [null!, foreign]);
                break;
            case 3:
                entry.DependsOn((Target[])null!);
                code = await host.Command.RunAsync(["--help"], entry);
                break;
            case 4:
                entry.DependsOn([null!, foreign]);
                code = await host.Command.RunAsync(["--help"], entry);
                break;
            default:
                code = await host.Command.RunAsync(["--help"], foreign);
                break;
        }
        code.Should().Be(2);
        host.Error.ToString().Should().Contain("Invalid command");
        host.Output.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cycles_include_the_path_even_when_unreachable_from_the_entry(bool self)
    {
        using var host = new TargetHost();
        var entry = host.Command.Target("entry", "Entry.").Run(_ => { });
        var build = host.Command.Target("build", "Build.");
        if (self)
        {
            build.DependsOn(build);
        }
        else
        {
            var test = host.Command.Target("test", "Test.").DependsOn(build);
            build.DependsOn(test);
        }
        (await host.Command.RunAsync(["--help"], entry)).Should().Be(2);
        host.Error.ToString().Should().Contain(self ? "build -> build" : "build -> test -> build");
        (await host.Command.RunAsync([], _ => { throw new Exception("must not run"); })).Should().Be(2);
    }

    [Fact]
    public async Task Valid_unused_targets_do_not_run_in_the_body_overload()
    {
        using var host = new TargetHost();
        host.Command.Target("unused", "Unused.").Run((Action<RafterContext>)(_ => throw new Exception("unused")));
        var calls = 0;
        (await host.Command.RunAsync([], _ => { calls++; })).Should().Be(0);
        calls.Should().Be(1);
        (await host.Command.RunAsync(["--help"], _ => { calls++; })).Should().Be(0);
        host.Output.ToString().Should().Contain("Run the supplied command body.").And.NotContain("unused");
    }

    [Fact]
    public async Task Async_prerequisite_is_awaited_before_its_dependant()
    {
        using var host = new TargetHost();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<string>();
        var first = host.Command.Target("first", "First.").Run(async _ =>
        {
            order.Add("started");
            await release.Task;
            order.Add("finished");
        });
        var second = host.Command.Target("second", "Second.").DependsOn(first).Run(_ => { order.Add("second"); });
        var run = host.Command.RunAsync([], second);
        try
        {
            run.IsCompleted.Should().BeFalse();
            order.Should().Equal("started");
        }
        finally
        {
            release.TrySetResult();
        }
        (await run).Should().Be(0);
        order.Should().Equal("started", "finished", "second");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Failures_and_null_tasks_stop_all_later_scheduling(int failure)
    {
        using var host = new TargetHost();
        var later = false;
        var first = host.Command.Target("first", "First.");
        switch (failure)
        {
            case 0:
                first.Run((Action<RafterContext>)(_ => throw new InvalidOperationException("sync failure")));
                break;
            case 1:
                first.Run(async _ =>
                {
                    await Task.Yield();
                    throw new InvalidOperationException("async failure");
                });
                break;
            case 2:
                first.Run((Func<RafterContext, Task>)(_ => null!));
                break;
            default:
                first.Run((Action<RafterContext>)(_ => throw new OperationCanceledException("unrelated")));
                break;
        }
        var second = host.Command.Target("second", "Second.").DependsOn(first).Run(_ => { later = true; });
        var other = host.Command.Target("other", "Other.").Run(_ => { later = true; });
        (await host.Command.RunAsync(["--quiet"], second, other)).Should().Be(1);
        later.Should().BeFalse();
        host.Error.ToString().Should().Contain("Target 'first' failed").And.Contain("stopped in first")
            .And.NotContain("[OK]");
        host.ActiveSubscriptions.Should().Be(0);
    }

    [Fact]
    public async Task Caught_output_failure_still_prevents_later_targets()
    {
        using var host = new TargetHost();
        var first = host.Command.Target("first", "First.").Run(context =>
        {
            host.Output.FailWrites = true;
            try
            {
                context.WriteLine("will fail");
            }
            catch (IOException)
            {
                host.Output.FailWrites = false;
            }
        });
        var later = false;
        var second = host.Command.Target("second", "Second.").DependsOn(first).Run(_ => { later = true; });
        (await host.Command.RunAsync([], second)).Should().Be(1);
        later.Should().BeFalse();
        host.Error.ToString().Should().Contain("stopped in first").And.Contain("IOException");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_awaits_current_body_cleanup_and_stops_remaining_targets(bool throwFailure)
    {
        using var host = new TargetHost();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var later = false;
        var first = host.Command.Target("first", "First.").Run(async context =>
        {
            entered.SetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, context.CancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Deliberately catch cancellation: it must still stop scheduling after cleanup settles.
            }
            finally
            {
                cleanup.SetResult();
                await release.Task;
                context.WriteLine("cleanup settled");
            }
            if (throwFailure)
            {
                throw new InvalidOperationException("failure wins");
            }
        });
        var second = host.Command.Target("second", "Second.").DependsOn(first).Run(_ => { later = true; });
        var run = host.Command.RunAsync([], second);
        try
        {
            await entered.Task;
            host.Signal();
            await cleanup.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            run.IsCompleted.Should().BeFalse();
            later.Should().BeFalse();
            host.ActiveSubscriptions.Should().Be(1);
        }
        finally
        {
            release.TrySetResult();
        }
        (await run).Should().Be(throwFailure ? 1 : 130);
        later.Should().BeFalse();
        host.Output.ToString().Should().Contain("cleanup settled");
        host.Error.ToString().Should().Contain("stopped in first").And.NotContain("[OK]");
        host.ActiveSubscriptions.Should().Be(0);
    }

    [Fact]
    public async Task Graph_bodies_and_selections_are_snapshotted_before_input_callbacks()
    {
        using var host = new TargetHost();
        var order = new List<string>();
        var first = host.Command.Target("first", "First.").Run(_ => { order.Add("first"); });
        var later = host.Command.Target("later", "Later.").Run(_ => { order.Add("later"); });
        Target[] additional = [later];
        host.Command.Validate("Mutate declarations for a later run.", _ =>
        {
            first.DependsOn(later);
            later.Run(_ => { order.Add("replacement"); });
            additional[0] = first;
            return true;
        });
        (await host.Command.RunAsync([], first, additional)).Should().Be(0);
        order.Should().Equal("first", "later");
        (await host.Command.RunAsync([], first)).Should().Be(2);
        host.Error.ToString().Should().Contain("may declare its body only once");
    }

    [Fact]
    public async Task Target_deduplication_and_input_values_are_invocation_local()
    {
        using var host = new TargetHost();
        var value = host.Command.Option<int>("value", "Value.").Required();
        var values = new List<int>();
        var calls = 0;
        value.Validate("Count once per invocation.", _ =>
        {
            calls++;
            return true;
        });
        var target = host.Command.Target("record", "Record.").Run(context =>
        {
            values.Add(context.GetValue(value));
            context.GetValue(value).Should().Be(values[^1]);
        });
        (await host.Command.RunAsync(["--value=1"], target, target)).Should().Be(0);
        (await host.Command.RunAsync(["--value=2"], target)).Should().Be(0);
        values.Should().Equal(1, 2);
        calls.Should().Be(2);
    }

    [Fact]
    public async Task Overlapping_target_runs_have_independent_values_and_completion_state()
    {
        using var host = new TargetHost();
        var value = host.Command.Option<int>("value", "Value.").Required();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var target = host.Command.Target("record", "Record.").Run(async context =>
        {
            var initial = context.GetValue(value);
            if (initial == 1)
            {
                await release.Task;
            }
            context.GetValue(value).Should().Be(initial);
            context.WriteLine(initial.ToString());
        });
        var first = host.Command.RunAsync(["--value=1", "--quiet"], target);
        try
        {
            (await host.Command.RunAsync(["--value=2", "--quiet"], target, target)).Should().Be(0);
            first.IsCompleted.Should().BeFalse();
        }
        finally
        {
            release.TrySetResult();
            (await first).Should().Be(0);
        }
        host.Output.ToString().Should().Be("2" + Environment.NewLine + "1" + Environment.NewLine);
    }

    [Theory]
    [InlineData(48)]
    [InlineData(100)]
    public async Task Help_describes_selected_groups_and_bodies_without_callbacks(int width)
    {
        using var host = new TargetHost(width);
        host.Command.Option<string>("name", "Name.").Required().Validate("Never called.", _ => throw new Exception());
        host.Command.Validate("Also never called.", _ => throw new Exception());
        var build = host.Command.Target("build", "Literal [red]build[/] 界面.")
            .Run((Action<RafterContext>)(_ => throw new Exception()));
        var test = host.Command.Target("test", "Test.").DependsOn(build)
            .Run((Action<RafterContext>)(_ => throw new Exception()));
        var group = host.Command.Target("verify", "Group.").DependsOn(build, test);
        host.Command.Target("unused", "Unused.").Run(_ => { });
        (await host.Command.RunAsync(["--help", "--quiet"], group, test)).Should().Be(0);
        var help = host.Output.ToString();
        help.Should().Contain("verify, test").And.Contain("> verify <-- (build, test)")
            .And.Contain("Group; no body.").And.Contain("[test] <-- (build)")
            .And.Contain("Literal [red]build[/] 界面.").And.Contain("Body order: build, test.")
            .And.NotContain("unused").And.NotContain("\x1b");
        host.Error.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData("--quiet")]
    [InlineData("--plain")]
    [InlineData("--verbose")]
    public async Task Lifecycle_output_uses_flat_sections_correct_counts_and_original_result_text(string mode)
    {
        using var host = new TargetHost();
        var build = host.Command.Target("build", "Build description.").Run(context =>
        {
            context.WriteLine("  [red]result[/]\n界面");
        });
        var test = host.Command.Target("test", "Test description.").DependsOn(build).Run(_ => { });
        var group = host.Command.Target("verify", "Group.").DependsOn(build, test);
        (await host.Command.RunAsync([mode], group)).Should().Be(0);
        host.Output.ToString().Should().Be("  [red]result[/]\n界面" + Environment.NewLine);
        var output = host.Error.ToString();
        if (mode == "--quiet")
        {
            output.Should().BeEmpty();
        }
        else
        {
            output.Should().Contain("> verify <-- (build, test)").And.Contain("[build]" + Environment.NewLine
                + "Build description." + Environment.NewLine + Environment.NewLine)
                .And.Contain("[test] <-- (build)").And.Contain("[OK] build |")
                .And.Contain("[OK] test |").And.Contain("[OK] verify | 2 target bodies completed |")
                .And.NotContain("[verify]").And.NotContain("\x1b")
                .And.NotContain(string.Concat(Enumerable.Repeat(Environment.NewLine, 3)));
        }
    }

    [Fact]
    public async Task Invalid_inputs_and_failed_rules_prevent_any_target_execution()
    {
        using var host = new TargetHost();
        host.Command.Option<int>("count", "Count.").Required().Validate("Positive.", value => value > 0);
        var calls = 0;
        var target = host.Command.Target("target", "Target.").Run(_ => { calls++; });
        (await host.Command.RunAsync(["--count=invalid", "--help"], target)).Should().Be(2);
        (await host.Command.RunAsync(["--count=-1"], target)).Should().Be(2);
        calls.Should().Be(0);
        host.Error.ToString().Should().NotContain("[target]").And.NotContain("target bodies completed");
    }

    private sealed class TargetHost : IDisposable
    {
        private readonly List<Action> _signals = [];
        public OutputWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        public Command Command { get; }
        public int ActiveSubscriptions => _signals.Count;

        public TargetHost(int width = 100)
        {
            Command = new Command("Target workflow.", () => new InvocationHost("root", Output, Error,
                true, true, false, width, signal =>
                {
                    _signals.Add(signal);
                    return new Subscription(() => _signals.Remove(signal));
                }));
        }

        public void Signal()
        {
            foreach (var signal in _signals.ToArray())
            {
                signal();
            }
        }

        public void Dispose()
        {
            Output.Dispose();
            Error.Dispose();
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }

    private sealed class OutputWriter : StringWriter
    {
        public bool FailWrites { get; set; }
        public override void Write(string? value)
        {
            if (FailWrites)
            {
                throw new IOException("output failed");
            }
            base.Write(value);
        }
    }
}
