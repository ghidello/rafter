using static Sotsera.Rafter.CommandModel;

namespace Sotsera.Rafter;

/// <summary>Represents an optional scalar option with an authored default.</summary>
public sealed class DefaultedOption<T>
{
    internal DefaultedOption(AuthoredOption authored) => Authored = authored;

    internal AuthoredOption Authored { get; }

    /// <summary>Sets the option description.</summary>
    public DefaultedOption<T> Description(string description)
    {
        OptionMutation.Description(Authored, description);
        return this;
    }

    /// <summary>Sets the option's short alias.</summary>
    public DefaultedOption<T> Alias(char alias)
    {
        OptionMutation.Alias(Authored, alias);
        return this;
    }

    /// <summary>Sets the option's environment fallback.</summary>
    public DefaultedOption<T> FromEnvironment(string name)
    {
        OptionMutation.FromEnvironment(Authored, name);
        return this;
    }

    /// <summary>Marks the option value as sensitive output.</summary>
    public DefaultedOption<T> Sensitive()
    {
        OptionMutation.Sensitive(Authored);
        return this;
    }

    /// <summary>Adds a synchronous value validator.</summary>
    public DefaultedOption<T> Validate(Func<T, bool> predicate, string message)
    {
        OptionMutation.Validate(Authored, predicate, message);
        return this;
    }
}
