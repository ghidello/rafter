using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace Rafter.Evaluation.Tests;

public sealed class ConsumerTests
{
    [Theory]
    [InlineData("file-app")]
    [InlineData("project")]
    public async Task Consumer_preserves_argument_boundaries_and_working_directory(string kind)
    {
        using var consumer = new IsolatedConsumer(kind, TestContext.Current.TestOutputHelper!);
        await consumer.BuildAsync();

        string[] arguments = ["two words", "", "quote\"inside", "trailing\\", "caffè", "--literal"];
        var result = await consumer.RunAsync(arguments);

        result.ExitCode.Should().Be(0);
        result.StandardError.Should().BeEmpty();

        using var json = JsonDocument.Parse(result.StandardOutput);
        var actualArguments = json.RootElement.GetProperty("Arguments").EnumerateArray()
            .Select(argument => argument.GetString()).ToArray();

        actualArguments.Should().Equal(arguments);
        json.RootElement.GetProperty("WorkingDirectory").GetString().Should().Be(consumer.WorkingDirectory);
        json.RootElement.GetProperty("Framework").GetString().Should().StartWith(".NET 10.");
    }

    [Theory]
    [InlineData("file-app")]
    [InlineData("project")]
    public async Task Consumer_failure_preserves_exit_code_and_separate_output_streams(string kind)
    {
        using var consumer = new IsolatedConsumer(kind, TestContext.Current.TestOutputHelper!);
        await consumer.BuildAsync();

        var result = await consumer.RunAsync("--fail");

        result.ExitCode.Should().Be(23);
        result.StandardOutput.Should().Be("output before failure" + Environment.NewLine);
        result.StandardError.Should().Be("intentional consumer failure" + Environment.NewLine);
    }
}
