using System.Text.Json;
using AwesomeAssertions;
using Rafter.Evaluation;
using Xunit;

namespace Rafter.IntegrationTests.Evaluation;

[Trait("LongRunning", "true")]
public sealed class ParserConsumerTests
{
    [Fact]
    public async Task File_app_executes_the_same_adapter_with_real_argument_boundaries_and_exit_codes()
    {
        using var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!, "tests/testassets/evaluations/parser/probe.cs");
        await consumer.BuildAsync();
        var responseFile = Path.Combine(consumer.WorkingDirectory, "arguments with spaces.rsp");
        await File.WriteAllTextAsync(responseFile, "--name=expanded-from-response-file",
            TestContext.Current.CancellationToken);
        var cases = new (string[] Arguments, int ExitCode, string? Name, bool Help)[]
        {
            (["--name=", "--help"], 0, "", true),
            (["--name", "", "--help"], 0, "", true),
            (["--name", "two words \"caffè\"\\", "--count=-42", "--flag=FALSE"], 0, "two words \"caffè\"\\", false),
            (["--name=--flag:false"], 0, "--flag:false", false),
            (["--help", "--flag=private-rejected-value-9a28"], 2, null, false),
            (["--name", "--help"], 2, null, false),
            (["@" + responseFile], 2, null, false),
            (["--name", "@" + responseFile], 0, "@" + responseFile, false)
        };

        foreach (var item in cases)
        {
            var actual = await consumer.RunAsync(item.Arguments);
            actual.ExitCode.Should().Be(item.ExitCode);
            var outcome = JsonSerializer.Deserialize<CandidateOutcome>(actual.StandardOutput)!;
            outcome.ExitCode.Should().Be(item.ExitCode);
            outcome.Name.Should().Be(item.Name);
            outcome.Help.Should().Be(item.Help);
            if (item.ExitCode == 0)
            {
                actual.StandardError.Should().BeEmpty();
            }
            else
            {
                actual.StandardError.Should().NotBeEmpty().And.NotContain("private-rejected-value-9a28");
                actual.StandardOutput.Should().NotContain("private-rejected-value-9a28");
            }
        }
    }
}
