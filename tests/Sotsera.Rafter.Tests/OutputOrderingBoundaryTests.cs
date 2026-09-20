namespace Sotsera.Rafter.Tests;

public sealed class OutputOrderingBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnresolvedConsolePrefixFailsClosedAtASemanticBarrier(bool standardError)
    {
        StringWriter sink = new();
        InvocationOutput invocation = Create(sink, "secret");
        OutputScope scope = new("work");
        RafterOutput output = new(invocation, scope);

        invocation.TryPublishConsole(standardError, scope, "sec").Should().BeTrue();
        if (standardError)
        {
            output.Warning("status");
        }
        else
        {
            output.Line("status");
        }
        invocation.TryPublishConsole(standardError, scope, "ret\n").Should().BeTrue();
        await invocation.SealAsync();

        invocation.Failure.Should().BeOfType<InvalidOperationException>();
        sink.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData("sec")]
    [InlineData("prefix sec")]
    [InlineData("sec\nret sec")]
    public async Task SemanticPrefixesCannotLeakAcrossLaterEventKinds(string text)
    {
        StringWriter sink = new();
        InvocationOutput invocation = Create(sink, "sec\nret");
        RafterOutput output = new(invocation, new OutputScope("work"));

        output.Line(text);
        invocation.TryPublishConsole(false, new OutputScope("work"), "ret\n").Should().BeTrue();
        await invocation.SealAsync();

        invocation.Failure.Should().BeOfType<InvalidOperationException>();
        sink.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData("sec", "[work] sec\n")]
    [InlineData("secret", "[work] <redacted>\n")]
    public async Task FinalizationResolvesPrefixesWithoutInventingFutureInput(string text, string expected)
    {
        StringWriter sink = new();
        InvocationOutput invocation = Create(sink, "secret");
        invocation.TryPublishConsole(false, new OutputScope("work"), text).Should().BeTrue();

        await invocation.SealAsync();

        invocation.Failure.Should().BeNull();
        sink.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData(65535)]
    [InlineData(65536)]
    [InlineData(65537)]
    public async Task SegmentLimitsUseContinuationMarkersAndRetainAttribution(int length)
    {
        StringWriter sink = new();
        InvocationOutput invocation = Create(sink);
        invocation.TryPublishConsole(false, new OutputScope("work"), new string('x', length) + "\n").Should().BeTrue();
        await invocation.SealAsync();

        string expected = length <= 65536 ? "[work] " + new string('x', length) + "\n"
            : "[work] " + new string('x', 65536) + " [continues]\n[work] [continued] x\n";
        sink.ToString().Should().Be(expected);
        invocation.Failure.Should().BeNull();
    }

    [Fact]
    public async Task ConcurrentMultilineWritesPublishWholeBatchesInSequence()
    {
        YieldingWriter sink = new();
        InvocationOutput invocation = Create(sink);
        Task[] tasks = Enumerable.Range(0, 64).Select(index => Task.Run(() =>
        {
            string payload = string.Concat(Enumerable.Repeat($"batch-{index}\n", 8));
            invocation.TryPublishConsole(false, new OutputScope("work"), payload).Should().BeTrue();
        }, TestContext.Current.CancellationToken)).ToArray();
        await Task.WhenAll(tasks);
        await invocation.SealAsync();

        string[] lines = sink.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Length.Should().Be(512);
        foreach (string[] batch in lines.Chunk(8))
        {
            batch.Should().OnlyContain(line => string.Equals(line, batch[0], StringComparison.Ordinal));
        }
        lines.Distinct(StringComparer.Ordinal).Count().Should().Be(64);
        invocation.Failure.Should().BeNull();
    }

    private static InvocationOutput Create(TextWriter sink, params string[] secrets)
        => new(sink, sink, OutputCapabilities.Plain, OutputCapabilities.Plain, TextRedactor.Create([.. secrets]));

    private sealed class YieldingWriter : StringWriter
    {
        public override void Write(string? value)
        {
            Thread.Yield();
            base.Write(value);
        }
    }
}
