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
    private readonly TextRedactor _invocationRedactor = TextRedactor.Empty;
    private ProcessOperationScope? _processScope;

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
        string? targetName = null,
        TextRedactor? invocationRedactor = null)
    {
        _snapshot = snapshot;
        _root = root;
        _workingDirectory = workingDirectory;
        _fileSystem = fileSystem;
        _cancellationToken = cancellationToken;
        _invocationRedactor = invocationRedactor ?? output?.Redactor ?? TextRedactor.Empty;
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

    /// <summary>Creates an immutable child-process specification.</summary>
    public ProcessBuilder Process(string executable)
    {
        ArgumentNullException.ThrowIfNull(executable);
        return CreateProcessBuilder(new ProcessValue(executable, Sensitive: false));
    }

    /// <summary>Creates an immutable child-process specification from a required option.</summary>
    public ProcessBuilder Process(RequiredOption<string> executable)
    {
        ArgumentNullException.ThrowIfNull(executable);
        return CreateProcessBuilder(Resolve(executable));
    }

    /// <summary>Creates an immutable child-process specification from a defaulted option.</summary>
    public ProcessBuilder Process(DefaultedOption<string> executable)
    {
        ArgumentNullException.ThrowIfNull(executable);
        return CreateProcessBuilder(Resolve(executable));
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
        BindingEngine.InvocationSnapshot snapshot = GetSnapshot();
        if (snapshot.CommandId != option.Command.Id)
        {
            throw new InvalidOperationException("The option belongs to a different command.");
        }

        return snapshot;
    }

    private BindingEngine.InvocationSnapshot GetSnapshot()
        => _snapshot ?? throw new InvalidOperationException("The context is not associated with a bound invocation.");

    private ProcessBuilder CreateProcessBuilder(ProcessValue executable)
    {
        ProcessOperationScope scope = _processScope
            ?? throw new InvalidOperationException("Processes can be created only while an invocation callback is active.");
        scope.EnsureActive();
        return ProcessBuilder.Create(this, scope, executable);
    }

    internal (bool Present, ProcessValue Value) Resolve(Option<string> option)
    {
        BindingEngine.InvocationSnapshot snapshot = GetOwnedSnapshot(option.Authored);
        string? value = snapshot.GetOptional<string>(option.Authored.Id);
        return value is null
            ? (false, default)
            : (true, new ProcessValue(value, IsSensitive(option.Authored)));
    }

    internal ProcessValue Resolve(RequiredOption<string> option)
    {
        BindingEngine.InvocationSnapshot snapshot = GetOwnedSnapshot(option.Authored);
        return new ProcessValue(snapshot.GetRequired<string>(option.Authored.Id), IsSensitive(option.Authored));
    }

    internal ProcessValue Resolve(DefaultedOption<string> option)
    {
        BindingEngine.InvocationSnapshot snapshot = GetOwnedSnapshot(option.Authored);
        return new ProcessValue(snapshot.GetRequired<string>(option.Authored.Id), IsSensitive(option.Authored));
    }

    internal (bool Present, bool Value) Resolve(Option<bool> option)
    {
        BindingEngine.InvocationSnapshot snapshot = GetOwnedSnapshot(option.Authored);
        bool? value = snapshot.GetOptional<bool>(option.Authored.Id);
        return value.HasValue ? (true, value.Value) : (false, false);
    }

    internal bool Resolve(RequiredOption<bool> option)
        => GetOwnedSnapshot(option.Authored).GetRequired<bool>(option.Authored.Id);

    internal bool Resolve(DefaultedOption<bool> option)
        => GetOwnedSnapshot(option.Authored).GetRequired<bool>(option.Authored.Id);

    internal (bool Present, TimeSpan Value) Resolve(Option<TimeSpan> option)
    {
        BindingEngine.InvocationSnapshot snapshot = GetOwnedSnapshot(option.Authored);
        TimeSpan? value = snapshot.GetOptional<TimeSpan>(option.Authored.Id);
        return value.HasValue ? (true, value.Value) : (false, default);
    }

    internal TimeSpan Resolve(RequiredOption<TimeSpan> option)
        => GetOwnedSnapshot(option.Authored).GetRequired<TimeSpan>(option.Authored.Id);

    internal TimeSpan Resolve(DefaultedOption<TimeSpan> option)
        => GetOwnedSnapshot(option.Authored).GetRequired<TimeSpan>(option.Authored.Id);

    internal (bool Present, long Value) Resolve(Option<long> option)
    {
        BindingEngine.InvocationSnapshot snapshot = GetOwnedSnapshot(option.Authored);
        long? value = snapshot.GetOptional<long>(option.Authored.Id);
        return value.HasValue ? (true, value.Value) : (false, default);
    }

    internal long Resolve(RequiredOption<long> option)
        => GetOwnedSnapshot(option.Authored).GetRequired<long>(option.Authored.Id);

    internal long Resolve(DefaultedOption<long> option)
        => GetOwnedSnapshot(option.Authored).GetRequired<long>(option.Authored.Id);

    internal ProcessOperationScope OpenProcessScope()
    {
        if (_processScope is not null)
        {
            throw new InvalidOperationException("A process-operation scope is already active.");
        }

        _processScope = new ProcessOperationScope();
        return _processScope;
    }

    internal Task<bool> CloseProcessScopeAsync(ProcessOperationScope scope)
    {
        if (!ReferenceEquals(Interlocked.CompareExchange(ref _processScope, null, scope), scope))
        {
            throw new InvalidOperationException("The process-operation scope is not active on this context.");
        }

        return scope.CloseAndSettleAsync();
    }

    internal InvocationOutput InvocationOutput => _output?.Invocation
        ?? throw new InvalidOperationException("The context has no invocation output services.");

    internal TextRedactor InvocationRedactor => _invocationRedactor;

    internal static bool IsSensitive(CommandModel.AuthoredOption option)
        => option.Sensitive.IsSet && option.Sensitive.Value;

    internal OutputScope? OutputScope => _outputScope;

    internal void CloseOutputScope()
    {
        if (_outputScope is not null)
        {
            _outputScope.Close();
        }
    }
}
