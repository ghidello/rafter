using System.Collections.Immutable;
using System.Diagnostics;

namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class ProcessHandleAndPathTests
{
    [Fact]
    public async Task EveryHandleFamilyRejectsForeignOwnershipEvenForAbsentValues()
    {
        Command foreign = PhaseFiveTestSupport.CreateCommand();
        Option<string> text = foreign.Option<string>("text").Description("Text.");
        RequiredOption<string> required = foreign.RequiredOption<string>("required").Description("Required.");
        DefaultedOption<string> defaulted = foreign.Option<string>("defaulted").Description("Defaulted.").Default("tool");
        Option<bool> flag = foreign.Option<bool>("flag").Description("Flag.");
        Option<TimeSpan> timeout = foreign.Option<TimeSpan>("timeout").Description("Timeout.");
        Option<long> limit = foreign.Option<long>("limit").Description("Limit.");
        int rejected = 0;
        Command command = PhaseFiveTestSupport.CreateCommand();
        Target work = command.Target("work").Description("Reject foreign handles.").Run(context =>
        {
            ProcessBuilder builder = context.Process("synthetic");
            Action[] uses =
            [
                () => context.Process(required), () => context.Process(defaulted),
                () => builder.Argument(text), () => builder.Argument(required), () => builder.Argument(defaulted),
                () => builder.Option("name", text), () => builder.Option("name", required),
                () => builder.Option("name", defaulted), () => builder.Flag("name", flag),
                () => builder.WorkingDirectory(text), () => builder.WorkingDirectory(required),
                () => builder.WorkingDirectory(defaulted), () => builder.Timeout(timeout),
                () => builder.CaptureLimitBytes(limit), () => builder.Environment(environment => environment.Set("KEY", text)),
                () => builder.Environment(environment => environment.SetSensitive("KEY", required)),
            ];
            foreach (Action use in uses)
            {
                use.Should().Throw<InvalidOperationException>();
                rejected++;
            }
        });

        (await command.RunAsync(work, [], TestContext.Current.CancellationToken)).Should().Be(0);
        rejected.Should().Be(16);
    }

    [Theory]
    [InlineData("bare", false)]
    [InlineData("relative", false)]
    [InlineData("parent", false)]
    [InlineData("absolute", false)]
    [InlineData("bare", true)]
    [InlineData("relative", true)]
    [InlineData("parent", true)]
    [InlineData("absolute", true)]
    public async Task ResolvesExecutableAtTheTerminalBoundaryWithoutProbingAvailability(string kind, bool overrideDirectory)
    {
        RecordingFactory factory = new();
        IProcessAdapterFactory original = ProcessRuntime.AdapterFactory;
        ProcessRuntime.AdapterFactory = factory;
        try
        {
            string? expectedExecutable = null;
            string? expectedDirectory = null;
            Command command = PhaseFiveTestSupport.CreateCommand();
            Target work = command.Target("work").Description("Resolve.").Run(context =>
            {
                expectedDirectory = overrideDirectory ? Path.Combine(context.WorkingDirectory, "missing directory")
                    : context.WorkingDirectory;
                string executable = kind switch
                {
                    "bare" => "missing-tool",
                    "relative" => Path.Combine(".", "missing-tool"),
                    "parent" => Path.Combine("..", "missing-tool"),
                    _ => Path.GetFullPath(Path.Combine(context.Root, "..", "external-tool")),
                };
                expectedExecutable = kind is "bare" ? executable : Path.GetFullPath(executable, expectedDirectory);
                ProcessBuilder builder = context.Process(executable);
                if (overrideDirectory)
                {
                    builder = builder.WorkingDirectory("missing directory");
                }
                return builder.Capture();
            });

            (await command.RunAsync(work, [], TestContext.Current.CancellationToken)).Should().Be(1);

            command.LastExecutionOutcome!.Targets.Single().PrimaryException.Should().BeOfType<ProcessStartException>();
            factory.Executable.Should().Be(expectedExecutable);
            factory.Directory.Should().Be(expectedDirectory);
        }
        finally
        {
            ProcessRuntime.AdapterFactory = original;
        }
    }

    [Fact]
    public async Task AbsentAndInactiveInputsDoNotEmitTokensOrConsumeSingletonPolicies()
    {
        RecordingFactory factory = new();
        IProcessAdapterFactory original = ProcessRuntime.AdapterFactory;
        ProcessRuntime.AdapterFactory = factory;
        try
        {
            Command command = PhaseFiveTestSupport.CreateCommand();
            Option<string> text = command.Option<string>("text").Description("Text.");
            DefaultedOption<bool> flag = command.Option<bool>("flag").Description("Flag.").Default(false);
            Target work = command.Target("work").Description("Omit.").Run(context => context.Process("synthetic")
                .Argument(text).Option("\0", text).Flag("\0", false).Flag("\0", flag)
                .Timeout(TimeSpan.FromSeconds(1)).CaptureLimitBytes(1)
                .WorkingDirectory(text).WorkingDirectory(".")
                .Environment(environment => environment.Set("=", text)).Capture());

            command.FreezeResult.Diagnostics.Should().BeEmpty();
            (await command.RunAsync(work, [], TestContext.Current.CancellationToken)).Should().Be(1);
            command.LastExecutionOutcome!.Targets.Single().PrimaryException.Should().BeOfType<ProcessStartException>();
            factory.Arguments.Should().BeEmpty();
            factory.Executable.Should().Be("synthetic");
        }
        finally
        {
            ProcessRuntime.AdapterFactory = original;
        }
    }

    private sealed class RecordingFactory : IProcessAdapterFactory
    {
        internal string? Executable { get; private set; }

        internal string? Directory { get; private set; }

        internal ImmutableArray<string> Arguments { get; private set; } = [];

        public IProcessAdapter Create(ProcessStartInfo startInfo)
        {
            Executable = startInfo.FileName;
            Directory = startInfo.WorkingDirectory;
            Arguments = [.. startInfo.ArgumentList];
            throw new IOException("Synthetic operational failure.");
        }
    }
}
