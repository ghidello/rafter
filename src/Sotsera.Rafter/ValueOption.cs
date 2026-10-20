using Sotsera.Rafter.Internal;

namespace Sotsera.Rafter;

/// <summary>A required or defaulted input whose validated value is present during execution.</summary>
public sealed class ValueOption<T>
{
    internal OptionDeclaration Declaration { get; }

    internal ValueOption(OptionDeclaration declaration)
    {
        Declaration = declaration;
    }

    /// <summary>Adds aliases without changing the input's presence policy.</summary>
    public ValueOption<T> Aliases(params string[] aliases)
    {
        Declaration.AddAliases(aliases);
        return this;
    }

    /// <summary>Adds a rule for supplied values and defaults.</summary>
    public ValueOption<T> Validate(string description, Func<T, bool> predicate)
    {
        Declaration.AddRule(description, predicate);
        return this;
    }
}
