namespace Sotsera.Rafter;

using static PathRuntime;

/// <summary>Represents the context supplied to target callbacks.</summary>
public sealed class RafterContext
{
    private readonly BindingEngine.InvocationSnapshot? _snapshot;
    private readonly string? _root;
    private readonly string? _workingDirectory;
    private readonly IFileSystemPrimitives? _fileSystem;

    internal RafterContext()
    {
    }

    internal RafterContext(BindingEngine.InvocationSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    internal RafterContext(
        BindingEngine.InvocationSnapshot snapshot,
        string root,
        string workingDirectory,
        IFileSystemPrimitives fileSystem)
    {
        _snapshot = snapshot;
        _root = root;
        _workingDirectory = workingDirectory;
        _fileSystem = fileSystem;
    }

    /// <summary>Gets the normalized absolute command root.</summary>
    public string Root => _root
        ?? throw new InvalidOperationException("The context is not associated with resolved invocation paths.");

    /// <summary>Gets the normalized absolute logical working directory.</summary>
    public string WorkingDirectory => _workingDirectory
        ?? throw new InvalidOperationException("The context is not associated with resolved invocation paths.");

    /// <summary>Gets filesystem operations scoped to this context.</summary>
    public RafterFileSystem FileSystem => new(
        GetSnapshot().CommandId,
        GetSnapshot(),
        Root,
        WorkingDirectory,
        _fileSystem ?? throw new InvalidOperationException("The context has no filesystem services."));

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
        BindingEngine.InvocationSnapshot snapshot = GetSnapshot();
        if (snapshot.CommandId != option.Command.Id)
        {
            throw new InvalidOperationException("The option belongs to a different command.");
        }

        return snapshot;
    }

    private BindingEngine.InvocationSnapshot GetSnapshot()
        => _snapshot ?? throw new InvalidOperationException("The context is not associated with a bound invocation.");
}
