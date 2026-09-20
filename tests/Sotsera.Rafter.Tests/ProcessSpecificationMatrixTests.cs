using System.Diagnostics;

namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class ProcessSpecificationMatrixTests
{
    public static IEnumerable<object[]> DiagnosticCases()
    {
        foreach ((string scenario, string code) in new[]
        {
            ("executable", "RAFTER1501"), ("path", "RAFTER1502"), ("argument", "RAFTER1503"),
            ("name", "RAFTER1504"), ("environment-key", "RAFTER1505"), ("environment-value", "RAFTER1506"),
            ("directory", "RAFTER1507"), ("timeout", "RAFTER1508"), ("limit", "RAFTER1509"),
            ("exits", "RAFTER1510"), ("duplicate-timeout", "RAFTER1511"), ("stream-limit", "RAFTER1512"),
            ("unsafe-marker", "RAFTER1513"), ("duplicate-directory", "RAFTER1511"),
            ("duplicate-environment", "RAFTER1511"), ("duplicate-limit", "RAFTER1511"), ("duplicate-exits", "RAFTER1511"),
        })
        {
            yield return [scenario, code, false];
            yield return [scenario, code, true];
        }
    }

    [Theory]
    [MemberData(nameof(DiagnosticCases))]
    public async Task EveryDiagnosticRejectsBeforeCreationAndBeforePreCancellation(string scenario, string code, bool cancel)
    {
        CountingFactory factory = new();
        IProcessAdapterFactory original = ProcessRuntime.AdapterFactory;
        ProcessRuntime.AdapterFactory = factory;
        using CancellationTokenSource cancellation = new();
        try
        {
            Command command = PhaseFiveTestSupport.CreateCommand();
            Target work = command.Target("work").Description("Validate.").Run(context =>
            {
                ProcessBuilder builder = InvalidBuilder(context, scenario);
                if (cancel)
                {
                    cancellation.Cancel();
                }
                return scenario is "stream-limit" or "unsafe-marker" ? builder.Run() : (Task)builder.Capture();
            });

            (await command.RunAsync(work, [], cancellation.Token)).Should().Be(1);

            command.LastExecutionOutcome!.Targets.Single().PrimaryException.Should().BeOfType<ProcessException>()
                .Which.Message.Should().Contain(code);
            factory.Calls.Should().Be(0);
            ProcessOperationReaper.Count.Should().Be(0);
        }
        finally
        {
            ProcessRuntime.AdapterFactory = original;
        }
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(4294967294, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(4294967295, false)]
    public async Task TimeoutEndpointsAreValidatedWithoutEagerExecutableProbing(long milliseconds, bool accepted)
    {
        CountingFactory factory = new();
        IProcessAdapterFactory original = ProcessRuntime.AdapterFactory;
        ProcessRuntime.AdapterFactory = factory;
        try
        {
            Command command = PhaseFiveTestSupport.CreateCommand();
            Target work = command.Target("work").Description("Validate timeout.")
                .Run(context => context.Process("missing-executable-for-validation")
                    .Timeout(TimeSpan.FromMilliseconds(milliseconds)).Capture());

            (await command.RunAsync(work, [], TestContext.Current.CancellationToken)).Should().Be(1);

            factory.Calls.Should().Be(accepted ? 1 : 0);
            Exception failure = command.LastExecutionOutcome!.Targets.Single().PrimaryException!;
            if (accepted)
            {
                failure.Should().BeOfType<ProcessStartException>();
            }
            else
            {
                failure.Should().BeOfType<ProcessException>().Which.Message.Should().Contain("RAFTER1508");
            }
        }
        finally
        {
            ProcessRuntime.AdapterFactory = original;
        }
    }

    private static ProcessBuilder InvalidBuilder(RafterContext context, string scenario)
    {
        ProcessBuilder builder = context.Process("synthetic");
        return scenario switch
        {
            "executable" => context.Process(" "),
            "path" => context.Process("\\\\?\\C:\\tool"),
            "argument" => builder.Argument("\0"),
            "name" => builder.Flag(" "),
            "environment-key" => builder.Environment(environment => environment.Set("=", "value")),
            "environment-value" => builder.Environment(environment => environment.Set("KEY", "\0")),
            "directory" => builder.WorkingDirectory(".."),
            "timeout" => builder.Timeout(TimeSpan.Zero),
            "limit" => builder.CaptureLimitBytes((long)int.MaxValue + 1),
            "exits" => builder.ValidExitCodes(),
            "duplicate-timeout" => builder.Timeout(TimeSpan.FromSeconds(1)).Timeout(TimeSpan.FromSeconds(2)),
            "duplicate-directory" => builder.WorkingDirectory(".").WorkingDirectory("."),
            "duplicate-environment" => builder.Environment(environment => environment.Clear())
                .Environment(environment => environment.Clear()),
            "duplicate-limit" => builder.CaptureLimitBytes(1).CaptureLimitBytes(2),
            "duplicate-exits" => builder.ValidExitCodes(0).ValidExitCodes(1),
            "stream-limit" => builder.CaptureLimitBytes(1),
            "unsafe-marker" => builder.Environment(environment =>
            {
                environment.SetSensitive("SECRET", "<");
                for (int character = 0xE000; character <= 0xF8FF; character++)
                {
                    environment.SetSensitive("SECRET", ((char)character).ToString());
                }
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
    }

    private sealed class CountingFactory : IProcessAdapterFactory
    {
        internal int Calls { get; private set; }

        public IProcessAdapter Create(ProcessStartInfo startInfo)
        {
            Calls++;
            throw new IOException("Synthetic launch failure.");
        }
    }
}
