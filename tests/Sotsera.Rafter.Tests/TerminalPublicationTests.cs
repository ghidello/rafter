namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class TerminalPublicationTests
{
    [Fact]
    public async Task ReportsUseTheSynchronousTerminalPrimitive()
    {
        DistinctWriter writer = new();

        Task<bool> publication = WriteReportAsync(writer);

        publication.IsCompletedSuccessfully.Should().BeTrue();
        (await publication).Should().BeTrue();
        writer.SynchronousCalls.Should().Be(1);
        writer.AsynchronousCalls.Should().Be(0);
        writer.ToString().Should().Be("report\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReportsAndSemanticEventsShareOnePublicationBoundary(bool reportsOnly)
    {
        ConcurrentWriter writer = new();
        InvocationOutput output = new(writer, writer, OutputCapabilities.Plain, OutputCapabilities.Plain,
            TextRedactor.Empty);
        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task[] writers = Enumerable.Range(0, 64).Select(index => Task.Run(async () =>
        {
            await start.Task.ConfigureAwait(false);
            if (reportsOnly || index % 2 == 0)
            {
                (await WriteReportAsync(writer).ConfigureAwait(false)).Should().BeTrue();
            }
            else
            {
                output.Publish(new OutputEvent("work", OutputKind.Line, "payload"));
            }
        }, TestContext.Current.CancellationToken)).ToArray();

        start.SetResult();
        await Task.WhenAll(writers).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        writer.OverlappingCalls.Should().Be(0);
        writer.Calls.Should().Be(64);
        output.Failure.Should().BeNull();
    }

    [Fact]
    public async Task ReentrantReportSinksDoNotBecomeManagedInputOfAnActiveCommand()
    {
        TextWriter original = Console.Out;
        StringWriter host = new();
        StringWriter managed = new();
        ReentrantWriter report = new();
        try
        {
            Console.SetOut(host);
            Command command = PhaseFiveTestSupport.CreateCommand();
            PhaseFiveTestSupport.ConfigureServices(command, managed);
            Target entry = command.Target("work").Description("Report.").Run(async () =>
            {
                (await WriteReportAsync(report).ConfigureAwait(false)).Should().BeTrue();
                Console.WriteLine("after report");
            });

            (await command.RunAsync(entry, [], TestContext.Current.CancellationToken)).Should().Be(0);

            report.ToString().Should().Be("report\n");
            host.ToString().Should().Be($"sink host output{Environment.NewLine}");
            managed.ToString().Should().Be("[work] after report\n\nCommand succeeded\n  [work] Succeeded\n");
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    [Fact]
    public async Task FailedReportRestoresManagedConsoleAttribution()
    {
        StringWriter managed = new();
        IOException failure = new("report failed");
        ThrowingWriter report = new(failure);
        Command command = PhaseFiveTestSupport.CreateCommand();
        PhaseFiveTestSupport.ConfigureServices(command, managed);
        Target entry = command.Target("work").Description("Fail report.").Run(async () =>
        {
            Func<Task> write = async () => _ = await WriteReportAsync(report).ConfigureAwait(false);
            (await write.Should().ThrowAsync<IOException>().ConfigureAwait(false)).Which.Should().BeSameAs(failure);
            Console.WriteLine("after failure");
        });

        (await command.RunAsync(entry, [], TestContext.Current.CancellationToken)).Should().Be(0);

        managed.ToString().Should().Be("[work] after failure\n\nCommand succeeded\n  [work] Succeeded\n");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task EarlyHelpFlushesPendingConsoleTextForTheSameSink(bool sameSink, bool standardError)
    {
        StringWriter managed = new();
        StringWriter helpOutput = sameSink ? managed : new StringWriter();
        Command help = PhaseFiveTestSupport.CreateCommand();
        PhaseFiveTestSupport.ConfigureServices(help, helpOutput);
        Target helpEntry = help.Target("help-entry").Description("Help entry.");
        Command command = PhaseFiveTestSupport.CreateCommand();
        PhaseFiveTestSupport.ConfigureServices(command,
            output: standardError ? new StringWriter() : managed,
            error: standardError ? managed : new StringWriter());
        string? beforeReturn = null;
        Target entry = command.Target("work").Description("Write partial output.").Run(async () =>
        {
            TextWriter console = standardError ? Console.Error : Console.Out;
            console.Write("earlier partial line");
            (await help.RunAsync(helpEntry, ["--help"], TestContext.Current.CancellationToken)
                .ConfigureAwait(false)).Should().Be(0);
            beforeReturn = managed.ToString();
        });

        (await command.RunAsync(entry, [], TestContext.Current.CancellationToken)).Should().Be(0);

        if (sameSink)
        {
            beforeReturn.Should().StartWith("[work] earlier partial line\nTest command.\n");
        }
        else
        {
            beforeReturn.Should().BeEmpty("a report on a different sink must not drain this invocation's buffer");
            helpOutput.ToString().Should().StartWith("Test command.\n").And.NotContain("earlier partial line");
        }
    }

    [Fact]
    public async Task FirstManagedSinkFailureSuppressesConcurrentLaterPublications()
    {
        IOException failure = new("sink failed");
        ThrowingWriter writer = new(failure);
        InvocationOutput output = new(writer, writer, OutputCapabilities.Plain, OutputCapabilities.Plain,
            TextRedactor.Empty);

        await Task.WhenAll(Enumerable.Range(0, 64).Select(_ => Task.Run(
            () => output.Publish(new OutputEvent("work", OutputKind.Line, "payload")),
            TestContext.Current.CancellationToken)));

        writer.Calls.Should().Be(1);
        output.Failure.Should().BeSameAs(failure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeferredSinkConsoleWritesReenterManagedOutputAfterPublicationEnds(bool report)
    {
        const string secret = "deferred-sink-secret";
        TextWriter original = Console.Out;
        StringWriter host = new();
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        DeferredWriter sink = new(secret, release.Task);
        StringWriter managed = report ? new StringWriter() : sink;
        try
        {
            Console.SetOut(host);
            Command command = PhaseFiveTestSupport.CreateCommand();
            PhaseFiveTestSupport.ConfigureServices(command, managed);
            _ = command.RequiredOption<string>("secret").Description("Secret.").Sensitive();
            Target entry = command.Target("work").Description("Schedule a sink write.").Run(async context =>
            {
                if (report)
                {
                    (await WriteReportAsync(sink).ConfigureAwait(false)).Should().BeTrue();
                }
                else
                {
                    context.Output.Line("schedule");
                }
                release.SetResult();
                await sink.Pending!.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)
                    .ConfigureAwait(false);
            });

            (await command.RunAsync(entry, ["--secret", secret], TestContext.Current.CancellationToken)).Should().Be(0);

            host.ToString().Should().BeEmpty();
            managed.ToString().Should().Contain("[work] <redacted>\n").And.NotContain(secret);
        }
        finally
        {
            release.TrySetResult();
            try
            {
                if (sink.Pending is not null)
                {
                    await sink.Pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                }
            }
            finally
            {
                Console.SetOut(original);
            }
        }
    }

    private static Task<bool> WriteReportAsync(TextWriter writer)
        => CommandPresentation.WriteAsync(new CommandPresentation.Report([
            new CommandPresentation.ReportLine("report", CommandPresentation.LineRole.Text),
        ]), writer, OutputCapabilities.Plain, TextRedactor.Empty);

    private sealed class DeferredWriter(string text, Task release) : StringWriter
    {
        private int _scheduled;

        internal Task? Pending { get; private set; }

        public override void Write(string? value)
        {
            base.Write(value);
            if (Interlocked.Exchange(ref _scheduled, 1) == 0)
            {
                Pending = Task.Run(async () =>
                {
                    await release.WaitAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
                    Console.WriteLine(text);
                }, TestContext.Current.CancellationToken);
            }
        }
    }

    private sealed class DistinctWriter : StringWriter
    {
        internal int SynchronousCalls { get; private set; }

        internal int AsynchronousCalls { get; private set; }

        public override void Write(string? value)
        {
            SynchronousCalls++;
            base.Write(value);
        }

        public override Task WriteAsync(string? value)
        {
            AsynchronousCalls++;
            base.Write(value);
            return Task.CompletedTask;
        }
    }

    private sealed class ConcurrentWriter : StringWriter
    {
        private int _active;
        private int _calls;
        private int _overlapping;

        internal int OverlappingCalls => Volatile.Read(ref _overlapping);

        internal int Calls => Volatile.Read(ref _calls);

        public override void Write(string? value)
        {
            if (Interlocked.Increment(ref _active) != 1)
            {
                Interlocked.Increment(ref _overlapping);
            }
            try
            {
                Thread.SpinWait(100_000);
                Interlocked.Increment(ref _calls);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }
    }

    private sealed class ReentrantWriter : StringWriter
    {
        public override void Write(string? value)
        {
            base.Write(value);
            Console.WriteLine("sink host output");
        }
    }

    private sealed class ThrowingWriter(Exception failure) : StringWriter
    {
        private int _calls;

        internal int Calls => Volatile.Read(ref _calls);

        public override void Write(string? value)
        {
            Interlocked.Increment(ref _calls);
            throw failure;
        }
    }
}
