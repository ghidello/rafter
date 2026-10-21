using Sotsera.Rafter.Internal;

namespace Sotsera.Rafter;

/// <summary>Configures a direct executable invocation owned by the current command or target body.</summary>
public sealed class ProcessCommand
{
    private readonly object _gate = new();
    private readonly BodyProcesses _body;
    private readonly string _executable;
    private readonly string _root;
    private readonly List<string> _arguments = [];
    private readonly Dictionary<string, string> _environment = new(StringComparer.OrdinalIgnoreCase);
    private int[]? _acceptedExitCodes;
    private string? _directory;
    private Func<ProcessResult, ProcessFailureAction>? _handler;
    private bool _hasHandler;

    internal ProcessCommand(BodyProcesses body, string executable, string root)
    {
        CheckText(executable, nameof(executable));
        _body = body;
        _executable = executable;
        _root = root;
    }

    /// <summary>Appends separate argument tokens, with no shell interpretation.</summary>
    public ProcessCommand Arguments(params string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var copy = arguments.ToArray();
        foreach (var argument in copy)
        {
            if (argument is null || argument.Contains('\0'))
            {
                throw new ArgumentException("Argument tokens must not be null or contain NUL.", nameof(arguments));
            }
        }
        lock (_gate)
        {
            _arguments.AddRange(copy);
        }
        return this;
    }

    /// <summary>Handles a completed process with a rejected exit code. May be specified once.</summary>
    public ProcessCommand OnFailure(Func<ProcessResult, ProcessFailureAction> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_gate)
        {
            if (_hasHandler)
            {
                throw new InvalidOperationException("A process failure handler may only be specified once.");
            }
            _hasHandler = true;
            _handler = handler;
        }
        return this;
    }

    /// <summary>Chooses the accepted exit codes once. Repeated codes within this list are harmless.</summary>
    public ProcessCommand AcceptExitCodes(params int[] exitCodes)
    {
        ArgumentNullException.ThrowIfNull(exitCodes);
        if (exitCodes.Length == 0)
        {
            throw new ArgumentException("At least one accepted exit code is required.", nameof(exitCodes));
        }
        lock (_gate)
        {
            if (_acceptedExitCodes is not null)
            {
                throw new InvalidOperationException("Accepted exit codes may only be specified once.");
            }
            _acceptedExitCodes = exitCodes.Distinct().ToArray();
        }
        return this;
    }

    /// <summary>Chooses this child's directory once. Relative paths resolve against the captured invocation root.</summary>
    public ProcessCommand WorkingDirectory(string path)
    {
        CheckText(path, nameof(path));
        lock (_gate)
        {
            if (_directory is not null)
            {
                throw new InvalidOperationException("A child working directory may only be specified once.");
            }
            _directory = Path.GetFullPath(path, _root);
        }
        return this;
    }

    /// <summary>Overrides an inherited variable for this child. Each name may be configured once per child.</summary>
    public ProcessCommand Environment(string name, string value)
    {
        CheckText(name, nameof(name));
        ArgumentNullException.ThrowIfNull(value);
        if (name.Contains('=') || value.Contains('\0'))
        {
            throw new ArgumentException("Environment names must not contain '='; values must not contain NUL.");
        }
        lock (_gate)
        {
            if (!_environment.TryAdd(name, value))
            {
                throw new InvalidOperationException($"Environment variable '{name}' was already configured for this child.");
            }
        }
        return this;
    }

    /// <summary>Starts this configuration and awaits its process scope, output, and failure handler.</summary>
    public Task<ProcessResult> RunAsync()
    {
        lock (_gate)
        {
            var executable = _executable.IndexOfAny(['/', '\\', ':']) >= 0
                ? Path.GetFullPath(_executable, _root) : _executable;
            return _body.Run(new(executable, _arguments.ToArray(), _directory ?? _root,
                new Dictionary<string, string>(_environment, StringComparer.OrdinalIgnoreCase),
                _acceptedExitCodes?.ToArray() ?? [0], _handler));
        }
    }

    private static void CheckText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('\0'))
        {
            throw new ArgumentException("The value must contain non-whitespace text and must not contain NUL.", name);
        }
    }
}
