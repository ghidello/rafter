using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.UnitTests.Processes;

public sealed class ProcessConfigurationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Invalid_configuration_is_catchable_before_launch(int kind)
    {
        using var host = new ProcessHost();
        (await host.Command.RunAsync(["--quiet"], context =>
        {
            Action configure = kind switch
            {
                0 => () => context.Process(" "),
                1 => () => context.Process("dotnet").Arguments("nul\0token"),
                _ => () => context.Process("dotnet").OnFailure(_ => ProcessFailureAction.Fail)
                    .OnFailure(_ => ProcessFailureAction.Continue)
            };
            configure.Should().Throw<Exception>();
        })).Should().Be(0);
        host.Error.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public async Task Invalid_or_repeated_configuration_is_rejected_before_launch(int kind)
    {
        using var host = new ProcessHost();
        (await host.Command.RunAsync(["--quiet"], context =>
        {
            var process = context.Process("dotnet");
            Action configure = kind switch
            {
                0 => () => process.AcceptExitCodes(null!),
                1 => () => process.AcceptExitCodes(),
                2 => () => process.AcceptExitCodes(0).AcceptExitCodes(0),
                3 => () => process.WorkingDirectory(".").WorkingDirectory("."),
                4 => () => process.Environment("KEY", "value").Environment("KEY", "value"),
                5 => () => process.Environment("KEY", "value").Environment("key", "different"),
                6 => () => process.Environment("BAD=NAME", "value"),
                7 => () => process.Environment("KEY", null!),
                8 => () => process.Environment("KEY", "bad\0value"),
                _ => () => process.WorkingDirectory(" ")
            };
            configure.Should().Throw<Exception>();
        })).Should().Be(0);
        host.Error.ToString().Should().BeEmpty();
    }

    private sealed class ProcessHost : IDisposable
    {
        private Action? _signal;
        public StringWriter Output { get; }
        public StringWriter Error { get; } = new();
        public Command Command { get; }

        public ProcessHost(StringWriter? output = null, string? root = null)
        {
            Output = output ?? new StringWriter();
            Command = new("Process workflow.", () => new InvocationHost(root ?? Directory.GetCurrentDirectory(), Output, Error,
                true, true, false, 100, signal =>
                {
                    _signal = signal;
                    return new Subscription(() => _signal = null);
                }));
        }

        public void Signal() => _signal!();

        public void Dispose()
        {
            TestContext.Current.TestOutputHelper?.WriteLine("Rafter stdout:\n{0}\nRafter stderr:\n{1}", Output, Error);
            Output.Dispose();
            Error.Dispose();
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }

}
