using System.Globalization;

namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class ProcessOutputBoundaryTests
{
    [Fact]
    public async Task RedactsProcessLocalMultilineSecretsAcrossByteChunks()
    {
        const string secret = "first-line\r\nsecond-line";
        StringWriter output = new(CultureInfo.InvariantCulture);
        Command command = PhaseFiveTestSupport.CreateCommand();
        PhaseFiveTestSupport.ConfigureServices(command, output);
        Target entry = command.Target("stream").Description("Stream a local multiline secret.")
            .Run(context => context.Process(FixturePath())
                .Argument("emit")
                .Option("--stdout", secret)
                .Option("--chunk-bytes", "1")
                .Environment(environment => environment.SetSensitive("LOCAL_SECRET", secret))
                .Run());

        int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("<redacted>").And.NotContain("first-line").And.NotContain("second-line");
    }

    [Fact]
    public async Task CapturePreservesRawNewlinesAndIsRedactedOnlyWhenPresented()
    {
        const string secret = "disposable-capture-secret";
        const string expected = secret + "\r\nsecond\rthird\nunterminated";
        ProcessCapture? capture = null;
        StringWriter output = new(CultureInfo.InvariantCulture);
        Command command = PhaseFiveTestSupport.CreateCommand();
        PhaseFiveTestSupport.ConfigureServices(command, output);
        _ = command.RequiredOption<string>("secret").Description("Invocation-wide secret.").Sensitive();
        Target entry = command.Target("capture").Description("Present captured program data.")
            .Run(async context =>
            {
                capture = await context.Process(FixturePath()).Argument("emit")
                    .Option("--stdout", expected).Capture().ConfigureAwait(false);
                output.ToString().Should().BeEmpty("capture does not present data automatically");
                context.Output.Line(capture.StandardOutput);
                Console.Error.WriteLine(capture.StandardOutput);
            });
        StringWriter error = new(CultureInfo.InvariantCulture);
        PhaseFiveTestSupport.ConfigureServices(command, output, error);

        int exitCode = await command.RunAsync(entry, ["--secret", secret], TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        capture!.StandardOutput.Should().Be(expected);
        (output.ToString() + error).Should().NotContain(secret).And.Contain("<redacted>");
    }

    [Theory]
    [InlineData("12345", "1234", ProcessOutputStream.StandardOutput)]
    [InlineData("1234", "12345", ProcessOutputStream.StandardError)]
    [InlineData("12345", "12345", ProcessOutputStream.Both)]
    public async Task CaptureOverflowIdentifiesEachAffectedStream(
        string stdout,
        string stderr,
        ProcessOutputStream expectedStream)
    {
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target entry = command.Target("overflow").Description("Overflow bounded capture.")
            .Run(context => context.Process(FixturePath()).Argument("emit")
                .Option("--stdout", stdout).Option("--stderr", stderr).CaptureLimitBytes(4).Capture());

        int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(1);
        ProcessOutputException failure = command.LastExecutionOutcome!.Targets.Single().PrimaryException
            .Should().BeOfType<ProcessOutputException>().Subject;
        failure.Reason.Should().Be(ProcessOutputReason.CaptureLimitExceeded);
        failure.Stream.Should().Be(expectedStream);
        failure.LimitBytes.Should().Be(4);
    }

    [Fact]
    public async Task SinkFailureStillDrainsBothChildPipesAndSettlesTheTarget()
    {
        Command command = PhaseFiveTestSupport.CreateCommand();
        PhaseFiveTestSupport.ConfigureServices(command, new FailingWriter());
        bool completed = false;
        Target entry = command.Target("drain").Description("Drain after a failed sink.")
            .Run(async context =>
            {
                await context.Process(FixturePath()).Argument("emit")
                    .Option("--stdout", new string('a', 4096))
                    .Option("--stderr", new string('b', 4096))
                    .Option("--repeat", "32")
                    .Timeout(TimeSpan.FromSeconds(10)).Run().ConfigureAwait(false);
                completed = true;
            });

        int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(1);
        completed.Should().BeTrue();
        command.LastOutputFailure.Should().BeOfType<IOException>();
        command.LastExecutionOutcome!.Targets.Single().PrimaryException.Should().BeNull();
        ProcessOperationReaper.Count.Should().Be(0);
    }

    private static string FixturePath()
    {
        string executable = OperatingSystem.IsWindows()
            ? "Sotsera.Rafter.ProcessFixture.exe"
            : "Sotsera.Rafter.ProcessFixture";
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Name;
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "Sotsera.Rafter.ProcessFixture", configuration, executable));
    }

    private sealed class FailingWriter : StringWriter
    {
        public override void Write(string? value) => throw new IOException("Expected sink failure.");
    }
}
