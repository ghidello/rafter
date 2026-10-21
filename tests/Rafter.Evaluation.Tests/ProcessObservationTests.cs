using System.Runtime.Versioning;
using System.Diagnostics;
using AwesomeAssertions;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.Evaluation.Tests;

[SupportedOSPlatform("windows")]
[Collection("Native process handles")]
public sealed class ProcessObservationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Monitoring_failures_preserve_the_difference_between_termination_and_exit_code(bool loseJobQuery)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var root = Directory.GetCurrentDirectory();
        var host = new InvocationHost(root, stdout, stderr, true, true, false, 100, _ => new Subscription());
        var output = new InvocationOutput(host, new(false, false, false, true, []));
        using var cancellation = new InvocationCancellation(output);
        var invocation = new ProcessInvocation(cancellation, output);
        var body = new BodyProcesses(invocation, "Observed body");
        var arguments = WindowsProcessTests.ProbeArguments("exit", "0");
        var settings = new ProcessSettings("dotnet", arguments, root, new Dictionary<string, string>(), [0],
            _ => throw new InvalidOperationException("Observation failures must not reach the exit handler."));
        var native = WindowsProcess.Start("dotnet", arguments, root, settings.Environment);
        using var observed = new FailingObservation(native, loseJobQuery);
        await WindowsProcessTests.UntilAsync(() => native.ExitCode.HasValue);

        Func<Task> complete = async () => { await ProcessOperation.CompleteAsync(settings, body, observed); };
        if (loseJobQuery)
        {
            var failure = (await complete.Should().ThrowAsync<ProcessCompletionException>()).Which;
            failure.Message.Should().Contain("Termination unknown").And.NotContain("Output incomplete");
            failure.ExitCode.Should().Be(0);
            invocation.Failed.Should().BeTrue();
            invocation.Diagnostics.Should().ContainSingle().Which.Should().BeSameAs(failure);
        }
        else
        {
            var failure = (await complete.Should().ThrowAsync<InvalidOperationException>()).Which;
            failure.Message.Should().Contain("Process exit code unavailable").And.NotContain("Termination unknown");
            invocation.Failed.Should().BeFalse("termination and output were both confirmed, so this exception is catchable");
        }
        observed.Disposed.Should().BeTrue();
        await output.CompleteOutputAsync();
        await output.CompleteErrorAsync();
    }

    [Fact]
    public async Task Failed_force_request_cancels_pending_reads_and_keeps_kill_on_close_as_final_cleanup()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var root = Directory.GetCurrentDirectory();
        var ready = Path.Combine(Path.GetTempPath(), "rafter-force-fault-" + Guid.NewGuid().ToString("N"));
        var host = new InvocationHost(root, stdout, stderr, true, true, false, 100, _ => new Subscription());
        var output = new InvocationOutput(host, new(false, false, false, true, []));
        using var cancellation = new InvocationCancellation(output);
        var invocation = new ProcessInvocation(cancellation, output);
        var body = new BodyProcesses(invocation, "Observed body");
        var arguments = WindowsProcessTests.ProbeArguments("tree", ready);
        var settings = new ProcessSettings("dotnet", arguments, root, new Dictionary<string, string>(), [0], null);
        var native = WindowsProcess.Start("dotnet", arguments, root, settings.Environment);
        using var observed = new FailedForce(native);
        try
        {
            await WindowsProcessTests.UntilAsync(() => File.Exists(ready) && new FileInfo(ready).Length > 0);
            using var descendant = Process.GetProcessById(int.Parse(File.ReadAllText(ready)));
            Func<Task> complete = async () => { await ProcessOperation.CompleteAsync(settings, body, observed); };
            var failure = (await complete.Should().ThrowAsync<ProcessCompletionException>()).Which;
            failure.Message.Should().Contain("Termination unknown");
            failure.ExitCode.Should().Be(0);
            invocation.Failed.Should().BeTrue();
            await WindowsProcessTests.UntilAsync(() => descendant.HasExited);
        }
        finally
        {
            File.Delete(ready);
            await output.CompleteOutputAsync();
            await output.CompleteErrorAsync();
        }
    }

    private sealed class FailedForce(WindowsProcess native) : IOwnedProcess
    {
        public StreamReader Output => native.Output;
        public StreamReader Error => native.Error;
        public int? ExitCode => native.ExitCode;
        public bool IsEmpty => native.IsEmpty;
        public bool RequestGracefulStop() => false;
        public void ForceStop() => throw new IOException("Force request failed.");
        public void Dispose() => native.Dispose();
    }

    private sealed class FailingObservation(WindowsProcess native, bool loseJobQuery) : IOwnedProcess
    {
        public StreamReader Output => native.Output;
        public StreamReader Error => native.Error;
        public int? ExitCode => loseJobQuery ? native.ExitCode : throw new IOException("Exit observation failed.");
        public bool IsEmpty => loseJobQuery ? throw new IOException("Job observation failed.") : native.IsEmpty;
        public bool Disposed { get; private set; }

        public bool RequestGracefulStop() => native.RequestGracefulStop();
        public void ForceStop() => native.ForceStop();

        public void Dispose()
        {
            if (!Disposed)
            {
                native.Dispose();
                Disposed = true;
            }
        }
    }

    private sealed class Subscription : IDisposable
    {
        public void Dispose() { }
    }
}
