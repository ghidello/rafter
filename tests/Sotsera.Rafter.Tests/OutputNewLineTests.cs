namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class OutputNewLineTests
{
    [Theory]
    [InlineData("\n", false)]
    [InlineData("\r\n", false)]
    [InlineData("\r", false)]
    [InlineData("|", false)]
    [InlineData("\n", true)]
    [InlineData("\r\n", true)]
    [InlineData("\r", true)]
    [InlineData("|", true)]
    public async Task SemanticConsoleAndSummaryOutputUseTheCapturedNewLine(string newLine, bool rich)
    {
        StringWriter output = new();
        StringWriter error = new();
        OutputCapabilities capabilities = (rich
            ? PhaseFiveTestSupport.RichCapabilities with { SupportsColor = false }
            : OutputCapabilities.Plain) with
        { NewLine = newLine };
        Command command = PhaseFiveTestSupport.CreateCommand();
        command.InvocationServicesFactory = () => new InvocationServices(_ => null, output, error,
            capabilities, capabilities with { NewLine = "\n" }, "test");
        Target work = command.Target("work").Description("Newlines.").Run(context =>
        {
            context.Output.Line("first\nsecond");
            Console.WriteLine("console");
            context.Output.Warning("warning");
        });

        (await command.RunAsync(work, [], TestContext.Current.CancellationToken)).Should().Be(0);

        string[] lines = output.ToString().Split(newLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().Contain("[work] first").And.Contain("[work] second").And.Contain("[work] console");
        lines.Should().Contain(line => line.Contains("Command succeeded", StringComparison.Ordinal));
        foreach (string line in lines)
        {
            line.Should().NotContain("\r").And.NotContain("\n");
        }
        error.ToString().Should().EndWith("warning\n").And.NotContain("\r");
        capabilities.Resolve(plain: true, suppressColor: false).NewLine.Should().Be(newLine);
        (capabilities with { IsRedirected = true }).Resolve(false, false).NewLine.Should().Be(newLine);
    }

    [Fact]
    public void ProductionCaptureSnapshotsIndependentWriterNewlines()
    {
        TextWriter originalOutput = Console.Out;
        TextWriter originalError = Console.Error;
        StringWriter output = new() { NewLine = "\r\n" };
        StringWriter error = new() { NewLine = "\r" };
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            InvocationServices captured = InvocationServices.Capture();
            output.NewLine = "changed";
            error.NewLine = "changed";

            captured.StandardOutputCapabilities.NewLine.Should().Be("\r\n");
            captured.StandardErrorCapabilities.NewLine.Should().Be("\r");
        }
        finally
        {
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
        }
    }
}
