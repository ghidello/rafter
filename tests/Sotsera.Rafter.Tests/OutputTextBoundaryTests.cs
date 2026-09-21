using System.Text.Json;

namespace Sotsera.Rafter.Tests;

public sealed class OutputTextBoundaryTests
{
    [Fact]
    public void CanonicalStringDecodingRoundTripsEveryUtf16CodeUnit()
    {
        string original = new(Enumerable.Range(0, char.MaxValue + 1).Select(value => (char)value).ToArray());
        using JsonDocument document = JsonDocument.Parse(OutputProperty.Quote(original));

        string decoded = OutputProperty.DecodeString(document.RootElement);

        decoded.Length.Should().Be(original.Length);
        string.Equals(decoded, original, StringComparison.Ordinal).Should().BeTrue();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SplitCrLfRemainsOneNewlineAcrossRepeatedPublicationBarriers(bool standardError, bool flush)
    {
        StringWriter sink = new();
        InvocationOutput invocation = new(sink, sink, OutputCapabilities.Plain, OutputCapabilities.Plain,
            TextRedactor.Empty);
        OutputScope scope = new("work");
        RafterOutput output = new(invocation, scope);
        invocation.TryPublishConsole(standardError, scope, "first\r").Should().BeTrue();
        for (int barrier = 0; barrier < 2; barrier++)
        {
            if (flush)
            {
                invocation.TryFlushConsole(standardError).Should().BeTrue();
            }
            else
            {
                output.Line("barrier");
            }
        }
        invocation.TryPublishConsole(standardError, scope, string.Empty).Should().BeTrue();
        invocation.TryPublishConsole(standardError, scope, "\nsecond\r").Should().BeTrue();
        invocation.TryFlushConsole(standardError).Should().BeTrue();
        invocation.TryPublishConsole(standardError, scope, "third\n").Should().BeTrue();
        await invocation.SealAsync();

        invocation.Failure.Should().BeNull();
        sink.ToString().Should().Be("[work] first\n"
            + (flush ? string.Empty : "[work] barrier\n[work] barrier\n")
            + "[work] second\n[work] third\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PropertiesPreserveUnpairedSurrogatesAsVisibleEscapes(bool rich)
    {
        StringWriter sink = new();
        OutputCapabilities capabilities = rich
            ? PhaseFiveTestSupport.RichCapabilities with { SupportsColor = false }
            : OutputCapabilities.Plain;
        InvocationOutput invocation = new(sink, sink, capabilities, capabilities, TextRedactor.Empty);
        RafterOutput output = new(invocation, new OutputScope("work"));

        string[] elements = ["\ud800", "\udfff"];
        output.Property("scalar", "a\ud800b\udfff");
        output.Property("array", elements);
        output.Property("multiline", "first\n\ud800last");
        await invocation.SealAsync();

        invocation.Failure.Should().BeNull();
        sink.ToString().Should().Be(rich
            ? "[work] scalar: \"a\\ud800b\\udfff\"\n[work] array: [\"\\ud800\", \"\\udfff\"]\n"
                + "[work] multiline:\n[work]   first\n[work]   \\ud800last\n"
            : "[work] scalar=\"a\\ud800b\\udfff\"\n[work] array=[\"\\ud800\",\"\\udfff\"]\n"
                + "[work] multiline=\"first\\n\\ud800last\"\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DecodedPropertyRedactionMatchesUnpairedSurrogatesBeforeEscaping(bool rich)
    {
        StringWriter sink = new();
        OutputCapabilities capabilities = rich
            ? PhaseFiveTestSupport.RichCapabilities with { SupportsColor = false }
            : OutputCapabilities.Plain;
        InvocationOutput invocation = new(sink, sink, capabilities, capabilities,
            TextRedactor.Create(["\ud800secret"]));
        RafterOutput output = new(invocation, new OutputScope("work"));

        output.Property("value", "\ud800secret");
        await invocation.SealAsync();

        invocation.Failure.Should().BeNull();
        sink.ToString().Should().Be(rich ? "[work] value: \"<redacted>\"\n" : "[work] value=\"<redacted>\"\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SemanticTextEscapesInvalidUtf16WithoutReplacingIt(bool rich)
    {
        OutputCapabilities capabilities = rich
            ? PhaseFiveTestSupport.RichCapabilities with { SupportsColor = false }
            : OutputCapabilities.Plain;
        string rendered = OutputPresentation.Render(new OutputEvent("work", OutputKind.Line, "😀\ud800x\udfff"),
            capabilities);

        rendered.Should().Be("[work] 😀\\ud800x\\udfff\n");
    }
}
