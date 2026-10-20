using AwesomeAssertions;
using Xunit;

namespace Rafter.Evaluation.Tests;

public sealed class TargetConsumerTests
{
    [Fact]
    public async Task Actual_targets_example_runs_and_generates_help_from_its_graph()
    {
        using var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!, "examples/targets.cs");
        await consumer.BuildAsync();
        var run = await consumer.RunAsync("--name=Ada", "--quiet");
        run.ExitCode.Should().Be(0);
        run.StandardOutput.Should().Be("Hello, Ada!" + Environment.NewLine);
        run.StandardError.Should().BeEmpty();
        var normal = await consumer.RunAsync();
        normal.ExitCode.Should().Be(0);
        normal.StandardOutput.Should().Be("Hello, Rafter!" + Environment.NewLine);
        normal.StandardError.Should().Contain("> verify <-- (prepare, greet)")
            .And.Contain("[greet] <-- (prepare)").And.Contain("[OK] verify | 2 target bodies completed");
        var help = await consumer.RunAsync("--help");
        help.ExitCode.Should().Be(0);
        help.StandardError.Should().BeEmpty();
        help.StandardOutput.Should().Contain("Group; no body.").And.Contain("Body order: prepare, greet.")
            .And.Contain("--name <string>").And.NotContain("Hello, Rafter!");
    }

    [Theory]
    [InlineData("await command.RunAsync(args, new Target[] { target });", "CS1503")]
    [InlineData("await command.RunAsync(args, (Action<RafterContext>)(context => { }), target);", "CS1503")]
    [InlineData("command.Target(\"missing-description\");", "CS7036")]
    [InlineData("target.Run();", "CS1501")]
    public async Task Unsupported_target_shapes_fail_at_compile_time(string expression, string diagnostic)
    {
        using var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!, "examples/minimal.cs");
        var source = """
            #:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj
            #:property PublishAot=false
            using Sotsera.Rafter;
            var command = Rafter.Command("Compile contract.");
            var target = command.Target("target", "Target.").Run(context => { });
            """ + Environment.NewLine + expression;
        await File.WriteAllTextAsync(Path.Combine(consumer.WorkingDirectory, "examples/minimal.cs"), source,
            TestContext.Current.CancellationToken);
        var build = await consumer.CompileAsync();
        build.ExitCode.Should().NotBe(0);
        (build.StandardOutput + build.StandardError).Should().Contain(diagnostic);
    }
}
