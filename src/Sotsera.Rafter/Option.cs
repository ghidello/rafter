using Sotsera.Rafter.Internal;

namespace Sotsera.Rafter;

/// <summary>An optional input. Required or Default selects its presence policy.</summary>
public sealed class Option<T>
{
    internal OptionDeclaration Declaration { get; }

    internal Option(OptionDeclaration declaration)
    {
        Declaration = declaration;
    }

    /// <summary>Adds aliases in declaration order. Invalid configuration is reported by RunAsync.</summary>
    public Option<T> Aliases(params string[] aliases)
    {
        Declaration.AddAliases(aliases);
        return this;
    }

    /// <summary>Adds a rule for supplied values. Omitted optional values skip these rules.</summary>
    public Option<T> Validate(string description, Func<T, bool> predicate)
    {
        Declaration.AddRule(description, predicate);
        return this;
    }

    /// <summary>Requires a supplied value, including an explicit empty string, zero, or false.</summary>
    public ValueOption<T> Required()
    {
        Declaration.Require();
        return new(Declaration);
    }

    /// <summary>Uses this value when the input is omitted. The default is also validated.</summary>
    public ValueOption<T> Default(T value)
    {
        Declaration.SetDefault(value);
        return new(Declaration);
    }
}
