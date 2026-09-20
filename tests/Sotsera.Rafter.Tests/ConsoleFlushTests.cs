namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class ConsoleFlushTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public async Task FlushOverloadsPublishFragmentsSynchronouslyAndFlushTheManagedSink(bool standardError, int mode)
    {
        FlushWriter sink = new();
        Command command = PhaseFiveTestSupport.CreateCommand();
        command.InvocationServicesFactory = () => new InvocationServices(_ => null, sink, sink,
            OutputCapabilities.Plain, OutputCapabilities.Plain, "test");
        Target work = command.Target("work").Description("Flush.").Run(async () =>
        {
            TextWriter writer = standardError ? Console.Error : Console.Out;
            writer.Write("fragment");
            Task completion = Flush(writer, mode);
            completion.IsCompletedSuccessfully.Should().BeTrue();
            await completion.ConfigureAwait(false);
            sink.ToString().Should().Be("[work] fragment [continues]\n");
            sink.FlushCount.Should().Be(1);
            writer.WriteLine("finished");
        });

        (await command.RunAsync(work, [], TestContext.Current.CancellationToken)).Should().Be(0);
        sink.ToString().Should().StartWith("[work] fragment [continues]\n[work] [continued] finished\n");
    }

    [Fact]
    public async Task CancelledFlushDoesNotPublishOrTouchTheSink()
    {
        FlushWriter sink = new();
        Command command = PhaseFiveTestSupport.CreateCommand();
        PhaseFiveTestSupport.ConfigureServices(command, sink);
        Target work = command.Target("work").Description("Cancel flush.").Run(async () =>
        {
            Console.Write("fragment");
            Func<Task> flush = () => Console.Out.FlushAsync(new CancellationToken(canceled: true));
            await flush.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(false);
            sink.ToString().Should().BeEmpty();
            sink.FlushCount.Should().Be(0);
        });

        (await command.RunAsync(work, [], TestContext.Current.CancellationToken)).Should().Be(0);
        sink.ToString().Should().StartWith("[work] fragment\n");
    }

    [Fact]
    public async Task ManagedFlushFailureIsInfrastructureFailureAndDoesNotEscapeTheCallback()
    {
        FlushWriter sink = new(fail: true);
        Command command = PhaseFiveTestSupport.CreateCommand();
        PhaseFiveTestSupport.ConfigureServices(command, sink);
        bool continued = false;
        Target work = command.Target("work").Description("Fail flush.").Run(() =>
        {
            Console.Out.Flush();
            continued = true;
        });

        (await command.RunAsync(work, [], TestContext.Current.CancellationToken)).Should().Be(1);
        continued.Should().BeTrue();
        command.LastOutputFailure.Should().BeSameAs(sink.Failure);
        command.LastExecutionOutcome!.ExitCode.Should().Be(0);
    }

    private static Task Flush(TextWriter writer, int mode)
    {
        if (mode == 0)
        {
            writer.Flush();
            return Task.CompletedTask;
        }
        return mode == 1 ? writer.FlushAsync() : writer.FlushAsync(TestContext.Current.CancellationToken);
    }

    private sealed class FlushWriter(bool fail = false) : StringWriter
    {
        internal int FlushCount { get; private set; }

        internal IOException Failure { get; } = new("flush failure");

        public override void Flush()
        {
            FlushCount++;
            if (fail)
            {
                throw Failure;
            }
        }
    }
}
