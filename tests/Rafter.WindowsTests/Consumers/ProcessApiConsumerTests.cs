using AwesomeAssertions;
using Xunit;

namespace Rafter.WindowsTests.Consumers;

[Trait("LongRunning", "true")]
public sealed class ProcessApiConsumerTests
{
    [WindowsTheory]
    [InlineData("file-app", "tests/testassets/consumers/runtime/file-app/probe.cs")]
    [InlineData("project", "tests/testassets/consumers/runtime/project/Consumer.csproj")]
    public async Task Public_process_api_works_in_real_consumers(string kind, string entryPoint)
    {
        using var consumer = new IsolatedConsumer(kind, TestContext.Current.TestOutputHelper!, entryPoint);
        await consumer.BuildAsync();
        var process = await consumer.RunAsync("process", "--quiet");
        process.ExitCode.Should().Be(0);
        process.StandardOutput.Should().Contain("10.0.").And.Contain("Process result: 0:True");
        process.StandardError.Should().BeEmpty();
    }
}
