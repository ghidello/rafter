using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Rafter.Evaluation;
using Spectre.Console;
using Xunit;

namespace Rafter.Tests.Evaluation;

public sealed class RendererTests
{
    [Fact]
    public void Help_wrapping_counts_combining_marks_and_wide_characters_as_terminal_cells()
    {
        using var writer = new StringWriter();
        var destination = new RendererDestination(writer, new(false, false), 48);
        destination.Framework(new string('a', 45) + "e\u0301界x", Style.Plain);

        writer.ToString().Should().Be(new string('a', 45) + "e\u0301界" + Environment.NewLine + "x"
            + Environment.NewLine);
    }

    [Theory]
    [InlineData("options", 100)]
    [InlineData("options", 48)]
    [InlineData("validation", 100)]
    [InlineData("validation", 48)]
    [InlineData("dependencies", 100)]
    [InlineData("dependencies", 48)]
    public async Task Help_preserves_all_content_in_plain_and_styled_layouts(string scenario, int width)
    {
        using var plain = new StringWriter();
        using var rich = new StringWriter();
        var plainDestination = new RendererDestination(plain, new(false, false), width);
        var richDestination = new RendererDestination(rich, new(true, false), width);

        HelpSamples.Write(plainDestination, scenario, adversarial: true);
        HelpSamples.Write(richDestination, scenario, adversarial: true);
        await Task.WhenAll(plainDestination.CompleteAsync(), richDestination.CompleteAsync());

        var text = plain.ToString();
        text.Should().NotContain("\u001b");
        StripStyles(rich.ToString()).Should().Be(text);
        Compact(text).Should().Be(Compact(string.Join('\n', HelpSamples.Lines(scenario, adversarial: true))));
        text.Should().Contain("[red]brackets[/]").And.Contain("cafe\u0301").And.Contain("界面").And.Contain("👩‍💻");
        text.Should().Contain("Command options").And.Contain("Execution").And.Contain("Rafter options");
        text.Should().Contain(Environment.NewLine + Environment.NewLine);
        text.Split(Environment.NewLine).Count(line => line.Length == 0).Should()
            .Be(HelpSamples.Lines(scenario, adversarial: true).Count(line => line.Length == 0) + 1);
        if (scenario == "options")
        {
            text.Should().Contain("--name, -n, --recipient <string>");
            if (width == 48)
            {
                text.Should().Contain(Environment.NewLine + "    allowed." + Environment.NewLine);
            }
        }

        // ASCII lines give an independent width check; Unicode preservation is checked above and in terminal evidence.
        foreach (var line in text.Split(Environment.NewLine))
        {
            if (line.All(character => character < 128))
            {
                line.Length.Should().BeLessThanOrEqualTo(width);
            }
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CI_environment_cannot_override_the_destination_policy(bool rich, bool noColor)
    {
        using var writer = new StringWriter();
        var destination = new RendererDestination(writer, new(rich, noColor), environment: new()
        {
            ["CI"] = "true", ["GITHUB_ACTIONS"] = "true", ["TERM"] = "xterm-256color"
        });
        destination.Framework("Cancellation requested.", new Style(Color.Yellow, decoration: Decoration.Bold));
        destination.Literal("[build]\n", "  | ");
        await destination.CompleteAsync();

        var text = writer.ToString();
        if (!rich)
        {
            text.Should().Be("Cancellation requested." + Environment.NewLine + "[build]\n");
        }
        else
        {
            text.Should().Contain("\u001b[");
            StripStyles(text).Should().Contain("  | [build]\n");
            if (noColor)
            {
                text.Should().NotMatchRegex("\\x1b\\[[^m]*(?:38;|48;|3[0-7]m|9[0-7]m)");
                text.Should().Contain("\u001b[1m");
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Literal_chunks_preserve_indent_blank_lines_CR_and_partial_final_lines(bool rich)
    {
        const string original = "  [red]literal[/]\r\n\n    indented\rprogress\n";
        var longLine = new string('x', 170) + "界e\u0301";
        using var writer = new StringWriter();
        var destination = new RendererDestination(writer, new(rich, false), 48);
        destination.Literal(original);
        destination.Literal(longLine[..60]);
        destination.Literal(longLine[60..]);
        await destination.CompleteAsync();

        var actual = writer.ToString();
        if (rich)
        {
            actual.Should().Contain("\u001b[0m  [red]literal[/]");
            actual = StripStyles(actual);
            actual = Regex.Replace(actual, "(?m)^  > ", "");
        }

        actual.Should().Be(original + longLine);
        actual.Should().NotEndWith("\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Full_nested_exception_text_bypasses_layout_even_at_narrow_width(bool rich)
    {
        Exception captured;
        try
        {
            try
            {
                throw new InvalidOperationException("inner [red] details " + new string('x', 140));
            }
            catch (Exception inner)
            {
                throw new ApplicationException("outer failure", inner);
            }
        }
        catch (Exception exception)
        {
            captured = exception;
        }

        using var writer = new StringWriter();
        var destination = new RendererDestination(writer, new(rich, false), 48);
        destination.Literal(captured.ToString(), "  ");
        await destination.CompleteAsync();

        var actual = StripStyles(writer.ToString());
        if (rich)
        {
            actual = Regex.Replace(actual, "(?m)^  ", "");
        }
        actual.Should().Be(captured.ToString());
    }

    [Fact]
    public async Task Child_ANSI_is_not_silently_sanitized_by_plain_output()
    {
        const string child = "\u001b[31mchild color\u001b[0m\rprogress\n";
        using var writer = new StringWriter();
        var destination = new RendererDestination(writer, new(false, false));
        destination.Literal(child);
        await destination.CompleteAsync();

        writer.ToString().Should().Be(child);
    }

    [Fact]
    public async Task Concurrent_layout_and_literal_writes_keep_each_destination_record_intact()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var output = new RendererDestination(stdout, new(true, false));
        var error = new RendererDestination(stderr, new(true, false));
        var tasks = new List<Task>();
        for (var index = 0; index < 50; index++)
        {
            var id = index;
            tasks.Add(Task.Run(() => output.Literal($"out-{id}\n"), TestContext.Current.CancellationToken));
            tasks.Add(Task.Run(() => error.Literal($"err-{id}\n"), TestContext.Current.CancellationToken));
            tasks.Add(Task.Run(() => error.Framework($"status-{id}", new Style(Color.Yellow)),
                TestContext.Current.CancellationToken));
        }
        await Task.WhenAll(tasks);
        await Task.WhenAll(output.CompleteAsync(), error.CompleteAsync());

        var outputLines = StripStyles(stdout.ToString()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        outputLines.Should().BeEquivalentTo(Enumerable.Range(0, 50).Select(index => $"  > out-{index}"));
        var errorLines = StripStyles(stderr.ToString()).Replace("\r", "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        errorLines.Should().BeEquivalentTo(Enumerable.Range(0, 50)
            .SelectMany(index => new[] { $"  > err-{index}", $"status-{index}" }));
        Regex.Matches(stdout.ToString(), "  > \\x1b\\[0mout-").Count.Should().Be(50);
        Regex.Matches(stderr.ToString(), "  > \\x1b\\[0merr-").Count.Should().Be(50);
    }

    [Fact]
    public async Task Completion_waits_for_flush_and_retains_host_writer_ownership()
    {
        using var writer = new ControlledWriter();
        var destination = new RendererDestination(writer, new(false, false));
        destination.Literal("pending");
        var completion = destination.CompleteAsync();
        await writer.FlushStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        completion.IsCompleted.Should().BeFalse();

        writer.ReleaseFlush.SetResult();
        await completion;
        writer.Disposed.Should().BeFalse();
        writer.Write(" host still owns this");
        writer.ToString().Should().Be("pending host still owns this");
        var lateWrite = () => destination.Literal("too late");
        lateWrite.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task A_blocked_output_flush_does_not_block_the_other_destination()
    {
        using var stdout = new ControlledWriter();
        using var stderr = new StringWriter();
        var output = new RendererDestination(stdout, new(false, false));
        var error = new RendererDestination(stderr, new(false, false));
        var flushing = output.CompleteAsync();
        await stdout.FlushStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        error.Framework("Output incomplete", Style.Plain);
        await error.CompleteAsync();
        stderr.ToString().Should().Contain("Output incomplete");
        flushing.IsCompleted.Should().BeFalse();
        stdout.ReleaseFlush.SetResult();
        await flushing;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Write_and_flush_errors_propagate_without_disposing_the_writer(bool layout, bool rich)
    {
        using var writer = new ControlledWriter { FailWrites = true };
        var destination = new RendererDestination(writer, new(rich, false));
        Action write = layout
            ? () => destination.Framework("failure", Style.Plain)
            : () => destination.Literal("failure");
        write.Should().Throw<IOException>();
        writer.FailWrites = false;
        writer.ReleaseFlush.SetException(new IOException("flush failed"));

        var complete = () => destination.CompleteAsync();
        await complete.Should().ThrowAsync<IOException>();
        writer.Disposed.Should().BeFalse();
    }

    [Fact]
    public void Legacy_WriteAnsi_callback_drops_text_but_the_adapter_plain_path_does_not()
    {
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer), Ansi = AnsiSupport.No,
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false }
        });
        console.WriteAnsi(ansi => ansi.Write("lost text"));
        writer.ToString().Should().BeEmpty();

        new RendererDestination(writer, new(false, false)).Framework("retained text", Style.Plain);
        writer.ToString().Should().Be("retained text" + Environment.NewLine);
    }

    [Theory]
    [InlineData("file-app", "tests/testassets/evaluations/renderer/full/probe.cs")]
    [InlineData("project", "tests/testassets/evaluations/renderer/full/project/Consumer.csproj")]
    public async Task Real_consumers_keep_redirected_streams_plain_and_separate(string kind, string entryPoint)
    {
        using var consumer = new IsolatedConsumer(kind, TestContext.Current.TestOutputHelper!, entryPoint);
        await consumer.BuildAsync();
        var record = Path.Combine(consumer.WorkingDirectory, "output-record.json");
        var actual = await consumer.RunAsync("--record", record);
        actual.ExitCode.Should().Be(0);
        actual.StandardOutput.Should().Be("  script [red]literal[/]\n\nchild stdout\rprogress\npartial");
        actual.StandardError.Should().Be("Cancellation requested." + Environment.NewLine + "  child stderr\n");

        using var evidence = JsonDocument.Parse(await File.ReadAllTextAsync(record, TestContext.Current.CancellationToken));
        evidence.RootElement.GetProperty("WritersRetained").GetBoolean().Should().BeTrue();
        evidence.RootElement.GetProperty("OutputRedirected").GetBoolean().Should().BeTrue();
        evidence.RootElement.GetProperty("ErrorRedirected").GetBoolean().Should().BeTrue();

        var help = await consumer.RunAsync("--help-sample", "validation", "--narrow", "--plain");
        help.ExitCode.Should().Be(0);
        help.StandardError.Should().BeEmpty();
        Compact(help.StandardOutput).Should().Be(Compact(string.Join('\n', HelpSamples.Lines("validation"))));
    }

    private static string StripStyles(string text) => Regex.Replace(text, "\\x1b\\[[0-9;]*m", "");
    private static string Compact(string text) => string.Concat(text.Where(character => !char.IsWhiteSpace(character)));

    private sealed class ControlledWriter : StringWriter
    {
        public TaskCompletionSource FlushStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFlush { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool FailWrites { get; set; }
        public bool Disposed { get; private set; }

        public override void Write(string? value)
        {
            if (FailWrites)
            {
                throw new IOException("write failed");
            }
            base.Write(value);
        }

        public override void Write(char value)
        {
            if (FailWrites)
            {
                throw new IOException("write failed");
            }
            base.Write(value);
        }

        public override void Write(ReadOnlySpan<char> value)
        {
            if (FailWrites)
            {
                throw new IOException("write failed");
            }
            base.Write(value);
        }

        public override Task FlushAsync()
        {
            FlushStarted.TrySetResult();
            return ReleaseFlush.Task;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
