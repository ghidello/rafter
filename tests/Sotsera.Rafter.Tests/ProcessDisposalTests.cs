using System.Diagnostics;
using System.Text;

namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class ProcessDisposalTests
{
    [Theory]
    [InlineData("success", false)]
    [InlineData("success", true)]
    [InlineData("start-false", false)]
    [InlineData("start-false", true)]
    [InlineData("start-throw", false)]
    [InlineData("start-throw", true)]
    [InlineData("invalid-exit", false)]
    [InlineData("invalid-exit", true)]
    [InlineData("invalid-utf8", false)]
    [InlineData("invalid-utf8", true)]
    [InlineData("cancel", false)]
    [InlineData("cancel", true)]
    [InlineData("timeout", false)]
    [InlineData("timeout", true)]
    public async Task DisposalFailurePreservesTheSelectedOutcome(string outcome, bool capture)
    {
        using CancellationTokenSource cancellation = new();
        DisposalProcess process = new(outcome, cancellation);
        IProcessAdapterFactory original = ProcessRuntime.AdapterFactory;
        ProcessRuntime.AdapterFactory = new Factory(process);
        try
        {
            Command command = PhaseFiveTestSupport.CreateCommand();
            Exception? failure = null;
            Target entry = command.Target("work").Description("Observe disposal.").Run(async context =>
            {
                ProcessBuilder builder = context.Process("synthetic");
                if (outcome is "timeout")
                {
                    builder = builder.Timeout(TimeSpan.FromMilliseconds(20));
                }
                try
                {
                    if (capture)
                    {
                        _ = await builder.Capture().ConfigureAwait(false);
                    }
                    else
                    {
                        _ = await builder.Run().ConfigureAwait(false);
                    }
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            _ = await command.RunAsync(entry, [], cancellation.Token)
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            AssertFailure(outcome, capture, failure!, process);
            process.DisposeCount.Should().Be(1);
            ProcessOperationReaper.Count.Should().Be(0);
        }
        finally
        {
            ProcessRuntime.AdapterFactory = original;
        }
    }

    private static void AssertFailure(string outcome, bool capture, Exception failure, DisposalProcess process)
    {
        Type expected = outcome switch
        {
            "success" => typeof(ProcessException),
            "start-false" or "start-throw" => typeof(ProcessStartException),
            "invalid-exit" => typeof(ProcessExitException),
            "invalid-utf8" => typeof(ProcessOutputException),
            "cancel" => typeof(OperationCanceledException),
            "timeout" => typeof(ProcessTimeoutException),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };
        failure.Should().BeOfType(expected);
        failure.Message.Should().NotContain("disposable-secret");
        if (outcome is "start-throw")
        {
            failure.InnerException.Should().BeOfType<AggregateException>().Which.InnerExceptions
                .Should().Equal(process.StartFailure, process.DisposalFailure);
        }
        else
        {
            failure.InnerException.Should().BeSameAs(process.DisposalFailure);
        }
        if (failure is ProcessExitException exit)
        {
            exit.ExitCode.Should().Be(42);
            if (capture)
            {
                exit.Capture!.StandardOutput.Should().Be("raw output");
            }
            else
            {
                exit.Capture.Should().BeNull();
            }
        }
        if (failure is OperationCanceledException cancelled)
        {
            cancelled.CancellationToken.IsCancellationRequested.Should().BeTrue();
        }
    }

    private sealed class Factory(DisposalProcess process) : IProcessAdapterFactory
    {
        public IProcessAdapter Create(ProcessStartInfo startInfo) => process;
    }

    private sealed class DisposalProcess(string outcome, CancellationTokenSource cancellation) : IProcessAdapter
    {
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly MemoryStream _stdout = new(outcome is "invalid-utf8" ? [0xff] : Encoding.UTF8.GetBytes("raw output"));

        public Stream StandardOutput => _stdout;

        public Stream StandardError => Stream.Null;

        public int Id => 1;

        public bool HasExited => _exit.Task.IsCompleted;

        public int ExitCode => outcome is "invalid-exit" ? 42 : 0;

        internal IOException StartFailure { get; } = new("start disposable-secret");

        internal IOException DisposalFailure { get; } = new("dispose disposable-secret");

        internal int DisposeCount { get; private set; }

        public bool Start()
        {
            if (outcome is "start-throw")
            {
                throw StartFailure;
            }
            if (outcome is not ("cancel" or "timeout"))
            {
                _exit.TrySetResult();
            }
            return outcome is not "start-false";
        }

        public Task WaitForExitAsync()
        {
            if (outcome is "cancel")
            {
                cancellation.Cancel();
            }
            return _exit.Task;
        }

        public void KillTree() => _exit.TrySetResult();

        public void CloseOutput() => _stdout.Dispose();

        public void Dispose()
        {
            DisposeCount++;
            _stdout.Dispose();
            throw DisposalFailure;
        }
    }
}
