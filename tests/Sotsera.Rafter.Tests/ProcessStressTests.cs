using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class ProcessStressTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(24)]
    public async Task ConcurrentCapturesSettleBothPipesAndDisposeEveryChild(int count)
    {
        IProcessAdapterFactory original = ProcessRuntime.AdapterFactory;
        TrackingFactory tracking = new(original);
        ProcessRuntime.AdapterFactory = tracking;
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            Command command = PhaseFiveTestSupport.CreateCommand();
            ProcessCapture[]? captures = null;
            Target entry = command.Target("stress").Description("Drain concurrent processes.").Run(async context =>
            {
                string executable = OperatingSystem.IsWindows()
                    ? "Sotsera.Rafter.ProcessFixture.exe"
                    : "Sotsera.Rafter.ProcessFixture";
                string fixture = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..",
                    "Sotsera.Rafter.ProcessFixture", "release", executable));
                ProcessBuilder builder = context.Process(fixture).Argument("emit")
                    .Option("--stdout", new string('o', 4096)).Option("--stderr", new string('e', 4096))
                    .Option("--repeat", "64").Option("--chunk-bytes", "997");
                captures = await Task.WhenAll(Enumerable.Range(0, count).Select(_ => builder.Capture()))
                    .ConfigureAwait(false);
            });

            (await command.RunAsync(entry, [], deadline.Token)).Should().Be(0);

            captures.Should().HaveCount(count);
            foreach (ProcessCapture capture in captures!)
            {
                capture.StandardOutput.Should().Be(new string('o', 256 * 1024));
                capture.StandardError.Should().Be(new string('e', 256 * 1024));
            }
            tracking.Processes.Should().HaveCount(count);
            foreach (TrackedProcess process in tracking.Processes)
            {
                process.DisposeCount.Should().Be(1);
                process.Probe!.HasExited.Should().BeTrue("an independent process handle verifies termination");
            }
            ProcessOperationReaper.Count.Should().Be(0);
        }
        finally
        {
            try
            {
                await Task.WhenAll(tracking.Processes.Select(process => process.CleanupAsync()));
            }
            finally
            {
                ProcessRuntime.AdapterFactory = original;
            }
        }
    }

    private sealed class TrackingFactory(IProcessAdapterFactory inner) : IProcessAdapterFactory
    {
        private readonly ConcurrentBag<TrackedProcess> _processes = [];

        internal ImmutableArray<TrackedProcess> Processes => [.. _processes];

        public IProcessAdapter Create(ProcessStartInfo startInfo)
        {
            TrackedProcess process = new(inner.Create(startInfo));
            _processes.Add(process);
            return process;
        }
    }

    private sealed class TrackedProcess(IProcessAdapter inner) : IProcessAdapter
    {
        public Stream StandardOutput => inner.StandardOutput;

        public Stream StandardError => inner.StandardError;

        public int Id => inner.Id;

        public bool HasExited => inner.HasExited;

        public int ExitCode => inner.ExitCode;

        internal Process? Probe { get; private set; }

        internal int DisposeCount { get; private set; }

        public bool Start()
        {
            bool started = inner.Start();
            if (started)
            {
                Probe = Process.GetProcessById(inner.Id);
                _ = Probe.Handle;
            }
            return started;
        }

        public Task WaitForExitAsync() => inner.WaitForExitAsync();

        public void KillTree() => inner.KillTree();

        public void CloseOutput() => inner.CloseOutput();

        public void Dispose()
        {
            DisposeCount++;
            inner.Dispose();
        }

        internal async Task CleanupAsync()
        {
            if (Probe is null)
            {
                return;
            }
            try
            {
                if (!Probe.HasExited)
                {
                    Probe.Kill(entireProcessTree: true);
                    await Probe.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }
            }
            finally
            {
                Probe.Dispose();
            }
        }
    }
}
