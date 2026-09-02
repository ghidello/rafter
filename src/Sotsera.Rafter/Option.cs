using static Sotsera.Rafter.CommandModel;

namespace Sotsera.Rafter;

/// <summary>Represents an optional scalar option.</summary>
public sealed class Option<T>
{
    internal Option(AuthoredOption authored) => Authored = authored;

    internal AuthoredOption Authored { get; }

    /// <summary>Sets the option description.</summary>
    public Option<T> Description(string description)
    {
        OptionMutation.Description(Authored, description);
        return this;
    }

    /// <summary>Sets the option's short alias.</summary>
    public Option<T> Alias(char alias)
    {
        OptionMutation.Alias(Authored, alias);
        return this;
    }

    /// <summary>Sets the option's environment fallback.</summary>
    public Option<T> FromEnvironment(string name)
    {
        OptionMutation.FromEnvironment(Authored, name);
        return this;
    }

    /// <summary>Sets the option's default value and returns its defaulted view.</summary>
    public DefaultedOption<T> Default(T value)
    {
        OptionMutation.Default(Authored, value);
        return new DefaultedOption<T>(Authored);
    }

    /// <summary>Marks the option value as sensitive output.</summary>
    public Option<T> Sensitive()
    {
        OptionMutation.Sensitive(Authored);
        return this;
    }

    /// <summary>Adds a synchronous value validator.</summary>
    public Option<T> Validate(Func<T, bool> predicate, string message)
    {
        OptionMutation.Validate(Authored, predicate, message);
        return this;
    }
}
