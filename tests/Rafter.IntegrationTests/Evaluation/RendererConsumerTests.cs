using System.Text.Json;
using AwesomeAssertions;
using Rafter.Evaluation;
using Xunit;

namespace Rafter.IntegrationTests.Evaluation;

[Trait("LongRunning", "true")]
public sealed class RendererConsumerTests
{
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

    private static string Compact(string text) => string.Concat(text.Where(character => !char.IsWhiteSpace(character)));
}
