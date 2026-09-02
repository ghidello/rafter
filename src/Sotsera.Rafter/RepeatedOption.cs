using static Sotsera.Rafter.CommandModel;

namespace Sotsera.Rafter;

/// <summary>Represents an option that may occur more than once.</summary>
public sealed class RepeatedOption<T>
{
    internal RepeatedOption(AuthoredOption authored) => Authored = authored;

    internal AuthoredOption Authored { get; }

    /// <summary>Sets the option description.</summary>
    public RepeatedOption<T> Description(string description)
    {
        OptionMutation.Description(Authored, description);
        return this;
    }

    /// <summary>Sets the option's short alias.</summary>
    public RepeatedOption<T> Alias(char alias)
    {
        OptionMutation.Alias(Authored, alias);
        return this;
    }

    /// <summary>Marks the option values as sensitive output.</summary>
    public RepeatedOption<T> Sensitive()
    {
        OptionMutation.Sensitive(Authored);
        return this;
    }

    /// <summary>Adds a synchronous list validator.</summary>
    public RepeatedOption<T> Validate(Func<IReadOnlyList<T>, bool> predicate, string message)
    {
        OptionMutation.Validate(Authored, predicate, message);
        return this;
    }
}
