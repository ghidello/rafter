using AwesomeAssertions;
using Xunit;

namespace Rafter.Evaluation.Tests;

public sealed class RuntimeConsumerTests
{
    [Theory]
    [InlineData("file-app", "fixtures/runtime/file-app/probe.cs")]
    [InlineData("project", "fixtures/runtime/project/Consumer.csproj")]
    public async Task Public_api_works_in_real_consumers(string kind, string entryPoint)
    {
        using var consumer = new IsolatedConsumer(kind, TestContext.Current.TestOutputHelper!, entryPoint);
        await consumer.BuildAsync();
        var sync = await consumer.RunAsync("sync", "--plain");
        sync.ExitCode.Should().Be(0);
        sync.StandardOutput.Should().Be("  Hello [red]literal[/].\n\n界面 cafe\u0301" + Environment.NewLine);
        sync.StandardError.Should().Contain("[OK] Completed").And.NotContain("\x1b");

        var asynchronous = await consumer.RunAsync("async", "--quiet");
        asynchronous.ExitCode.Should().Be(0);
        asynchronous.StandardOutput.Should().Be("Async body finished." + Environment.NewLine);
        asynchronous.StandardError.Should().BeEmpty();

        var help = await consumer.RunAsync("failure", "--help");
        help.ExitCode.Should().Be(0);
        help.StandardError.Should().BeEmpty();
        help.StandardOutput.Should().Contain("Execution").And.NotContain("Before failure");

        var invalid = await consumer.RunAsync("invalid", "--help");
        invalid.ExitCode.Should().Be(2);
        invalid.StandardOutput.Should().BeEmpty();
        invalid.StandardError.Should().Contain("Invalid command");

        var failure = await consumer.RunAsync("failure", "--quiet");
        failure.ExitCode.Should().Be(1);
        failure.StandardOutput.Should().Be("Before failure." + Environment.NewLine);
        failure.StandardError.Should().Contain("InvalidOperationException").And.Contain("ArgumentException")
            .And.Contain("Inner [red]literal[/] cause.").And.Contain("Scenario.cs:line")
            .And.Contain("[Failed]");
    }

    [Fact]
    public async Task Actual_minimal_example_runs_from_an_unrelated_directory()
    {
        using var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!, "examples/minimal.cs");
        await consumer.BuildAsync();
        var run = await consumer.RunAsync("--quiet");
        run.ExitCode.Should().Be(0);
        run.StandardOutput.Should().Be("Hello from Rafter." + Environment.NewLine);
        run.StandardError.Should().BeEmpty();
    }

    [Theory]
    [InlineData("var command = Rafter.Command();", "CS7036")]
    [InlineData("await Rafter.Command(\"description\").RunAsync(args);", "CS1501")]
    public async Task Required_description_and_body_are_compiler_enforced(string invalidSource, string diagnostic)
    {
        using var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!, "examples/minimal.cs");
        var source = "#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj\n#:property PublishAot=false\n"
            + "using Sotsera.Rafter;\n" + invalidSource;
        await File.WriteAllTextAsync(Path.Combine(consumer.WorkingDirectory, "examples/minimal.cs"), source,
            TestContext.Current.CancellationToken);
        var build = await consumer.CompileAsync();
        build.ExitCode.Should().NotBe(0);
        (build.StandardOutput + build.StandardError).Should().Contain(diagnostic);
    }
}
