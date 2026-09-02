using static Sotsera.Rafter.CommandModel;

namespace Sotsera.Rafter;

internal static class OptionMutation
{
    internal static void Description(AuthoredOption option, string description)
    {
        ArgumentNullException.ThrowIfNull(description);
        long sequence = Begin(option);
        option.Command.Set(option.Description, description, sequence, "RAFTER1113", "The option description");
    }

    internal static void Alias(AuthoredOption option, char alias)
    {
        long sequence = Begin(option);
        option.Command.Set(option.Alias, alias, sequence, "RAFTER1114", "The option alias");
    }

    internal static void FromEnvironment(AuthoredOption option, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        long sequence = Begin(option);
        option.Command.Set(option.EnvironmentName, name, sequence, "RAFTER1115", "The environment fallback");
    }

    internal static void Default<T>(AuthoredOption option, T value)
    {
        long sequence = Begin(option);
        option.Command.Set(option.Default, value, sequence, "RAFTER1116", "The option default");
    }

    internal static void Sensitive(AuthoredOption option)
    {
        long sequence = Begin(option);
        option.Command.Set(option.Sensitive, true, sequence, "RAFTER1117", "Option sensitivity");
    }

    internal static void Validate<T>(AuthoredOption option, Func<T, bool> predicate, string message)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(message);
        long sequence = Begin(option);
        option.Validators.Add(new AuthoredValidator(predicate, message, sequence));
        if (string.IsNullOrWhiteSpace(message))
        {
            option.Command.Diagnostics.Add(new ModelDiagnostic(
                "RAFTER1118",
                "A validator message cannot be empty or whitespace.",
                sequence,
                1,
                DiagnosticStage.InvalidValue));
        }
    }

    private static long Begin(AuthoredOption option)
    {
        option.Command.EnsureMutable();
        return option.Command.NextSequence();
    }
}
