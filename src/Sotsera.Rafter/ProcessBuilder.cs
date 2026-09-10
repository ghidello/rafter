using System.Collections.Immutable;

namespace Sotsera.Rafter;

/// <summary>Builds an immutable, argument-safe child-process specification.</summary>
public sealed class ProcessBuilder
{
    private readonly RafterContext _context;
    private readonly ProcessOperationScope _operationScope;
    private readonly ProcessSpecification _specification;

    private ProcessBuilder(
        RafterContext context,
        ProcessOperationScope operationScope,
        ProcessSpecification specification)
    {
        _context = context;
        _operationScope = operationScope;
        _specification = specification;
    }

    internal static ProcessBuilder Create(
        RafterContext context,
        ProcessOperationScope operationScope,
        ProcessValue executable)
    {
        ImmutableArray<ProcessDiagnostic> diagnostics = ValidateExecutable(executable.Text, sequence: 0);
        ProcessSpecification specification = new(
            executable,
            [],
            [],
            diagnostics,
            WorkingDirectory: null,
            Timeout: null,
            CaptureLimitBytes: null,
            [0],
            HasEnvironment: false,
            HasWorkingDirectory: false,
            HasTimeout: false,
            HasCaptureLimit: false,
            HasValidExitCodes: false,
            NextSequence: 1);
        return new ProcessBuilder(context, operationScope, specification);
    }

    /// <summary>Appends one literal argument token.</summary>
    public ProcessBuilder Argument(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return AppendArgument(new ProcessValue(value, Sensitive: false));
    }

    /// <summary>Appends an optional string option value when present.</summary>
    public ProcessBuilder Argument(Option<string> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        (bool present, ProcessValue resolved) = _context.Resolve(value);
        return present ? AppendArgument(resolved) : this;
    }

    /// <summary>Appends a required string option value.</summary>
    public ProcessBuilder Argument(RequiredOption<string> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return AppendArgument(_context.Resolve(value));
    }

    /// <summary>Appends a defaulted string option value.</summary>
    public ProcessBuilder Argument(DefaultedOption<string> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return AppendArgument(_context.Resolve(value));
    }

    /// <summary>Appends an unconditional flag token.</summary>
    public ProcessBuilder Flag(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return AppendNamedToken(name);
    }

    /// <summary>Appends a flag token when the condition is true.</summary>
    public ProcessBuilder Flag(string name, bool condition)
    {
        ArgumentNullException.ThrowIfNull(name);
        return condition ? AppendNamedToken(name) : this;
    }

    /// <summary>Appends a flag token when an optional Boolean option is present and true.</summary>
    public ProcessBuilder Flag(string name, Option<bool> condition)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(condition);
        (bool present, bool resolved) = _context.Resolve(condition);
        return present && resolved ? AppendNamedToken(name) : this;
    }

    /// <summary>Appends a flag token when a required Boolean option is true.</summary>
    public ProcessBuilder Flag(string name, RequiredOption<bool> condition)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(condition);
        return _context.Resolve(condition) ? AppendNamedToken(name) : this;
    }

    /// <summary>Appends a flag token when a defaulted Boolean option is true.</summary>
    public ProcessBuilder Flag(string name, DefaultedOption<bool> condition)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(condition);
        return _context.Resolve(condition) ? AppendNamedToken(name) : this;
    }

    /// <summary>Appends a literal name and value as two argument tokens.</summary>
    public ProcessBuilder Option(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        return AppendOption(name, new ProcessValue(value, Sensitive: false));
    }

    /// <summary>Appends a name and optional string value as two tokens when the value is present.</summary>
    public ProcessBuilder Option(string name, Option<string> value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        (bool present, ProcessValue resolved) = _context.Resolve(value);
        return present ? AppendOption(name, resolved) : this;
    }

    /// <summary>Appends a name and required string value as two argument tokens.</summary>
    public ProcessBuilder Option(string name, RequiredOption<string> value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        return AppendOption(name, _context.Resolve(value));
    }

    /// <summary>Appends a name and defaulted string value as two argument tokens.</summary>
    public ProcessBuilder Option(string name, DefaultedOption<string> value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        return AppendOption(name, _context.Resolve(value));
    }

    /// <summary>Configures ordered edits to the child process environment.</summary>
    public ProcessBuilder Environment(Action<ProcessEnvironmentBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        long sequence = _specification.NextSequence;
        ProcessEnvironmentBuilder draft = new(_context, sequence);
        try
        {
            configure(draft);
        }
        finally
        {
            draft.Close();
        }

        ImmutableArray<ProcessDiagnostic> diagnostics = _specification.Diagnostics;
        if (_specification.HasEnvironment)
        {
            diagnostics = diagnostics
                .AddRange(draft.Diagnostics)
                .Add(DuplicatePolicy(sequence, "environment"));
            return Derive(_specification with
            {
                Diagnostics = diagnostics,
                NextSequence = sequence + 1,
            });
        }

        return Derive(_specification with
        {
            EnvironmentEdits = draft.Edits,
            Diagnostics = diagnostics.AddRange(draft.Diagnostics),
            HasEnvironment = true,
            NextSequence = sequence + 1,
        });
    }

    /// <summary>Sets a process working directory relative to the target working directory.</summary>
    public ProcessBuilder WorkingDirectory(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return SetWorkingDirectory(path);
    }

    /// <summary>Sets a process working directory from an optional option when present.</summary>
    public ProcessBuilder WorkingDirectory(Option<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        (bool present, ProcessValue value) = _context.Resolve(path);
        return present ? SetWorkingDirectory(value.Text) : this;
    }

    /// <summary>Sets a process working directory from a required option.</summary>
    public ProcessBuilder WorkingDirectory(RequiredOption<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return SetWorkingDirectory(_context.Resolve(path).Text);
    }

    /// <summary>Sets a process working directory from a defaulted option.</summary>
    public ProcessBuilder WorkingDirectory(DefaultedOption<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return SetWorkingDirectory(_context.Resolve(path).Text);
    }

    /// <summary>Sets the authored process execution timeout.</summary>
    public ProcessBuilder Timeout(TimeSpan timeout) => SetTimeout(timeout);

    /// <summary>Sets the timeout from an optional option when present.</summary>
    public ProcessBuilder Timeout(Option<TimeSpan> timeout)
    {
        ArgumentNullException.ThrowIfNull(timeout);
        (bool present, TimeSpan value) = _context.Resolve(timeout);
        return present ? SetTimeout(value) : this;
    }

    /// <summary>Sets the timeout from a required option.</summary>
    public ProcessBuilder Timeout(RequiredOption<TimeSpan> timeout)
    {
        ArgumentNullException.ThrowIfNull(timeout);
        return SetTimeout(_context.Resolve(timeout));
    }

    /// <summary>Sets the timeout from a defaulted option.</summary>
    public ProcessBuilder Timeout(DefaultedOption<TimeSpan> timeout)
    {
        ArgumentNullException.ThrowIfNull(timeout);
        return SetTimeout(_context.Resolve(timeout));
    }

    /// <summary>Sets the retained-byte limit independently for each captured stream.</summary>
    public ProcessBuilder CaptureLimitBytes(long limit) => SetCaptureLimit(limit);

    /// <summary>Sets the capture limit from an optional option when present.</summary>
    public ProcessBuilder CaptureLimitBytes(Option<long> limit)
    {
        ArgumentNullException.ThrowIfNull(limit);
        (bool present, long value) = _context.Resolve(limit);
        return present ? SetCaptureLimit(value) : this;
    }

    /// <summary>Sets the capture limit from a required option.</summary>
    public ProcessBuilder CaptureLimitBytes(RequiredOption<long> limit)
    {
        ArgumentNullException.ThrowIfNull(limit);
        return SetCaptureLimit(_context.Resolve(limit));
    }

    /// <summary>Sets the capture limit from a defaulted option.</summary>
    public ProcessBuilder CaptureLimitBytes(DefaultedOption<long> limit)
    {
        ArgumentNullException.ThrowIfNull(limit);
        return SetCaptureLimit(_context.Resolve(limit));
    }

    /// <summary>Replaces the complete set of valid child-process exit codes.</summary>
    public ProcessBuilder ValidExitCodes(params int[] codes)
    {
        ArgumentNullException.ThrowIfNull(codes);
        long sequence = _specification.NextSequence;
        ImmutableArray<ProcessDiagnostic> diagnostics = _specification.Diagnostics;
        if (_specification.HasValidExitCodes)
        {
            diagnostics = diagnostics.Add(DuplicatePolicy(sequence, "valid exit codes"));
        }

        ImmutableArray<int> normalized = [.. codes.Distinct()];
        if (codes.Length == 0)
        {
            diagnostics = diagnostics.Add(new ProcessDiagnostic(
                "RAFTER1510",
                "A valid-exit declaration must contain at least one exit code.",
                sequence));
        }

        return Derive(_specification with
        {
            ValidExitCodes = _specification.HasValidExitCodes ? _specification.ValidExitCodes : normalized,
            Diagnostics = diagnostics,
            HasValidExitCodes = true,
            NextSequence = sequence + 1,
        });
    }

    /// <summary>Runs the process and streams its output through Rafter presentation.</summary>
    public Task<ProcessExit> Run()
    {
        _operationScope.EnsureActive();
        ProcessOperationScope.Operation operation = _operationScope.Register();
        Task<ProcessExit> task = ProcessRuntime.Run(_context, _specification, operation);
        operation.Attach(task);
        return task;
    }

    /// <summary>Runs the process and returns its complete bounded output.</summary>
    public Task<ProcessCapture> Capture()
    {
        _operationScope.EnsureActive();
        ProcessOperationScope.Operation operation = _operationScope.Register();
        Task<ProcessCapture> task = ProcessRuntime.Capture(_context, _specification, operation);
        operation.Attach(task);
        return task;
    }

    private ProcessBuilder AppendArgument(ProcessValue value)
    {
        long sequence = _specification.NextSequence;
        ImmutableArray<ProcessDiagnostic> diagnostics = _specification.Diagnostics;
        if (value.Text.Contains('\0', StringComparison.Ordinal))
        {
            diagnostics = diagnostics.Add(InvalidToken(sequence));
        }

        return Derive(_specification with
        {
            Arguments = _specification.Arguments.Add(value),
            Diagnostics = diagnostics,
            NextSequence = sequence + 1,
        });
    }

    private ProcessBuilder AppendNamedToken(string name)
    {
        long sequence = _specification.NextSequence;
        ImmutableArray<ProcessDiagnostic> diagnostics = ValidateName(name, sequence);
        return Derive(_specification with
        {
            Arguments = _specification.Arguments.Add(new ProcessValue(name, Sensitive: false)),
            Diagnostics = _specification.Diagnostics.AddRange(diagnostics),
            NextSequence = sequence + 1,
        });
    }

    private ProcessBuilder AppendOption(string name, ProcessValue value)
    {
        long sequence = _specification.NextSequence;
        ImmutableArray<ProcessDiagnostic> diagnostics = ValidateName(name, sequence);
        if (value.Text.Contains('\0', StringComparison.Ordinal))
        {
            diagnostics = diagnostics.Add(InvalidToken(sequence, order: 1));
        }

        return Derive(_specification with
        {
            Arguments = _specification.Arguments
                .Add(new ProcessValue(name, Sensitive: false))
                .Add(value),
            Diagnostics = _specification.Diagnostics.AddRange(diagnostics),
            NextSequence = sequence + 1,
        });
    }

    private ProcessBuilder SetWorkingDirectory(string path)
    {
        long sequence = _specification.NextSequence;
        ImmutableArray<ProcessDiagnostic> diagnostics = _specification.Diagnostics;
        string? resolved = null;
        try
        {
            resolved = PathRuntime.PathPolicy.ResolveUnderRoot(_context.Root, _context.WorkingDirectory, path);
        }
        catch (PathRuntime.PathPolicyException)
        {
            diagnostics = diagnostics.Add(new ProcessDiagnostic(
                "RAFTER1507",
                "The process working directory is malformed, unsupported, or outside the command root.",
                sequence));
        }

        if (_specification.HasWorkingDirectory)
        {
            diagnostics = diagnostics.Add(DuplicatePolicy(sequence, "working directory"));
        }

        return Derive(_specification with
        {
            WorkingDirectory = _specification.HasWorkingDirectory ? _specification.WorkingDirectory : resolved,
            Diagnostics = diagnostics,
            HasWorkingDirectory = true,
            NextSequence = sequence + 1,
        });
    }

    private ProcessBuilder SetTimeout(TimeSpan timeout)
    {
        long sequence = _specification.NextSequence;
        ImmutableArray<ProcessDiagnostic> diagnostics = _specification.Diagnostics;
        if (timeout < TimeSpan.FromMilliseconds(1)
            || timeout > TimeSpan.FromMilliseconds(4_294_967_294d))
        {
            diagnostics = diagnostics.Add(new ProcessDiagnostic(
                "RAFTER1508",
                "The process timeout is outside the supported range.",
                sequence));
        }

        if (_specification.HasTimeout)
        {
            diagnostics = diagnostics.Add(DuplicatePolicy(sequence, "timeout"));
        }

        return Derive(_specification with
        {
            Timeout = _specification.HasTimeout ? _specification.Timeout : timeout,
            Diagnostics = diagnostics,
            HasTimeout = true,
            NextSequence = sequence + 1,
        });
    }

    private ProcessBuilder SetCaptureLimit(long limit)
    {
        long sequence = _specification.NextSequence;
        ImmutableArray<ProcessDiagnostic> diagnostics = _specification.Diagnostics;
        if (limit is < 1 or > int.MaxValue)
        {
            diagnostics = diagnostics.Add(new ProcessDiagnostic(
                "RAFTER1509",
                "The capture limit is outside the supported range.",
                sequence));
        }

        if (_specification.HasCaptureLimit)
        {
            diagnostics = diagnostics.Add(DuplicatePolicy(sequence, "capture limit"));
        }

        return Derive(_specification with
        {
            CaptureLimitBytes = _specification.HasCaptureLimit ? _specification.CaptureLimitBytes : limit,
            Diagnostics = diagnostics,
            HasCaptureLimit = true,
            NextSequence = sequence + 1,
        });
    }

    private ProcessBuilder Derive(ProcessSpecification specification)
        => new(_context, _operationScope, specification);

    private static ImmutableArray<ProcessDiagnostic> ValidateExecutable(string executable, long sequence)
        => string.IsNullOrWhiteSpace(executable) || executable.Contains('\0', StringComparison.Ordinal)
            ? [new ProcessDiagnostic("RAFTER1501", "The process executable is invalid.", sequence)]
            : [];

    private static ImmutableArray<ProcessDiagnostic> ValidateName(string name, long sequence)
    {
        ImmutableArray<ProcessDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<ProcessDiagnostic>();
        if (string.IsNullOrWhiteSpace(name))
        {
            diagnostics.Add(new ProcessDiagnostic(
                "RAFTER1504",
                "A process flag or option name is empty or whitespace.",
                sequence));
        }

        if (name.Contains('\0', StringComparison.Ordinal))
        {
            diagnostics.Add(InvalidToken(sequence, order: 1));
        }

        return diagnostics.ToImmutable();
    }

    private static ProcessDiagnostic InvalidToken(long sequence, int order = 0)
        => new("RAFTER1503", "A process argument token contains NUL.", sequence, order);

    private static ProcessDiagnostic DuplicatePolicy(long sequence, string policy)
        => new("RAFTER1511", $"The process {policy} policy may be specified only once.", sequence);
}
