using System.Globalization;

namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class BindingOutputTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task QuarantinesValidationOutputUntilTheCompleteRedactorIsKnown(bool accepted)
    {
        const string secret = "late-bound-secret";
        TextWriter originalOutput = Console.Out;
        TextWriter originalError = Console.Error;
        StringWriter host = new(CultureInfo.InvariantCulture);
        StringWriter output = new(CultureInfo.InvariantCulture);
        StringWriter error = new(CultureInfo.InvariantCulture);
        try
        {
            Console.SetOut(host);
            Console.SetError(host);
            Command command = PhaseFiveTestSupport.CreateCommand();
            PhaseFiveTestSupport.ConfigureServices(command, output, error);
            _ = command.Option<string>("probe").Description("Write during validation.").Default("probe")
                .Validate(_ =>
                {
                    Console.WriteLine(secret);
                    Console.Error.WriteLine(secret);
                    output.ToString().Should().BeEmpty();
                    error.ToString().Should().BeEmpty();
                    return accepted;
                }, "Probe rejected.");
            _ = command.RequiredOption<string>("secret").Description("Bind a later secret.").Sensitive();
            Target entry = command.Target("entry").Description("Complete binding.");

            int exitCode = await command.RunAsync(entry, ["--secret", secret], TestContext.Current.CancellationToken);

            exitCode.Should().Be(accepted ? 0 : 2);
            host.ToString().Should().BeEmpty();
            (output.ToString() + error).Should().NotContain(secret);
            if (accepted)
            {
                output.ToString().Should().Be("[command] <redacted>\n\nCommand succeeded\n  [entry] No work\n");
                error.ToString().Should().Be("[command] <redacted>\n");
            }
            else
            {
                output.ToString().Should().BeEmpty();
                error.ToString().Should().NotContain("[command]");
            }
        }
        finally
        {
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
        }
    }

    [Theory]
    [InlineData(1_048_575, 0)]
    [InlineData(1_048_576, 0)]
    [InlineData(1_048_577, 1)]
    public async Task EnforcesThePerStreamQuarantineLimitBeforeTargetExecution(int length, int expectedExit)
    {
        StringWriter output = new(CultureInfo.InvariantCulture);
        StringWriter error = new(CultureInfo.InvariantCulture);
        Command command = PhaseFiveTestSupport.CreateCommand();
        PhaseFiveTestSupport.ConfigureServices(command, output, error);
        bool executed = false;
        _ = command.Option<string>("probe").Description("Write during validation.").Default("probe")
            .Validate(_ =>
            {
                Console.Write(new string('a', length));
                Console.Error.Write(new string('b', length));
                return true;
            }, "Unused.");
        Target entry = command.Target("entry").Description("Run only after safe binding.")
            .Run(() => executed = true);

        int exitCode = await command.RunAsync(entry, [], TestContext.Current.CancellationToken);

        exitCode.Should().Be(expectedExit);
        executed.Should().Be(expectedExit == 0);
        if (expectedExit != 0)
        {
            output.ToString().Should().BeEmpty();
            error.ToString().Should().NotContain(new string('b', 100));
            command.LastOutputFailure.Should().BeOfType<InvalidOperationException>();
        }
    }
}
