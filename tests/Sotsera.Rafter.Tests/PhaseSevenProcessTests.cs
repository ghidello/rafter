using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class PhaseSevenProcessTests
{
    [Fact]
    public async Task CapturesExactArgumentTokensAndWorkingDirectory()
    {
        ProcessCapture? capture = null;
        Command command = PhaseFiveTestSupport.CreateCommand();
        string[] expected = ["", "two words", "\"quoted\"", "&|;$()", "--name=value"];
        Target target = command.Target("inspect")
            .Description("Inspect argument tokens.")
            .Run(async context =>
            {
                ProcessBuilder process = context.Process(GetFixturePath()).Argument("inspect");
                foreach (string value in expected)
                {
                    process = process.Argument(value);
                }

                capture = await process.Capture().ConfigureAwait(false);
            });

        int exitCode = await command.RunAsync(target, [], TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        exitCode.Should().Be(0);
        using JsonDocument document = JsonDocument.Parse(capture!.StandardOutput);
        document.RootElement.GetProperty("arguments").EnumerateArray()
            .Select(static item => item.GetString()).Should().Equal(expected);
        document.RootElement.GetProperty("workingDirectory").GetString()
            .Should().Be(Path.TrimEndingDirectorySeparator(Directory.GetCurrentDirectory()));
    }

    [Fact]
    public async Task DrainsBothStreamsBeyondPipeCapacityWithoutDeadlock()
    {
        ProcessCapture? capture = null;
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target target = command.Target("capture")
            .Description("Capture simultaneous output.")
            .Run(async context => capture = await context.Process(GetFixturePath())
                .Argument("emit")
                .Option("--stdout", new string('o', 4096))
                .Option("--stderr", new string('e', 4096))
                .Option("--repeat", "64")
                .Option("--chunk-bytes", "997")
                .CaptureLimitBytes(512 * 1024)
                .Capture().ConfigureAwait(false));

        int exitCode = await command.RunAsync(target, [], TestContext.Current.CancellationToken).ConfigureAwait(true);

        exitCode.Should().Be(0);
        capture!.StandardOutput.Should().Be(new string('o', 256 * 1024));
        capture.StandardError.Should().Be(new string('e', 256 * 1024));
    }

    [Fact]
    public async Task ReportsCaptureOverflowAfterTheChildExits()
    {
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target target = command.Target("overflow")
            .Description("Overflow capture.")
            .Run(context => context.Process(GetFixturePath())
                .Argument("emit")
                .Option("--stdout", "12345")
                .CaptureLimitBytes(4)
                .Capture());

        int exitCode = await command.RunAsync(target, [], TestContext.Current.CancellationToken).ConfigureAwait(true);

        exitCode.Should().Be(1);
        Exception failure = command.LastExecutionOutcome!.Targets.Single().PrimaryException!;
        ProcessOutputException exception = failure.Should()
            .BeOfType<ProcessOutputException>(failure.ToString()).Subject;
        exception.Reason.Should().Be(ProcessOutputReason.CaptureLimitExceeded);
        exception.Stream.Should().Be(ProcessOutputStream.StandardOutput);
        exception.LimitBytes.Should().Be(4);
    }

    [Fact]
    public async Task RejectsMalformedUtf8WithoutReplacementText()
    {
        Command command = PhaseFiveTestSupport.CreateCommand();
        string malformed = Convert.ToBase64String([0x61, 0xC3, 0x28]);
        Target target = command.Target("decode")
            .Description("Decode strict UTF-8.")
            .Run(context => context.Process(GetFixturePath())
                .Argument("emit")
                .Option("--stdout-base64", malformed)
                .Capture());

        int exitCode = await command.RunAsync(target, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(1);
        Exception failure = command.LastExecutionOutcome!.Targets.Single().PrimaryException!;
        ProcessOutputException exception = failure.Should()
            .BeOfType<ProcessOutputException>(failure.ToString()).Subject;
        exception.Reason.Should().Be(ProcessOutputReason.InvalidUtf8);
        exception.Stream.Should().Be(ProcessOutputStream.StandardOutput);
    }

    [Fact]
    public async Task PreservesALeadingUtf8BomAsProcessData()
    {
        ProcessCapture? capture = null;
        Command command = PhaseFiveTestSupport.CreateCommand();
        string payload = Convert.ToBase64String([0xEF, 0xBB, 0xBF, 0x61]);
        Target target = command.Target("bom")
            .Description("Capture a BOM.")
            .Run(async context => capture = await context.Process(GetFixturePath())
                .Argument("emit")
                .Option("--stdout-base64", payload)
                .Option("--chunk-bytes", "1")
                .Capture().ConfigureAwait(false));

        int exitCode = await command.RunAsync(target, [], TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        exitCode.Should().Be(0);
        capture!.StandardOutput.Should().Be("\uFEFFa");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task CapturesUtf8SplitAcrossEveryPossibleRuneBoundary(int chunkBytes)
    {
        const string expected = "a€𐍈z";
        ProcessCapture? capture = null;
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target target = command.Target("utf8")
            .Description("Capture split UTF-8.")
            .Run(async context => capture = await context.Process(GetFixturePath())
                .Argument("emit")
                .Option("--stdout", expected)
                .Option("--chunk-bytes", chunkBytes.ToString(CultureInfo.InvariantCulture))
                .Capture().ConfigureAwait(false));

        int exitCode = await command.RunAsync(target, [], TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        exitCode.Should().Be(0);
        capture!.StandardOutput.Should().Be(expected);
    }

    [Fact]
    public async Task AcceptsCaptureExactlyAtThePerStreamLimit()
    {
        ProcessCapture? capture = null;
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target target = command.Target("limit")
            .Description("Capture at the limit.")
            .Run(async context => capture = await context.Process(GetFixturePath())
                .Argument("emit")
                .Option("--stdout", "1234")
                .Option("--stderr", "abcd")
                .CaptureLimitBytes(4)
                .Capture().ConfigureAwait(false));

        int exitCode = await command.RunAsync(target, [], TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        exitCode.Should().Be(0);
        capture!.StandardOutput.Should().Be("1234");
        capture.StandardError.Should().Be("abcd");
    }

    [Fact]
    public async Task ReportsSpecificationDiagnosticsInAuthoredOrderWithoutLaunching()
    {
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target target = command.Target("invalid")
            .Description("Reject an invalid process specification.")
            .Run(context => context.Process(string.Empty)
                .Argument("\0")
                .Flag(" ")
                .Environment(environment => environment.Set("=", "\0"))
                .WorkingDirectory(Path.GetFullPath(Path.Combine(context.Root, "..")))
                .Timeout(TimeSpan.Zero)
                .Timeout(TimeSpan.FromSeconds(1))
                .CaptureLimitBytes(0)
                .ValidExitCodes()
                .Run());

        int exitCode = await command.RunAsync(target, [], TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        exitCode.Should().Be(1);
        string message = command.LastExecutionOutcome!.Targets.Single().PrimaryException
            .Should().BeOfType<ProcessException>().Subject.Message;
        string[] expectedCodes =
        [
            "RAFTER1501",
            "RAFTER1503",
            "RAFTER1504",
            "RAFTER1505",
            "RAFTER1506",
            "RAFTER1507",
            "RAFTER1508",
            "RAFTER1511",
            "RAFTER1509",
            "RAFTER1510",
            "RAFTER1512",
        ];
        message.Should().ContainAll(expectedCodes);
        expectedCodes.Select(code => message.IndexOf(code, StringComparison.Ordinal)).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task UsesOneMarkerSafeForInvocationAndProcessLocalSecrets()
    {
        StringWriter output = new(CultureInfo.InvariantCulture);
        Command command = PhaseFiveTestSupport.CreateCommand();
        PhaseFiveTestSupport.ConfigureServices(command, output);
        RequiredOption<string> secret = command.RequiredOption<string>("secret")
            .Description("Secret used by the process.")
            .Sensitive();
        Target target = command.Target("stream")
            .Description("Stream redacted output.")
            .Run(context => context.Process(GetFixturePath())
                .Argument("emit")
                .Option("--stdout", secret)
                .Environment(environment => environment.SetSensitive("LOCAL_SECRET", "<redacted>"))
                .Run());

        int exitCode = await command.RunAsync(
            target,
            ["--secret", "abc"],
            TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        output.ToString().Should().NotContain("abc").And.NotContain("<redacted>");
        output.ToString().Should().Contain("<redacted:1>");
    }

    [Fact]
    public async Task PreservesCompleteCaptureOnAnExplicitlyInvalidExit()
    {
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target target = command.Target("exit")
            .Description("Reject exit.")
            .Run(context => context.Process(GetFixturePath())
                .Argument("emit")
                .Option("--stdout", "captured")
                .Option("--exit-code", "7")
                .Capture());

        int exitCode = await command.RunAsync(target, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(1);
        ProcessExitException exception = command.LastExecutionOutcome!.Targets.Single().PrimaryException
            .Should().BeOfType<ProcessExitException>().Subject;
        exception.ExitCode.Should().Be(7);
        exception.Capture!.StandardOutput.Should().Be("captured");
    }

    [Fact]
    public async Task ReturnsAnExplicitlyValidNonzeroStreamingExit()
    {
        ProcessExit? processExit = null;
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target target = command.Target("exit")
            .Description("Accept a nonzero exit.")
            .Run(async context => processExit = await context.Process(GetFixturePath())
                .Argument("emit")
                .Option("--exit-code", "7")
                .ValidExitCodes(7)
                .Run().ConfigureAwait(false));

        int exitCode = await command.RunAsync(target, [], TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        exitCode.Should().Be(0);
        processExit.Should().Be(new ProcessExit(7));
    }

    [Fact]
    public async Task ReportsAStreamingInvalidExitWithoutRetainingCapture()
    {
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target target = command.Target("exit")
            .Description("Reject a streaming exit.")
            .Run(context => context.Process(GetFixturePath())
                .Argument("emit")
                .Option("--stdout", "presented-only")
                .Option("--exit-code", "7")
                .Run());

        int exitCode = await command.RunAsync(target, [], TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        exitCode.Should().Be(1);
        ProcessExitException exception = command.LastExecutionOutcome!.Targets.Single().PrimaryException
            .Should().BeOfType<ProcessExitException>().Subject;
        exception.ExitCode.Should().Be(7);
        exception.Capture.Should().BeNull();
    }

    [Fact]
    public async Task ReportsStartupFailureButHonorsPreCancellationBeforeLaunch()
    {
        string missingExecutable = Path.Combine("artifacts", $"missing-{Guid.NewGuid():N}", "fixture");
        Command startupCommand = PhaseFiveTestSupport.CreateCommand();
        Target startupTarget = startupCommand.Target("start")
            .Description("Fail process startup.")
            .Run(context => context.Process(missingExecutable).Run());

        int startupExit = await startupCommand.RunAsync(
            startupTarget,
            [],
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        startupExit.Should().Be(1);
        startupCommand.LastExecutionOutcome!.Targets.Single().PrimaryException
            .Should().BeOfType<ProcessStartException>()
            .Which.InnerException.Should().NotBeNull();

        using CancellationTokenSource cancellation = new();
        Command cancelledCommand = PhaseFiveTestSupport.CreateCommand();
        Target cancelledTarget = cancelledCommand.Target("cancel")
            .Description("Cancel before process startup.")
            .Run(context =>
            {
                cancellation.Cancel();
                return context.Process(missingExecutable).Run();
            });

        int cancelledExit = await cancelledCommand.RunAsync(cancelledTarget, [], cancellation.Token)
            .ConfigureAwait(true);

        cancelledExit.Should().Be(130);
        cancelledCommand.LastExecutionOutcome!.Targets.Single().PrimaryException
            .Should().BeOfType<OperationCanceledException>();
    }

    [Fact]
    public async Task ReusesAnImmutableBuilderForConcurrentIndependentLaunches()
    {
        ProcessCapture[]? captures = null;
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target target = command.Target("reuse")
            .Description("Reuse a process builder.")
            .Run(async context =>
            {
                ProcessBuilder process = context.Process(GetFixturePath()).Argument("emit");
                captures = await Task.WhenAll(
                    process.Option("--stdout", "first").Capture(),
                    process.Option("--stdout", "second").Capture()).ConfigureAwait(false);
            });

        int exitCode = await command.RunAsync(target, [], TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        exitCode.Should().Be(0);
        captures!.Select(static capture => capture.StandardOutput).Should().Equal("first", "second");
    }

    [Fact]
    public async Task RejectsCapturePolicyInStreamingModeBeforeLaunch()
    {
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target target = command.Target("invalid")
            .Description("Reject capture policy.")
            .Run(context => context.Process(GetFixturePath())
                .Argument("wait")
                .CaptureLimitBytes(10)
                .Run());

        int exitCode = await command.RunAsync(target, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(1);
        ProcessException exception = command.LastExecutionOutcome!.Targets.Single().PrimaryException
            .Should().BeOfType<ProcessException>().Subject;
        exception.Message.Should().Contain("RAFTER1512");
    }

    [Fact]
    public async Task AppliesOrderedEnvironmentEditsWithoutMutatingTheParent()
    {
        ProcessCapture? capture = null;
        string? parentValue = Environment.GetEnvironmentVariable("RAFTER_PROCESS_TEST");
        string dotnet = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..",
            OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target target = command.Target("environment")
            .Description("Inspect child environment.")
            // An explicit host still works after Clear removes a custom installation's DOTNET_ROOT.
            .Run(async context => capture = await context.Process(dotnet)
                .Argument(Path.Combine(Path.GetDirectoryName(GetFixturePath())!, "Sotsera.Rafter.ProcessFixture.dll"))
                .Argument("environment")
                .Environment(environment => environment
                    .Set("DISCARDED", "before-clear")
                    .Clear()
                    .Set("CI", "true")
                    .Set("EMPTY", string.Empty))
                .Capture().ConfigureAwait(false));

        int exitCode = await command.RunAsync(target, [], TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        exitCode.Should().Be(0);
        using JsonDocument document = JsonDocument.Parse(capture!.StandardOutput);
        document.RootElement.GetProperty("CI").GetString().Should().Be("true");
        document.RootElement.GetProperty("EMPTY").GetString().Should().BeEmpty();
        document.RootElement.GetProperty("DISCARDED").ValueKind.Should().Be(JsonValueKind.Null);
        Environment.GetEnvironmentVariable("RAFTER_PROCESS_TEST").Should().Be(parentValue);
    }

    [Fact]
    public async Task ResolvesTheProcessWorkingDirectoryFromTheTargetDirectory()
    {
        string relative = Path.Combine("artifacts", "phase-seven-working", Guid.NewGuid().ToString("N"));
        string expected = Path.GetFullPath(relative);
        Directory.CreateDirectory(expected);
        try
        {
            ProcessCapture? capture = null;
            Command command = PhaseFiveTestSupport.CreateCommand();
            Target target = command.Target("working")
                .Description("Inspect process working directory.")
                .Run(async context => capture = await context.Process(GetFixturePath())
                    .Argument("working-directory")
                    .WorkingDirectory(relative)
                    .Capture().ConfigureAwait(false));

            int exitCode = await command.RunAsync(target, [], TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            exitCode.Should().Be(0);
            capture!.StandardOutput.Should().Be(Path.TrimEndingDirectorySeparator(expected));
        }
        finally
        {
            Directory.Delete(expected);
        }
    }

    [Fact]
    public async Task AuthoredTimeoutTerminatesTheDirectChild()
    {
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target target = command.Target("timeout")
            .Description("Time out child.")
            .Run(context => context.Process(GetFixturePath())
                .Argument("wait")
                .Timeout(TimeSpan.FromMilliseconds(100))
                .Run());

        int exitCode = await command.RunAsync(target, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(1);
        command.LastExecutionOutcome!.Targets.Single().PrimaryException
            .Should().BeOfType<ProcessTimeoutException>();
        ProcessOperationReaper.Count.Should().Be(0);
    }

    [Fact]
    public async Task AuthoredTimeoutTerminatesAReportedProcessTree()
    {
        string controlDirectory = Path.Combine("artifacts", "phase-seven-control", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(controlDirectory);
        try
        {
            Command command = PhaseFiveTestSupport.CreateCommand();
            Target target = command.Target("tree")
                .Description("Time out a process tree.")
                .Run(context => context.Process(GetFixturePath())
                    .Argument("spawn-child")
                    .Option("--control-directory", Path.GetFullPath(controlDirectory))
                    .Option("--parent-process-id", Environment.ProcessId.ToString(CultureInfo.InvariantCulture))
                    .Option("--depth", "2")
                    .Timeout(TimeSpan.FromMilliseconds(750))
                    .Run());

            int exitCode = await command.RunAsync(target, [], TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            exitCode.Should().Be(1);
            command.LastExecutionOutcome!.Targets.Single().PrimaryException
                .Should().BeOfType<ProcessTimeoutException>();

            JsonElement[] metadata = Directory.EnumerateFiles(controlDirectory, "*.json")
                .Select(ReadMetadata)
                .ToArray();
            metadata.Should().HaveCount(3);
            int[] processIds = metadata.Select(static item => item.GetProperty("processId").GetInt32()).ToArray();
            metadata.Count(item => item.GetProperty("parentProcessId").GetInt32() == Environment.ProcessId)
                .Should().Be(1);
            metadata.Count(item => processIds.Contains(item.GetProperty("parentProcessId").GetInt32()))
                .Should().Be(2);
            processIds.Should().OnlyContain(processId => !IsProcessRunning(processId));
            ProcessOperationReaper.Count.Should().Be(0);
        }
        finally
        {
            await TerminateFixtureProcessesAsync(controlDirectory).ConfigureAwait(true);
            Directory.Delete(controlDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task InvocationCancellationPreservesStandardCancellationClassification()
    {
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(100));
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target target = command.Target("cancel")
            .Description("Cancel child.")
            .Run(context => context.Process(GetFixturePath())
                .Argument("wait")
                .Run());

        int exitCode = await command.RunAsync(target, [], cancellation.Token);

        exitCode.Should().Be(130);
        command.LastExecutionOutcome!.Targets.Single().PrimaryException
            .Should().BeOfType<OperationCanceledException>();
        ProcessOperationReaper.Count.Should().Be(0);
    }

    [Fact]
    public async Task ADiscardedActiveProcessFailsAndIsSettledBeforeTheCallbackCloses()
    {
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target target = command.Target("discard")
            .Description("Discard child task.")
            .Run(context =>
            {
                _ = context.Process(GetFixturePath())
                    .Argument("wait")
                    .Run();
            });

        int exitCode = await command.RunAsync(target, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(1);
        command.LastExecutionOutcome!.Targets.Single().PrimaryException
            .Should().BeOfType<InvalidOperationException>();
        ProcessOperationReaper.Count.Should().Be(0);
    }

    [Theory]
    [InlineData("stdout", ProcessOutputStream.StandardOutput)]
    [InlineData("stderr", ProcessOutputStream.StandardError)]
    [InlineData("both", ProcessOutputStream.Both)]
    public async Task FailsWithinTheDrainDeadlineWhenADescendantRetainsPipes(
        string retainedStream,
        ProcessOutputStream expectedStream)
    {
        string controlDirectory = Path.Combine("artifacts", "phase-seven-control", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(controlDirectory);
        try
        {
            Command command = PhaseFiveTestSupport.CreateCommand();
            Target target = command.Target("retain")
                .Description("Retain redirected pipes.")
                .Run(context => context.Process(GetFixturePath())
                    .Argument("retain-pipe")
                    .Option("--control-directory", Path.GetFullPath(controlDirectory))
                    .Option("--parent-process-id", Environment.ProcessId.ToString(CultureInfo.InvariantCulture))
                    .Option("--stream", retainedStream)
                    .Capture());

            long started = Stopwatch.GetTimestamp();
            int exitCode = await command.RunAsync(target, [], TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            TimeSpan elapsed = Stopwatch.GetElapsedTime(started);

            exitCode.Should().Be(1);
            ProcessOutputException exception = command.LastExecutionOutcome!.Targets.Single().PrimaryException
                .Should().BeOfType<ProcessOutputException>().Subject;
            exception.Reason.Should().Be(ProcessOutputReason.RetainedPipe);
            exception.Stream.Should().Be(expectedStream);
            elapsed.Should().BeLessThan(TimeSpan.FromSeconds(6));
        }
        finally
        {
            await TerminateFixtureProcessesAsync(controlDirectory).ConfigureAwait(true);
            Directory.Delete(controlDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task TransfersNonSettlingTeardownToTheTrackedReaper()
    {
        ControlledProcessAdapter process = new();
        IProcessAdapterFactory originalFactory = ProcessRuntime.AdapterFactory;
        ProcessRuntimePolicy originalPolicy = ProcessRuntime.Policy;
        ProcessRuntime.AdapterFactory = new ControlledProcessFactory(process);
        ProcessRuntime.Policy = new ProcessRuntimePolicy(
            TimeSpan.FromMilliseconds(30),
            TimeSpan.FromMilliseconds(30),
            TimeSpan.FromMilliseconds(30),
            TimeSpan.FromMilliseconds(30),
            TimeProvider.System);
        try
        {
            Command command = PhaseFiveTestSupport.CreateCommand();
            Target target = command.Target("reap")
                .Description("Transfer late process work.")
                .Run(context => context.Process("synthetic")
                    .Timeout(TimeSpan.FromMilliseconds(10))
                    .Run());

            long started = Stopwatch.GetTimestamp();
            int exitCode = await command.RunAsync(target, [], TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            TimeSpan elapsed = Stopwatch.GetElapsedTime(started);

            exitCode.Should().Be(1);
            command.LastExecutionOutcome!.Targets.Single().PrimaryException
                .Should().BeOfType<ProcessTimeoutException>();
            elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(1500));
            ProcessOperationReaper.Count.Should().Be(1);
            process.Disposed.Should().BeFalse();

            using CancellationTokenSource settlement = new(TimeSpan.FromSeconds(3));
            await ProcessOperationReaper.WaitForEmptyAsync(settlement.Token).ConfigureAwait(true);
            process.Disposed.Should().BeTrue();
        }
        finally
        {
            process.Release();
            ProcessRuntime.AdapterFactory = originalFactory;
            ProcessRuntime.Policy = originalPolicy;
        }
    }

    private static string GetFixturePath()
    {
        string executable = OperatingSystem.IsWindows()
            ? "Sotsera.Rafter.ProcessFixture.exe"
            : "Sotsera.Rafter.ProcessFixture";
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "Sotsera.Rafter.ProcessFixture",
            "release",
            executable));
    }

    private static async Task TerminateFixtureProcessesAsync(string controlDirectory)
    {
        foreach (string metadata in Directory.EnumerateFiles(controlDirectory, "*.json"))
        {
            using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(metadata).ConfigureAwait(false));
            int processId = document.RootElement.GetProperty("processId").GetInt32();
            try
            {
                using Process process = Process.GetProcessById(processId);
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        using CancellationTokenSource settlement = new(TimeSpan.FromSeconds(2));
        await ProcessOperationReaper.WaitForEmptyAsync(settlement.Token).ConfigureAwait(false);
    }

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static JsonElement ReadMetadata(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    private sealed class ControlledProcessFactory(ControlledProcessAdapter process) : IProcessAdapterFactory
    {
        public IProcessAdapter Create(ProcessStartInfo startInfo) => process;
    }

    private sealed class ControlledProcessAdapter : IProcessAdapter
    {
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim _kill = new(initialState: false);
        private readonly ControlledStream _standardError = new();
        private readonly ControlledStream _standardOutput = new();

        public Stream StandardOutput => _standardOutput;

        public Stream StandardError => _standardError;

        public int Id => 0;

        public bool HasExited => _exit.Task.IsCompleted;

        public int ExitCode => 0;

        internal bool Disposed { get; private set; }

        public bool Start() => true;

        public Task WaitForExitAsync() => _exit.Task;

        public void KillTree()
        {
            _kill.Wait(TimeSpan.FromSeconds(2));
            Release();
        }

        public void CloseOutput()
        {
        }

        public void Dispose()
        {
            Disposed = true;
            _kill.Dispose();
        }

        internal void Release()
        {
            _kill.Set();
            _exit.TrySetResult();
            _standardOutput.Complete();
            _standardError.Complete();
        }
    }

    private sealed class ControlledStream : Stream
    {
        private readonly TaskCompletionSource<int> _read = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
            => new(_read.Task);

        internal void Complete() => _read.TrySetResult(0);
    }
}
