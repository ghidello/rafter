using static Sotsera.Rafter.CommandModel;

namespace Sotsera.Rafter;

/// <summary>Represents a required scalar option.</summary>
public sealed class RequiredOption<T>
{
    internal RequiredOption(AuthoredOption authored) => Authored = authored;

    internal AuthoredOption Authored { get; }

    /// <summary>Sets the option description.</summary>
    public RequiredOption<T> Description(string description)
    {
        OptionMutation.Description(Authored, description);
        return this;
    }

    /// <summary>Sets the option's short alias.</summary>
    public RequiredOption<T> Alias(char alias)
    {
        OptionMutation.Alias(Authored, alias);
        return this;
    }

    /// <summary>Sets the option's environment fallback.</summary>
    public RequiredOption<T> FromEnvironment(string name)
    {
        OptionMutation.FromEnvironment(Authored, name);
        return this;
    }

    /// <summary>Marks the option value as sensitive output.</summary>
    public RequiredOption<T> Sensitive()
    {
        OptionMutation.Sensitive(Authored);
        return this;
    }

    /// <summary>Adds a synchronous value validator.</summary>
    public RequiredOption<T> Validate(Func<T, bool> predicate, string message)
    {
        OptionMutation.Validate(Authored, predicate, message);
        return this;
    }
}
