using System.Globalization;
using Spectre.Console;

namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class OutputCapabilityTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(" ", false)]
    [InlineData("0", false)]
    [InlineData("1", false)]
    public void NoColorDisablesOnlyColorAndIsReadOnce(string? noColor, bool color)
    {
        List<string> reads = [];
        InvocationServices services = Services(name =>
        {
            reads.Add(name);
            return noColor;
        });

        InvocationServices prepared = services.PreparePresentation(plain: false);

        prepared.StandardOutputCapabilities.Should().Be(services.StandardOutputCapabilities with
        {
            SupportsColor = color,
        });
        prepared.StandardErrorCapabilities.Should().Be(prepared.StandardOutputCapabilities);
        prepared.ReadEnvironment("NO_COLOR").Should().Be(noColor);
        reads.Should().Equal("NO_COLOR");
        OutputEvent success = new("work", OutputKind.Success, "done");
        string rendered = OutputPresentation.Render(success, prepared.StandardOutputCapabilities);
        rendered.Contains('\u001B').Should().Be(color);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task SelectsStdoutAndStderrIndependently(bool outputRedirected, bool errorRedirected)
    {
        StringWriter output = new(CultureInfo.InvariantCulture);
        StringWriter error = new(CultureInfo.InvariantCulture);
        Command command = PhaseFiveTestSupport.CreateCommand();
        command.InvocationServicesFactory = () => Services(_ => null) with
        {
            StandardOutput = output,
            StandardError = error,
            StandardOutputCapabilities = PhaseFiveTestSupport.RichCapabilities with { IsRedirected = outputRedirected },
            StandardErrorCapabilities = PhaseFiveTestSupport.RichCapabilities with { IsRedirected = errorRedirected },
        };
        Target target = command.Target("work").Description("Write both streams.").Run(context =>
        {
            context.Output.Success("done");
            context.Output.Warning("notice");
        });

        int code = await command.RunAsync(target, [], TestContext.Current.CancellationToken);

        code.Should().Be(0);
        output.ToString().Contains('\u001B').Should().Be(!outputRedirected);
        error.ToString().Contains('\u001B').Should().Be(!errorRedirected);
        output.ToString().Should().Contain("done").And.NotContain("notice");
        error.ToString().Should().Contain("notice").And.NotContain("done");
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(0, false)]
    [InlineData(19, false)]
    [InlineData(20, true)]
    [InlineData(4096, true)]
    [InlineData(4097, false)]
    public void RejectsUnknownAndOutOfRangeWidths(int? width, bool rich)
    {
        OutputCapabilities capabilities = PhaseFiveTestSupport.RichCapabilities with { Width = width };

        OutputCapabilities resolved = capabilities.Resolve(plain: false, suppressColor: false);

        resolved.IsRich.Should().Be(rich);
        if (!rich)
        {
            resolved.Should().Be(OutputCapabilities.Plain);
        }
    }

    [Fact]
    public void CosmeticProbeFailureDowngradesOnlyTheAffectedStream()
    {
        InvocationServices services = Services(_ => null) with
        {
            StandardOutputCapabilities = OutputCapabilities.Capture(() => throw new IOException("probe failed")),
            StandardErrorCapabilities = OutputCapabilities.Capture(() => PhaseFiveTestSupport.RichCapabilities),
        };

        InvocationServices prepared = services.PreparePresentation(plain: false);

        prepared.StandardOutputCapabilities.Should().Be(OutputCapabilities.Plain);
        prepared.StandardErrorCapabilities.Should().Be(PhaseFiveTestSupport.RichCapabilities);
    }

    [Fact]
    public void NoColorLookupFailureDisablesColorButPreservesBindingFailure()
    {
        IOException failure = new("lookup failed");
        int reads = 0;
        InvocationServices services = Services(_ =>
        {
            reads++;
            throw failure;
        }).PreparePresentation(plain: false);

        services.StandardOutputCapabilities.Should().Be(PhaseFiveTestSupport.RichCapabilities with
        {
            SupportsColor = false,
        });
        Action read = () => services.ReadEnvironment("NO_COLOR");
        read.Should().Throw<IOException>().Which.Should().BeSameAs(failure);
        reads.Should().Be(1);
    }

    [Theory]
    [InlineData("model")]
    [InlineData("graph")]
    [InlineData("cancel")]
    [InlineData("input")]
    public async Task ExactPlainAppliesBeforeEarlyReports(string failure)
    {
        Command command = PhaseFiveTestSupport.CreateCommand();
        StringWriter error = new(CultureInfo.InvariantCulture);
        command.InvocationServicesFactory = () => Services(_ => null) with { StandardError = error };
        Target target = command.Target("work");
        if (failure is not "model")
        {
            target.Description("Work.");
        }

        if (failure is "graph")
        {
            target.DependsOn(target);
        }

        using CancellationTokenSource cancellation = new();
        if (failure is "cancel")
        {
            cancellation.Cancel();
        }

        string[] arguments = failure is "input" ? ["--plain", "--plain"] : ["--plain"];
        int code = await command.RunAsync(target, arguments, cancellation.Token);

        code.Should().Be(failure is "cancel" ? 130 : 2);
        error.ToString().Should().NotBeEmpty().And.NotContain("\u001B");
    }

    [Fact]
    public async Task MalformedPlainDoesNotSelectPlainPresentation()
    {
        Command command = PhaseFiveTestSupport.CreateCommand();
        StringWriter error = new(CultureInfo.InvariantCulture);
        command.InvocationServicesFactory = () => Services(_ => null) with { StandardError = error };
        Target target = command.Target("work").Description("Work.");

        int code = await command.RunAsync(target, ["--plain=true"], TestContext.Current.CancellationToken);

        code.Should().Be(2);
        error.ToString().Should().Contain("\u001B[").And.Contain("Malformed common option");
    }

    [Fact]
    public async Task NoColorIsSharedWithOptionBindingAndRefreshedForEachInvocation()
    {
        int reads = 0;
        Command command = PhaseFiveTestSupport.CreateCommand();
        RequiredOption<string> value = command.RequiredOption<string>("value").Description("Value.")
            .FromEnvironment("NO_COLOR");
        command.InvocationServicesFactory = () => Services(name =>
        {
            name.Should().Be("NO_COLOR");
            reads++;
            return reads.ToString(CultureInfo.InvariantCulture);
        });
        List<string> values = [];
        Target target = command.Target("work").Description("Read value.")
            .Run(context => values.Add(context.Value(value)));

        (await command.RunAsync(target, [], TestContext.Current.CancellationToken)).Should().Be(0);
        (await command.RunAsync(target, [], TestContext.Current.CancellationToken)).Should().Be(0);

        values.Should().Equal("1", "2");
        reads.Should().Be(2);
    }

    [Fact]
    public void RenderingUsesInjectedWidthAndIndependentUnicodeAndColor()
    {
        OutputCapabilities capabilities = PhaseFiveTestSupport.RichCapabilities with
        {
            Width = 37,
            SupportsUnicode = false,
            SupportsCursor = false,
        };
        IAnsiConsole console = OutputPresentation.CreateConsole(new StringWriter(), capabilities);

        console.Profile.Width.Should().Be(37);
        console.Profile.Capabilities.Unicode.Should().BeFalse();
        console.Profile.Capabilities.ColorSystem.Should().Be(ColorSystem.Standard);
        console.Profile.Capabilities.Interactive.Should().BeFalse();
        (capabilities with { SupportsStaticLayout = false }).Resolve(false, false)
            .Should().Be(OutputCapabilities.Plain);
        capabilities.Resolve(plain: true, suppressColor: false).Should().Be(OutputCapabilities.Plain);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DistinguishesWriterCaptureFailureFromCosmeticEnvironmentFailure(bool writerCaptureFails)
    {
        StringWriter output = new(CultureInfo.InvariantCulture);
        Command command = PhaseFiveTestSupport.CreateCommand();
        command.InvocationServicesFactory = () => writerCaptureFails
            ? throw new IOException("writer capture failed")
            : Services(_ => throw new IOException("cosmetic lookup failed")) with { StandardOutput = output };
        bool ran = false;
        Target target = command.Target("work").Description("Work.").Run(context =>
        {
            ran = true;
            context.Output.Success("done");
        });

        int code = await command.RunAsync(target, [], TestContext.Current.CancellationToken);

        code.Should().Be(writerCaptureFails ? 1 : 0);
        ran.Should().Be(!writerCaptureFails);
        output.ToString().Should().NotContain("\u001B");
        command.LastInvocationStatus.Should().Be(writerCaptureFails
            ? Command.InvocationStatus.InfrastructureFailure
            : Command.InvocationStatus.Success);
    }

    private static InvocationServices Services(Func<string, string?> readEnvironment)
        => new(readEnvironment, TextWriter.Null, TextWriter.Null,
            PhaseFiveTestSupport.RichCapabilities, PhaseFiveTestSupport.RichCapabilities, "test-command");
}
