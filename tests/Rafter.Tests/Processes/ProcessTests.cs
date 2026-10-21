using System.Diagnostics;
using System.Text.Json;
using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;
using RafterProcessResult = Sotsera.Rafter.ProcessResult;

namespace Rafter.Tests.Processes;

[Collection("Native process handles")]
public sealed class ProcessTests
{
    [Fact]
    public async Task Success_returns_a_result_and_plain_output_preserves_streams_and_partial_lines()
    {
        using var host = new ProcessHost();
        RafterProcessResult? result = null;
        var code = await host.Command.RunAsync(["--plain", "--quiet"], async context =>
        {
            result = await Probe(context, "output").RunAsync();
        });
        code.Should().Be(0);
        result!.Succeeded.Should().BeTrue();
        result.ExitCode.Should().Be(0);
        host.Output.ToString().Should().Be("  first\n\nlast");
        host.Error.ToString().Should().Be("  error\npartial");
    }

    [Theory]
    [InlineData(ProcessFailureAction.Continue, 0)]
    [InlineData(ProcessFailureAction.Fail, 1)]
    public async Task Handled_rejected_exits_return_results_and_keep_diagnostics_in_quiet_mode(
        ProcessFailureAction action, int expected)
    {
        using var host = new ProcessHost();
        var calls = 0;
        var code = await host.Command.RunAsync(["--quiet"], async context =>
        {
            var result = await Probe(context, "exit", "7").OnFailure(value =>
            {
                calls++;
                value.ExitCode.Should().Be(7);
                return action;
            }).RunAsync();
            result.Succeeded.Should().BeFalse();
            result.ExitCode.Should().Be(7);
            context.WriteLine("body continued");
            await Probe(context, "exit", "0").RunAsync();
        });
        code.Should().Be(expected);
        calls.Should().Be(1);
        host.Output.ToString().Should().Contain("body continued");
        host.Error.ToString().Should().Contain("Exit code: 7 | accepted: 0").And.Contain("Action: " + action);
        Occurrences(host.Error.ToString(), "Exit code: 7").Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Throw_unwinds_until_caught_without_recording_an_irreversible_failure(bool caught)
    {
        using var host = new ProcessHost();
        var after = false;
        var cleanup = false;
        var code = await host.Command.RunAsync(["--quiet"], async context =>
        {
            try
            {
                await Probe(context, "exit", "8").RunAsync();
                after = true;
            }
            catch (ProcessFailureException exception) when (caught)
            {
                exception.Result.ExitCode.Should().Be(8);
                exception.Result.Succeeded.Should().BeFalse();
            }
            finally
            {
                cleanup = true;
            }
        });
        code.Should().Be(caught ? 0 : 1);
        cleanup.Should().BeTrue();
        after.Should().BeFalse();
        if (caught)
        {
            host.Error.ToString().Should().BeEmpty();
        }
        else
        {
            host.Error.ToString().Should().Contain("Process failed | dotnet").And.NotContain("ProcessFailureException:");
        }
    }

    [Fact]
    public async Task Fail_is_sticky_and_stops_later_targets_after_current_body_finishes()
    {
        using var host = new ProcessHost();
        var ranLater = false;
        var build = host.Command.Target("build", "Build.").Run(async context =>
        {
            await Probe(context, "exit", "1").OnFailure(_ => ProcessFailureAction.Fail).RunAsync();
            await Probe(context, "exit", "2").OnFailure(_ => ProcessFailureAction.Continue).RunAsync();
            await Probe(context, "exit", "0").RunAsync();
            context.WriteLine("cleanup completed");
        });
        var test = host.Command.Target("test", "Test.").DependsOn(build).Run(_ => { ranLater = true; });
        (await host.Command.RunAsync([], test)).Should().Be(1);
        ranLater.Should().BeFalse();
        host.Output.ToString().Should().Contain("cleanup completed");
        host.Error.ToString().Should().Contain("stopped in build").And.NotContain("[OK] build");
    }

    [Fact]
    public async Task Handler_exception_keeps_original_rejection_and_complete_callback_details()
    {
        using var host = new ProcessHost();
        var code = await host.Command.RunAsync([], async context =>
        {
            await Probe(context, "exit", "6").OnFailure(_ =>
                throw new InvalidOperationException("callback failed", new IOException("inner reason"))).RunAsync();
        });
        code.Should().Be(1);
        host.Error.ToString().Should().Contain("Process failure handler threw | dotnet")
            .And.Contain("Exit code: 6").And.Contain("InvalidOperationException: callback failed")
            .And.Contain("IOException: inner reason").And.Contain(nameof(Handler_exception_keeps_original_rejection_and_complete_callback_details));
        Occurrences(host.Error.ToString(), "Exit code: 6").Should().Be(1);
    }

    [Fact]
    public async Task Missing_executable_can_be_caught_and_never_invokes_exit_handler()
    {
        using var host = new ProcessHost();
        var handled = false;
        (await host.Command.RunAsync(["--quiet"], async context =>
        {
            try
            {
                await context.Process("rafter-missing-" + Guid.NewGuid()).OnFailure(_ =>
                {
                    handled = true;
                    return ProcessFailureAction.Continue;
                }).RunAsync();
            }
            catch (Exception exception)
            {
                exception.Message.Should().Contain("Process launch failed");
            }
        })).Should().Be(0);
        handled.Should().BeFalse();
        host.Error.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Launch_diagnostic_is_compact_and_does_not_expose_argument_values()
    {
        using var host = new ProcessHost();
        (await host.Command.RunAsync(["--verbose"], async context =>
        {
            await context.Process("rafter-missing-tool").Arguments("private-token").RunAsync();
        })).Should().Be(1);
        host.Error.ToString().Should().Contain("Process launch failed | rafter-missing-tool")
            .And.Contain("Working directory:").And.NotContain("private-token").And.NotContain("Win32Exception:");
    }

    [Fact]
    public async Task Both_large_streams_are_drained_without_waiting_for_the_other_to_finish()
    {
        using var host = new ProcessHost();
        (await host.Command.RunAsync(["--plain", "--quiet"], async context =>
        {
            await Probe(context, "large-output").RunAsync();
        })).Should().Be(0);
        host.Output.ToString().Should().HaveLength(100000);
        host.Error.ToString().Should().HaveLength(100000);
    }

    [Fact]
    public async Task Accepted_root_exit_cleans_descendants_before_returning_result()
    {
        using var host = new ProcessHost();
        var ready = TemporaryName();
        try
        {
            (await host.Command.RunAsync([], async context =>
            {
                var result = await Probe(context, "tree", ready).RunAsync();
                result.Succeeded.Should().BeTrue();
                HasExited(int.Parse(File.ReadAllText(ready))).Should().BeTrue();
            })).Should().Be(0);
        }
        finally
        {
            File.Delete(ready);
        }
    }

    [Fact]
    public async Task Cancellation_settles_the_child_before_body_finally_and_returns_130()
    {
        using var host = new ProcessHost();
        var ready = TemporaryName();
        var finallyRan = false;
        try
        {
            var run = host.Command.RunAsync([], async context =>
            {
                try
                {
                    await Probe(context, "hold", ready).RunAsync();
                }
                finally
                {
                    finallyRan = true;
                    HasExited(int.Parse(File.ReadAllText(ready))).Should().BeTrue();
                }
            });
            await WindowsProcessTests.UntilAsync(() => File.Exists(ready));
            host.Signal();
            (await run).Should().Be(130);
            finallyRan.Should().BeTrue();
            host.Error.ToString().Should().Contain("Cancellation requested.").And.NotContain("Process failed");
        }
        finally
        {
            File.Delete(ready);
        }
    }

    [Fact]
    public async Task Unawaited_work_fails_its_body_and_is_cleaned_before_next_target()
    {
        using var host = new ProcessHost();
        var ready = TemporaryName();
        var later = false;
        try
        {
            var first = host.Command.Target("first", "First.").Run(async context =>
            {
                _ = Probe(context, "hold", ready).RunAsync();
                await WindowsProcessTests.UntilAsync(() => File.Exists(ready));
            });
            var second = host.Command.Target("second", "Second.").DependsOn(first).Run(_ => { later = true; });
            (await host.Command.RunAsync([], second)).Should().Be(1);
            HasExited(int.Parse(File.ReadAllText(ready))).Should().BeTrue();
            later.Should().BeFalse();
            host.Error.ToString().Should().Contain("Target 'first' finished with 1 unfinished process operation");
        }
        finally
        {
            File.Delete(ready);
        }
    }

    [Fact]
    public async Task Concurrent_operations_are_legal_when_all_are_awaited()
    {
        using var host = new ProcessHost();
        (await host.Command.RunAsync([], async context =>
        {
            var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Probe(context, "exit", "0").RunAsync()));
            results.Should().OnlyContain(result => result.Succeeded);
        })).Should().Be(0);
        host.Error.ToString().Should().NotContain("unfinished");
    }

    [Fact]
    public async Task Captured_context_cannot_launch_work_from_a_finished_target()
    {
        using var host = new ProcessHost();
        RafterContext? stale = null;
        var first = host.Command.Target("first", "First.").Run(context => { stale = context; });
        var next = host.Command.Target("next", "Next.").DependsOn(first).Run(async _ =>
        {
            await Probe(stale!, "exit", "0").RunAsync();
        });
        (await host.Command.RunAsync([], next)).Should().Be(1);
        host.Error.ToString().Should().Contain("Cannot start a process after Target 'first' has finished");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Invalid_configuration_is_catchable_before_launch(int kind)
    {
        using var host = new ProcessHost();
        (await host.Command.RunAsync(["--quiet"], context =>
        {
            Action configure = kind switch
            {
                0 => () => context.Process(" "),
                1 => () => context.Process("dotnet").Arguments("nul\0token"),
                _ => () => context.Process("dotnet").OnFailure(_ => ProcessFailureAction.Fail)
                    .OnFailure(_ => ProcessFailureAction.Continue)
            };
            configure.Should().Throw<Exception>();
        })).Should().Be(0);
        host.Error.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Second_interrupt_forces_all_concurrent_scopes_without_restarting_grace()
    {
        using var host = new ProcessHost();
        var files = Enumerable.Range(0, 3).Select(_ => TemporaryName()).ToArray();
        try
        {
            var run = host.Command.RunAsync([], async context =>
            {
                await Task.WhenAll(files.Select(file => Probe(context, "hold", file).RunAsync()));
            });
            await WindowsProcessTests.UntilAsync(() => files.All(File.Exists));
            var elapsed = Stopwatch.StartNew();
            host.Signal();
            host.Signal();
            (await run).Should().Be(130);
            elapsed.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(4));
            foreach (var file in files)
            {
                HasExited(int.Parse(File.ReadAllText(file))).Should().BeTrue();
            }
            Occurrences(host.Error.ToString(), "Cancellation requested.").Should().Be(1);
        }
        finally
        {
            foreach (var file in files)
            {
                File.Delete(file);
            }
        }
    }

    [Fact]
    public async Task Concurrent_scopes_share_one_grace_period()
    {
        using var host = new ProcessHost();
        var files = Enumerable.Range(0, 3).Select(_ => TemporaryName()).ToArray();
        try
        {
            var run = host.Command.RunAsync([], async context =>
            {
                await Task.WhenAll(files.Select(file => Probe(context, "hold", file).RunAsync()));
            });
            await WindowsProcessTests.UntilAsync(() => files.All(File.Exists));
            var elapsed = Stopwatch.StartNew();
            host.Signal();
            (await run).Should().Be(130);
            elapsed.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(9));
        }
        finally
        {
            foreach (var file in files)
            {
                File.Delete(file);
            }
        }
    }

    [Fact]
    public async Task A_still_running_failure_callback_is_part_of_the_owned_operation()
    {
        using var host = new ProcessHost();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bodyFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = host.Command.RunAsync([], async context =>
        {
            _ = Probe(context, "exit", "1").OnFailure(_ =>
            {
                entered.SetResult();
                release.Task.GetAwaiter().GetResult();
                return ProcessFailureAction.Continue;
            }).RunAsync();
            await entered.Task;
            bodyFinished.SetResult();
        });
        try
        {
            await bodyFinished.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
            run.IsCompleted.Should().BeFalse();
        }
        finally
        {
            release.TrySetResult();
        }
        (await run).Should().Be(1);
        host.Error.ToString().Should().Contain("unfinished process operation");
    }

    [Fact]
    public async Task Output_failure_stops_child_and_cannot_be_converted_by_exit_handler()
    {
        using var output = new ControlledWriter { Fail = true };
        using var host = new ProcessHost(output);
        var ready = TemporaryName();
        var handlerCalled = false;
        try
        {
            (await host.Command.RunAsync([], async context =>
            {
                try
                {
                    await Probe(context, "output-hold", ready).OnFailure(_ =>
                    {
                        handlerCalled = true;
                        return ProcessFailureAction.Continue;
                    }).RunAsync();
                }
                catch (Exception)
                {
                    // Output failure remains an invocation failure even if script code attempts recovery.
                }
            })).Should().Be(1);
            handlerCalled.Should().BeFalse();
            host.Error.ToString().Should().Contain("Output incomplete");
            File.Exists(ready).Should().BeTrue("the fixture publishes its PID before emitting the failing output");
            HasExited(int.Parse(File.ReadAllText(ready))).Should().BeTrue();
        }
        finally
        {
            File.Delete(ready);
        }
    }

    [Fact]
    public async Task Expired_drain_budget_does_not_abandon_a_blocked_output_writer()
    {
        using var output = new ControlledWriter { Block = true };
        using var host = new ProcessHost(output);
        var run = host.Command.RunAsync([], async context =>
        {
            await Probe(context, "output").RunAsync();
        });
        try
        {
            await output.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(6), TestContext.Current.CancellationToken);
            run.IsCompleted.Should().BeFalse();
        }
        finally
        {
            output.Release.TrySetResult();
        }
        (await run).Should().Be(1);
        host.Error.ToString().Should().Contain("Output incomplete").And.NotContain("Termination unknown");
    }

    [Fact]
    public async Task Child_cannot_break_away_from_its_owned_job()
    {
        using var host = new ProcessHost();
        (await host.Command.RunAsync([], async context =>
        {
            await Probe(context, "breakaway").RunAsync();
        })).Should().Be(0, host.Error.ToString());
    }

    [Fact]
    public async Task Killing_the_owner_closes_its_job_and_stops_the_child()
    {
        var ready = TemporaryName();
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in WindowsProcessTests.ProbeArguments("owned-host", ready))
        {
            start.ArgumentList.Add(argument);
        }
        using var owner = Process.Start(start)!;
        try
        {
            await WindowsProcessTests.UntilAsync(() => File.Exists(ready) || owner.HasExited);
            owner.HasExited.Should().BeFalse("the fixture must reach its live owned-child state");
            using var child = Process.GetProcessById(int.Parse(File.ReadAllText(ready)));
            owner.Kill();
            await owner.WaitForExitAsync(TestContext.Current.CancellationToken);
            await WindowsProcessTests.UntilAsync(() => child.HasExited);
        }
        finally
        {
            if (!owner.HasExited)
            {
                owner.Kill(entireProcessTree: true);
                await owner.WaitForExitAsync(TestContext.Current.CancellationToken);
            }
            File.Delete(ready);
        }
    }

    [Fact]
    public async Task Accepted_codes_allow_nonzero_results_without_calling_failure_handler()
    {
        using var host = new ProcessHost();
        var accepted = new[] { 0, 7, 7 };
        (await host.Command.RunAsync([], async context =>
        {
            var process = Probe(context, "exit", "7").AcceptExitCodes(accepted)
                .OnFailure(_ => throw new InvalidOperationException("Accepted exits must not call this handler."));
            accepted[1] = 2;
            accepted[2] = 2;
            var result = await process.RunAsync();
            result.ExitCode.Should().Be(7);
            result.Succeeded.Should().BeTrue();
        })).Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public async Task Invalid_or_repeated_configuration_is_rejected_before_launch(int kind)
    {
        using var host = new ProcessHost();
        (await host.Command.RunAsync(["--quiet"], context =>
        {
            var process = context.Process("dotnet");
            Action configure = kind switch
            {
                0 => () => process.AcceptExitCodes(null!),
                1 => () => process.AcceptExitCodes(),
                2 => () => process.AcceptExitCodes(0).AcceptExitCodes(0),
                3 => () => process.WorkingDirectory(".").WorkingDirectory("."),
                4 => () => process.Environment("KEY", "value").Environment("KEY", "value"),
                5 => () => process.Environment("KEY", "value").Environment("key", "different"),
                6 => () => process.Environment("BAD=NAME", "value"),
                7 => () => process.Environment("KEY", null!),
                8 => () => process.Environment("KEY", "bad\0value"),
                _ => () => process.WorkingDirectory(" ")
            };
            configure.Should().Throw<Exception>();
        })).Should().Be(0);
        host.Error.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Separate_children_can_override_the_same_inherited_variable_without_changing_the_parent()
    {
        using var host = new ProcessHost();
        var original = Environment.GetEnvironmentVariable("PATH");
        var executable = original!.Split(Path.PathSeparator)
            .Select(path => Path.Combine(path.Trim('"'), "dotnet.exe")).First(File.Exists);
        (await host.Command.RunAsync(["--quiet"], async context =>
        {
            foreach (var value in new[] { "first", "second" })
            {
                await context.Process(executable).Arguments(WindowsProcessTests.ProbeArguments("environment", "PATH"))
                    .Environment("PATH", value).RunAsync();
                context.WriteLine();
            }
        })).Should().Be(0);
        var lines = host.Output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        JsonSerializer.Deserialize<string[]>(lines[0])![1].Should().Be("first");
        JsonSerializer.Deserialize<string[]>(lines[1])![1].Should().Be("second");
        Environment.GetEnvironmentVariable("PATH").Should().Be(original);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Child_directory_uses_captured_root_and_relative_overrides(bool selectDirectory)
    {
        var root = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        using var host = new ProcessHost(root: root);
        var expected = selectDirectory ? Path.GetPathRoot(root)!.TrimEnd(Path.DirectorySeparatorChar) + "\\" : root;
        (await host.Command.RunAsync(["--quiet"], async context =>
        {
            var process = Probe(context, "environment", "RAFTER_UNDEFINED_" + Guid.NewGuid());
            if (selectDirectory)
            {
                process.WorkingDirectory(Path.GetRelativePath(root, expected));
            }
            await process.RunAsync();
        })).Should().Be(0);
        JsonSerializer.Deserialize<string[]>(host.Output.ToString())![0].Should().Be(expected);
    }

    [Fact]
    public async Task Headless_owner_forces_descendants_without_waiting_for_unavailable_console_signals()
    {
        var ready = TemporaryName();
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in WindowsProcessTests.ProbeArguments("headless-host", ready))
        {
            start.ArgumentList.Add(argument);
        }
        using var owner = Process.Start(start)!;
        var stdout = owner.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stderr = owner.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        try
        {
            await WindowsProcessTests.UntilAsync(() => File.Exists(ready) || owner.HasExited);
            var elapsed = Stopwatch.StartNew();
            await owner.WaitForExitAsync(TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
            owner.ExitCode.Should().Be(0, await stderr);
            (await stdout).Should().Be("parent finished");
            elapsed.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(4));
            HasExited(int.Parse(File.ReadAllText(ready))).Should().BeTrue();
        }
        finally
        {
            if (!owner.HasExited)
            {
                owner.Kill(entireProcessTree: true);
                await owner.WaitForExitAsync(TestContext.Current.CancellationToken);
            }
            await Task.WhenAll(stdout, stderr);
            File.Delete(ready);
        }
    }

    [Fact]
    public async Task Failure_diagnostic_starts_a_separate_line_after_partial_child_stderr()
    {
        using var host = new ProcessHost();
        (await host.Command.RunAsync(["--quiet"], async context =>
        {
            await Probe(context, "output", "1").OnFailure(_ => ProcessFailureAction.Continue).RunAsync();
        })).Should().Be(0);
        host.Error.ToString().Should().Contain("partial" + Environment.NewLine + "  Process failure handled");
    }

    private static ProcessCommand Probe(RafterContext context, string mode, params string[] args)
        => context.Process("dotnet").Arguments(WindowsProcessTests.ProbeArguments(mode, args));

    private static string TemporaryName() => Path.Combine(Path.GetTempPath(), "rafter-probe-" + Guid.NewGuid().ToString("N"));

    private static int Occurrences(string text, string value) => text.Split(value).Length - 1;

    private static bool HasExited(int id)
    {
        try
        {
            using var process = Process.GetProcessById(id);
            return process.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    private sealed class ProcessHost : IDisposable
    {
        private Action? _signal;
        public StringWriter Output { get; }
        public StringWriter Error { get; } = new();
        public Command Command { get; }

        public ProcessHost(StringWriter? output = null, string? root = null)
        {
            Output = output ?? new StringWriter();
            Command = new("Process workflow.", () => new InvocationHost(root ?? Directory.GetCurrentDirectory(), Output, Error,
                true, true, false, 100, signal =>
                {
                    _signal = signal;
                    return new Subscription(() => _signal = null);
                }));
        }

        public void Signal() => _signal!();

        public void Dispose()
        {
            TestContext.Current.TestOutputHelper?.WriteLine("Rafter stdout:\n{0}\nRafter stderr:\n{1}", Output, Error);
            Output.Dispose();
            Error.Dispose();
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }

    private sealed class ControlledWriter : StringWriter
    {
        public bool Fail { get; init; }
        public bool Block { get; init; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void Write(string? value)
        {
            if (Fail)
            {
                throw new IOException("Writer rejected output.");
            }
            if (Block)
            {
                Entered.TrySetResult();
                Release.Task.GetAwaiter().GetResult();
            }
            base.Write(value);
        }
    }
}
