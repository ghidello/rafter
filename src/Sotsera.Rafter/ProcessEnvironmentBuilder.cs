using System.Collections.Immutable;

namespace Sotsera.Rafter;

/// <summary>Builds an ordered child-process environment edit list.</summary>
public sealed class ProcessEnvironmentBuilder
{
    private readonly RafterContext _context;
    private readonly ImmutableArray<ProcessDiagnostic>.Builder _diagnostics = ImmutableArray.CreateBuilder<ProcessDiagnostic>();
    private readonly ImmutableArray<ProcessEnvironmentEdit>.Builder _edits = ImmutableArray.CreateBuilder<ProcessEnvironmentEdit>();
    private readonly long _sequence;
    private bool _closed;
    private int _order;

    internal ProcessEnvironmentBuilder(RafterContext context, long sequence)
    {
        _context = context;
        _sequence = sequence;
    }

    internal ImmutableArray<ProcessDiagnostic> Diagnostics => _diagnostics.ToImmutable();

    internal ImmutableArray<ProcessEnvironmentEdit> Edits => _edits.ToImmutable();

    /// <summary>Clears the inherited child environment and all preceding edits.</summary>
    public ProcessEnvironmentBuilder Clear()
    {
        EnsureOpen();
        _edits.Add(new ProcessEnvironmentEdit(ProcessEnvironmentEditKind.Clear, null, default, _sequence, _order++));
        return this;
    }

    /// <summary>Removes an environment variable.</summary>
    public ProcessEnvironmentBuilder Unset(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        EnsureOpen();
        ValidateKey(name);
        _edits.Add(new ProcessEnvironmentEdit(
            ProcessEnvironmentEditKind.Unset,
            name,
            default,
            _sequence,
            _order++));
        return this;
    }

    /// <summary>Sets an environment variable to a literal value.</summary>
    public ProcessEnvironmentBuilder Set(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        return SetCore(name, new ProcessValue(value, Sensitive: false));
    }

    /// <summary>Sets an environment variable from an optional option when present.</summary>
    public ProcessEnvironmentBuilder Set(string name, Option<string> value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        EnsureOpen();
        (bool present, ProcessValue resolved) = _context.Resolve(value);
        return present ? SetCore(name, resolved) : this;
    }

    /// <summary>Sets an environment variable from a required option.</summary>
    public ProcessEnvironmentBuilder Set(string name, RequiredOption<string> value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        return SetCore(name, _context.Resolve(value));
    }

    /// <summary>Sets an environment variable from a defaulted option.</summary>
    public ProcessEnvironmentBuilder Set(string name, DefaultedOption<string> value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        return SetCore(name, _context.Resolve(value));
    }

    /// <summary>Sets a sensitive literal environment value.</summary>
    public ProcessEnvironmentBuilder SetSensitive(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        return SetCore(name, new ProcessValue(value, Sensitive: true));
    }

    /// <summary>Sets a sensitive environment value from an optional option when present.</summary>
    public ProcessEnvironmentBuilder SetSensitive(string name, Option<string> value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        EnsureOpen();
        (bool present, ProcessValue resolved) = _context.Resolve(value);
        return present ? SetCore(name, resolved with { Sensitive = true }) : this;
    }

    /// <summary>Sets a sensitive environment value from a required option.</summary>
    public ProcessEnvironmentBuilder SetSensitive(string name, RequiredOption<string> value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        return SetCore(name, _context.Resolve(value) with { Sensitive = true });
    }

    /// <summary>Sets a sensitive environment value from a defaulted option.</summary>
    public ProcessEnvironmentBuilder SetSensitive(string name, DefaultedOption<string> value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        return SetCore(name, _context.Resolve(value) with { Sensitive = true });
    }

    internal void Close() => _closed = true;

    private ProcessEnvironmentBuilder SetCore(string name, ProcessValue value)
    {
        EnsureOpen();
        ValidateKey(name);
        int order = _order++;
        if (value.Text.Contains('\0', StringComparison.Ordinal))
        {
            _diagnostics.Add(new ProcessDiagnostic(
                "RAFTER1506",
                "A process environment value contains NUL.",
                _sequence,
                order));
        }

        _edits.Add(new ProcessEnvironmentEdit(ProcessEnvironmentEditKind.Set, name, value, _sequence, order));
        return this;
    }

    private void ValidateKey(string name)
    {
        int order = _order;
        if (string.IsNullOrWhiteSpace(name)
            || name.Contains('\0', StringComparison.Ordinal)
            || name.Contains('=', StringComparison.Ordinal))
        {
            _diagnostics.Add(new ProcessDiagnostic(
                "RAFTER1505",
                "A process environment key is invalid.",
                _sequence,
                order));
        }
    }

    private void EnsureOpen()
    {
        if (_closed)
        {
            throw new InvalidOperationException("The process environment builder is no longer active.");
        }
    }
}
