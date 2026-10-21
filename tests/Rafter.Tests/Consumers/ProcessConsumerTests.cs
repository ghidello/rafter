using AwesomeAssertions;
using Xunit;

namespace Rafter.Tests.Consumers;

public sealed class ProcessConsumerTests
{
    [Fact]
    public async Task Actual_process_example_runs_from_an_isolated_directory()
    {
        using var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!, "examples/processes.cs");
        await consumer.BuildAsync();
        var run = await consumer.RunAsync("--quiet");
        run.ExitCode.Should().Be(0);
        run.StandardOutput.Should().Contain("10.0.").And.EndWith("SDK inspection completed." + Environment.NewLine);
        run.StandardError.Should().BeEmpty();
        var help = await consumer.RunAsync("--help");
        help.ExitCode.Should().Be(0);
        help.StandardOutput.Should().Contain("Display the installed .NET SDK version.")
            .And.NotContain("SDK inspection completed.");
        help.StandardError.Should().BeEmpty();
    }

    [Fact]
    public async Task Working_directory_example_builds_the_child_project()
    {
        using var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!,
            "examples/process-working-directory.cs");
        await consumer.BuildAsync();
        var run = await consumer.RunFromAsync("tests/testassets/workflows", "--quiet");
        run.ExitCode.Should().Be(0, run.StandardError);
        run.StandardOutput.Should().Contain("Build succeeded.").And.Contain("Build completed.");
        File.Exists(Path.Combine(consumer.WorkingDirectory,
            "tests/testassets/workflows/src/bin/Debug/net10.0/BuildSample.dll")).Should().BeTrue();
    }

    [Fact]
    public async Task Failed_build_example_returns_the_unsuccessful_result_and_preserves_invocation_failure()
    {
        using var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!,
            "examples/process-failure.cs");
        await consumer.BuildAsync();
        var success = await consumer.RunAsync("--project", "tests/testassets/workflows/src/BuildSample.csproj", "--quiet");
        success.ExitCode.Should().Be(0, success.StandardError);
        success.StandardOutput.Should().Contain("Build completed.");
        await File.AppendAllTextAsync(Path.Combine(consumer.WorkingDirectory, "tests/testassets/workflows/src/Greeting.cs"),
            "\n#error Intentional fixture build failure\n", TestContext.Current.CancellationToken);
        var failure = await consumer.RunAsync("--project", "tests/testassets/workflows/src/BuildSample.csproj", "--quiet");
        failure.ExitCode.Should().Be(1);
        failure.StandardOutput.Should().Contain("Build exited with code 1.").And.NotContain("Build completed.");
        failure.StandardError.Should().Contain("Action: Fail").And.Contain("[Failed]");
    }

    [Fact]
    public async Task Dependency_example_builds_and_tests_and_stops_at_the_failing_target()
    {
        using var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!,
            "examples/dependencies.cs");
        await consumer.BuildAsync();
        var success = await consumer.RunAsync("--project", "tests/testassets/workflows/sample-tests/BuildSample.Tests.csproj");
        success.ExitCode.Should().Be(0, success.StandardOutput + success.StandardError);
        success.StandardOutput.Should().Contain("succeeded: 1");
        success.StandardError.Should().Contain("[build]").And.Contain("[test] <-- (build)")
            .And.Contain("2 target bodies completed");

        var testPath = Path.Combine(consumer.WorkingDirectory, "tests/testassets/workflows/sample-tests/GreetingTests.cs");
        var source = await File.ReadAllTextAsync(testPath, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(testPath, source.Replace("Hello from the build fixture.", "Wrong expectation."),
            TestContext.Current.CancellationToken);
        var failedTest = await consumer.RunAsync("--project", "tests/testassets/workflows/sample-tests/BuildSample.Tests.csproj");
        failedTest.ExitCode.Should().Be(1);
        failedTest.StandardOutput.Should().Contain("failed: 1");
        failedTest.StandardError.Should().Contain("stopped in test");

        await File.AppendAllTextAsync(Path.Combine(consumer.WorkingDirectory, "tests/testassets/workflows/src/Greeting.cs"),
            "\n#error Intentional fixture build failure\n", TestContext.Current.CancellationToken);
        var failedBuild = await consumer.RunAsync("--project", "tests/testassets/workflows/sample-tests/BuildSample.Tests.csproj");
        failedBuild.ExitCode.Should().Be(1);
        failedBuild.StandardError.Should().Contain("stopped in build").And.NotContain("[test]");
    }
}
