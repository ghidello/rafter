namespace Sotsera.Rafter;

/// <summary>Represents the context supplied to target callbacks.</summary>
public sealed class RafterContext
{
    private readonly BindingEngine.InvocationSnapshot? _snapshot;

    internal RafterContext()
    {
    }

    internal RafterContext(BindingEngine.InvocationSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    /// <summary>Gets the bound value of an optional option.</summary>
    public T? Value<T>(Option<T> option)
    {
        ArgumentNullException.ThrowIfNull(option);
        BindingEngine.InvocationSnapshot snapshot = GetOwnedSnapshot(option.Authored);
        return snapshot.GetOptional<T>(option.Authored.Id);
    }

    /// <summary>Gets the bound value of a required option.</summary>
    public T Value<T>(RequiredOption<T> option)
    {
        ArgumentNullException.ThrowIfNull(option);
        BindingEngine.InvocationSnapshot snapshot = GetOwnedSnapshot(option.Authored);
        return snapshot.GetRequired<T>(option.Authored.Id);
    }

    /// <summary>Gets the bound value of an option with an authored default.</summary>
    public T Value<T>(DefaultedOption<T> option)
    {
        ArgumentNullException.ThrowIfNull(option);
        BindingEngine.InvocationSnapshot snapshot = GetOwnedSnapshot(option.Authored);
        return snapshot.GetRequired<T>(option.Authored.Id);
    }

    /// <summary>Gets the immutable bound values of a repeated option.</summary>
    public IReadOnlyList<T> Value<T>(RepeatedOption<T> option)
    {
        ArgumentNullException.ThrowIfNull(option);
        BindingEngine.InvocationSnapshot snapshot = GetOwnedSnapshot(option.Authored);
        return snapshot.GetRepeated<T>(option.Authored.Id);
    }

    private BindingEngine.InvocationSnapshot GetOwnedSnapshot(CommandModel.AuthoredOption option)
    {
        BindingEngine.InvocationSnapshot snapshot = _snapshot
            ?? throw new InvalidOperationException("The context is not associated with a bound invocation.");
        if (snapshot.CommandId != option.Command.Id)
        {
            throw new InvalidOperationException("The option belongs to a different command.");
        }

        return snapshot;
    }
}
