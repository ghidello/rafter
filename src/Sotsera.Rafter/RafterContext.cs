namespace Sotsera.Rafter;

using static PathRuntime;

/// <summary>Represents the context supplied to target callbacks.</summary>
public sealed class RafterContext
{
    private readonly BindingEngine.InvocationSnapshot? _snapshot;
    private readonly string? _root;
    private readonly string? _workingDirectory;
    private readonly IFileSystemPrimitives? _fileSystem;
    private readonly CancellationToken _cancellationToken;
    private readonly OutputScope? _outputScope;
    private readonly RafterOutput? _output;

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
        IFileSystemPrimitives fileSystem,
        CancellationToken cancellationToken,
        InvocationOutput? output = null,
        string? targetName = null)
    {
        _snapshot = snapshot;
        _root = root;
        _workingDirectory = workingDirectory;
        _fileSystem = fileSystem;
        _cancellationToken = cancellationToken;
        if (output is not null)
        {
            _outputScope = new OutputScope(targetName);
            _output = new RafterOutput(output, _outputScope);
        }
    }

    /// <summary>Gets the token that signals cancellation of the current invocation callback.</summary>
    public CancellationToken CancellationToken => _cancellationToken;

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

    /// <summary>Gets semantic output for the current invocation.</summary>
    public RafterOutput Output => _output
        ?? throw new InvalidOperationException("The context has no invocation output services.");

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

    internal OutputScope? OutputScope => _outputScope;

    internal void CloseOutputScope()
    {
        if (_outputScope is not null)
        {
            _outputScope.Close();
        }
    }
}
