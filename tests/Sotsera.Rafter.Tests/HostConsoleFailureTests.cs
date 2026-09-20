using System.Diagnostics.CodeAnalysis;

namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class HostConsoleFailureTests
{
    [Theory]
    [InlineData(false, "write")]
    [InlineData(true, "write")]
    [InlineData(false, "read-newline")]
    [InlineData(true, "read-newline")]
    [InlineData(false, "set-newline")]
    [InlineData(true, "set-newline")]
    public async Task HostFailureMarksOnlyInvocationsUsingTheFailedWriter(bool standardError, string operation)
    {
        TextWriter original = standardError ? Console.Error : Console.Out;
        IOException failure = new("host sink failed");
        ControlledWriter host = new(failure);
        try
        {
            SetHost(standardError, host);
            TextWriter captured = standardError ? Console.Error : Console.Out;
            InvocationOutput affected = CreateOutput(captured);
            InvocationOutput independent = CreateOutput(new StringWriter());
            using ConsoleOutputCoordinator.Lease affectedLease = ConsoleOutputCoordinator.Register(affected);
            using ConsoleOutputCoordinator.Lease independentLease = ConsoleOutputCoordinator.Register(independent);
            host.FailureOperation = operation;
            try
            {
                Func<Task> action = () => WithoutScopeAsync(() => UseHost(standardError, operation));

                (await action.Should().ThrowAsync<IOException>()).Which.Should().BeSameAs(failure);

                affected.Failure.Should().BeOfType<IOException>().Which.InnerException.Should().BeSameAs(failure);
                independent.Failure.Should().BeNull();
            }
            finally
            {
                host.FailureOperation = null;
            }
        }
        finally
        {
            SetHost(standardError, original);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HostWriteFailureDoesNotChangeTargetOrCleanupOutcomes(bool standardError)
    {
        TextWriter original = standardError ? Console.Error : Console.Out;
        IOException failure = new("host sink failed");
        ControlledWriter host = new(failure);
        try
        {
            SetHost(standardError, host);
            TextWriter captured = standardError ? Console.Error : Console.Out;
            Command command = PhaseFiveTestSupport.CreateCommand();
            command.InvocationServicesFactory = () => new InvocationServices(_ => null,
                standardError ? new StringWriter() : captured,
                standardError ? captured : new StringWriter(),
                OutputCapabilities.Plain, OutputCapabilities.Plain, "test-command");
            int cleanupCount = 0;
            Target entry = command.Target("work").Description("Observe host failure.").Run(async () =>
            {
                host.FailureOperation = "write";
                try
                {
                    Func<Task> action = () => WithoutScopeAsync(() => UseHost(standardError, "write"));
                    (await action.Should().ThrowAsync<IOException>().ConfigureAwait(false))
                        .Which.Should().BeSameAs(failure);
                }
                finally
                {
                    host.FailureOperation = null;
                }
            }).Finally(() => cleanupCount++);
            command.Finally(() => cleanupCount++);

            (await command.RunAsync(entry, [], TestContext.Current.CancellationToken)).Should().Be(1);

            command.LastInvocationStatus.Should().Be(Command.InvocationStatus.InfrastructureFailure);
            command.LastOutputFailure.Should().BeOfType<IOException>().Which.InnerException.Should().BeSameAs(failure);
            command.LastExecutionOutcome!.ExitCode.Should().Be(0);
            cleanupCount.Should().Be(2);
            (standardError ? Console.Error : Console.Out).Should().BeSameAs(captured);
        }
        finally
        {
            SetHost(standardError, original);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HostFailurePreservesAnEarlierInvocationFailure(bool standardError)
    {
        TextWriter original = standardError ? Console.Error : Console.Out;
        IOException earlier = new("earlier output failure");
        IOException failure = new("host sink failed");
        ControlledWriter host = new(failure);
        try
        {
            SetHost(standardError, host);
            InvocationOutput output = CreateOutput(standardError ? Console.Error : Console.Out);
            using ConsoleOutputCoordinator.Lease lease = ConsoleOutputCoordinator.Register(output);
            output.Fail(earlier);
            host.FailureOperation = "write";
            try
            {
                Func<Task> action = () => WithoutScopeAsync(() => UseHost(standardError, "write"));
                (await action.Should().ThrowAsync<IOException>()).Which.Should().BeSameAs(failure);
                output.Failure.Should().BeSameAs(earlier);
            }
            finally
            {
                host.FailureOperation = null;
            }
        }
        finally
        {
            SetHost(standardError, original);
        }
    }

    private static InvocationOutput CreateOutput(TextWriter writer)
        => new(writer, new StringWriter(), OutputCapabilities.Plain, OutputCapabilities.Plain, TextRedactor.Empty);

    private static Task WithoutScopeAsync(Action action)
    {
        using (ExecutionContext.SuppressFlow())
        {
            return Task.Run(action, TestContext.Current.CancellationToken);
        }
    }

    private static void SetHost(bool standardError, TextWriter writer)
    {
        if (standardError)
        {
            Console.SetError(writer);
        }
        else
        {
            Console.SetOut(writer);
        }
    }

    private static void UseHost(bool standardError, string operation)
    {
        TextWriter writer = standardError ? Console.Error : Console.Out;
        switch (operation)
        {
            case "write":
                writer.Write("host text");
                break;
            case "read-newline":
                _ = writer.NewLine;
                break;
            case "set-newline":
                writer.NewLine = "\n";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    private sealed class ControlledWriter(Exception failure) : StringWriter
    {
        internal string? FailureOperation { get; set; }

        [AllowNull]
        public override string NewLine
        {
            get => FailureOperation is "read-newline" ? throw failure : base.NewLine;
            set
            {
                if (FailureOperation is "set-newline")
                {
                    throw failure;
                }
                base.NewLine = value;
            }
        }

        public override void Write(string? value)
        {
            if (FailureOperation is "write")
            {
                throw failure;
            }
            base.Write(value);
        }
    }
}
