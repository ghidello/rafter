using System.Globalization;

namespace Sotsera.Rafter.Tests;

internal static class PhaseFiveTestSupport
{
    internal static Command CreateCommand(int? concurrency = null)
    {
        Command command = global::Sotsera.Rafter.Rafter.Command(Root.Invocation).Description("Test command.");
        if (concurrency is not null)
        {
            command.Concurrency(concurrency.Value);
        }

        ConfigureServices(command);
        return command;
    }

    internal static void ConfigureServices(
        Command command,
        StringWriter? output = null,
        StringWriter? error = null,
        Func<string, string?>? environment = null)
    {
        command.InvocationServicesFactory = () => new InvocationServices(
            environment ?? (_ => null),
            output ?? new StringWriter(CultureInfo.InvariantCulture),
            error ?? new StringWriter(CultureInfo.InvariantCulture),
            false,
            false,
            "test-command");
    }
}
