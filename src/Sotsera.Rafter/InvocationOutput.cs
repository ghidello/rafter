using System.Globalization;
using System.Text;

namespace Sotsera.Rafter;

internal sealed class InvocationOutput
{
    private const int MaximumPropertyCharacters = 1_048_576;
    private const int MaximumPropertyItems = 1_024;
    private const int MaximumBindingCharacters = 1_048_576;
    private readonly StringBuilder _bindingError = new();
    private readonly StringBuilder _bindingOutput = new();
    private readonly Lock _consoleSync = new();
    private readonly Lock _sync = new();
    private readonly TextWriter _standardOutput;
    private readonly TextWriter _standardError;
    private readonly OutputCapabilities _outputCapabilities;
    private readonly OutputCapabilities _errorCapabilities;
    private ConsoleBuffer _consoleError;
    private ConsoleBuffer _consoleOutput;
    private TextRedactor _redactor;
    private TaskCompletionSource? _drained;
    private Exception? _failure;
    private LiveTargetDisplay? _live;
    private int _admittedCalls;
    private bool _sealed;
    private bool _bindingPending;

    internal InvocationOutput(
        TextWriter standardOutput,
        TextWriter standardError,
        OutputCapabilities outputCapabilities,
        OutputCapabilities errorCapabilities,
        TextRedactor redactor)
    {
        _standardOutput = standardOutput;
        _standardError = standardError;
        _outputCapabilities = outputCapabilities;
        _errorCapabilities = errorCapabilities;
        _redactor = redactor;
        _consoleOutput = new ConsoleBuffer(standardError: false, redactor);
        _consoleError = new ConsoleBuffer(standardError: true, redactor);
    }

    internal static InvocationOutput ForBinding(InvocationServices services)
        => new(
            services.StandardOutput,
            services.StandardError,
            services.StandardOutputCapabilities,
            services.StandardErrorCapabilities,
            TextRedactor.Empty)
        {
            _bindingPending = true,
        };

    internal Exception? Failure
    {
        get
        {
            lock (_sync)
            {
                return _failure;
            }
        }
    }

    internal TextRedactor Redactor => _redactor;

    internal long LastSequence { get; private set; }

    internal void StartLive(GraphPlanner.GraphPlan plan)
    {
        if (_outputCapabilities.IsRich && _outputCapabilities.SupportsAnsi && _outputCapabilities.SupportsCursor
            && _outputCapabilities.NewLine is "\n" or "\r\n")
        {
            _live = new LiveTargetDisplay(plan, _outputCapabilities, _redactor);
        }
    }

    internal void Observe(ExecutionRuntime.TargetNotification notification)
    {
        using Lock.Scope publication = TerminalPublication.Enter();
        LastSequence = TerminalPublication.NextSequence();
        if (_live is not null && Failure is null)
        {
            FlushConsoleForOrdering(standardError: false);
            FlushConsoleForOrdering(standardError: true);
            TerminalPublication.UpdateLive(this, _standardOutput, _standardError, _live.Update(notification));
        }
    }

    internal void CompleteBinding(TextRedactor? redactor)
    {
        using Lock.Scope publication = TerminalPublication.Enter();
        LastSequence = TerminalPublication.NextSequence();
        List<OutputEvent> events = [];
        lock (_consoleSync)
        {
            if (!_bindingPending)
            {
                return;
            }

            _bindingPending = false;
            if (redactor is not null && Failure is null)
            {
                _redactor = redactor;
                _consoleOutput = new ConsoleBuffer(standardError: false, redactor);
                _consoleError = new ConsoleBuffer(standardError: true, redactor);
                events.AddRange(_consoleOutput.Append("command", _bindingOutput.ToString()));
                events.AddRange(_consoleError.Append("command", _bindingError.ToString()));
            }

            _bindingOutput.Clear();
            _bindingError.Clear();
        }

        foreach (OutputEvent outputEvent in events)
        {
            PublishCore(outputEvent);
        }
    }

    internal Admission Admit()
    {
        lock (_sync)
        {
            if (_sealed)
            {
                throw new InvalidOperationException("Output is no longer available for this invocation.");
            }

            _admittedCalls++;
            return new Admission(this);
        }
    }

    internal async Task SealAsync()
    {
        Task drain;
        lock (_sync)
        {
            _sealed = true;
            if (_admittedCalls == 0)
            {
                drain = Task.CompletedTask;
            }
            else
            {
                _drained ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                drain = _drained.Task;
            }
        }

        await drain.ConfigureAwait(false);
        TerminalPublication.EndLive(this);
        FlushConsole();
    }

    internal void Publish(OutputEvent outputEvent)
    {
        using Lock.Scope publication = TerminalPublication.Enter();
        LastSequence = TerminalPublication.NextSequence();
        bool standardError = IsStandardError(outputEvent.Kind);
        FlushConsoleForOrdering(standardError ? _standardError : _standardOutput);
        PublishCore(outputEvent);
    }

    internal bool TryFlushConsole(bool standardError)
    {
        using Admission? admission = TryAdmit();
        if (admission is null)
        {
            return false;
        }

        using Lock.Scope publication = TerminalPublication.Enter();
        LastSequence = TerminalPublication.NextSequence();
        TextWriter writer = standardError ? _standardError : _standardOutput;
        FlushConsoleForOrdering(writer);
        if (Failure is null)
        {
            try
            {
                TerminalPublication.Flush(writer);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }
        return true;
    }

    internal bool TryPublishConsole(bool standardError, OutputScope? scope, string text)
    {
        Admission? admission = TryAdmit();
        if (admission is null)
        {
            return false;
        }

        using (admission)
        {
            PublishConsoleAdmitted(standardError, scope, text);
        }

        return true;
    }

    internal void PublishConsoleAdmitted(bool standardError, OutputScope? scope, string text)
    {
        using Lock.Scope publication = TerminalPublication.Enter();
        LastSequence = TerminalPublication.NextSequence();
        List<OutputEvent> events;
        lock (_consoleSync)
        {
            if (_bindingPending)
            {
                StringBuilder quarantine = standardError ? _bindingError : _bindingOutput;
                if (Failure is not null)
                {
                    return;
                }

                if (text.Length > MaximumBindingCharacters - quarantine.Length)
                {
                    _bindingOutput.Clear();
                    _bindingError.Clear();
                    Fail(new InvalidOperationException("Binding console output exceeded its quarantine limit."));
                    return;
                }

                quarantine.Append(text);
                return;
            }

            ConsoleBuffer buffer = standardError ? _consoleError : _consoleOutput;
            events = buffer.Append(scope?.GetName() ?? "command", text);
        }

        foreach (OutputEvent outputEvent in events)
        {
            PublishCore(outputEvent);
        }
    }

    internal void Fail(Exception exception)
    {
        lock (_sync)
        {
            _failure ??= exception;
        }
    }

    internal bool UsesWriter(TextWriter writer)
        => ReferenceEquals(writer, _standardOutput) || ReferenceEquals(writer, _standardError);

    internal void FlushConsoleForOrdering(bool standardError)
    {
        using Lock.Scope publication = TerminalPublication.Enter();
        foreach (OutputEvent outputEvent in TakeConsole(standardError))
        {
            PublishCore(outputEvent);
        }
    }

    internal void FlushConsoleForOrdering(TextWriter writer)
    {
        using Lock.Scope publication = TerminalPublication.Enter();
        if (ReferenceEquals(writer, _standardOutput))
        {
            FlushConsoleForOrdering(standardError: false);
        }
        if (ReferenceEquals(writer, _standardError))
        {
            FlushConsoleForOrdering(standardError: true);
        }
    }

    internal static OutputProperty SnapshotProperty(string name, object? value)
    {
        string canonical;
        if (value is null)
        {
            canonical = "null";
        }
        else if (value is string text)
        {
            canonical = Quote(text);
        }
        else if (value is System.Collections.IDictionary
            || ImplementsOpenGeneric(value.GetType(), typeof(IAsyncEnumerable<>)))
        {
            throw new ArgumentException("Dictionaries and asynchronous collections are not supported.", nameof(value));
        }
        else if (value is Array { Rank: > 1 })
        {
            throw new ArgumentException("Multidimensional arrays are not supported.", nameof(value));
        }
        else if (value is System.Collections.IEnumerable values)
        {
            canonical = SnapshotCollection(name, values, nameof(value));
        }
        else
        {
            canonical = FormatScalar(value);
        }

        EnsurePropertySize(name, canonical, nameof(value));
        return new OutputProperty(name, canonical);
    }

    private void PublishCore(OutputEvent outputEvent)
    {
        lock (_sync)
        {
            if (_failure is not null)
            {
                return;
            }
        }

        try
        {
            OutputProperty? property = outputEvent.Property?.Redact(_redactor);
            if (property is not null)
            {
                outputEvent = outputEvent with { Text = $"{property.Name}={property.CanonicalValue}" };
            }
            if (!_redactor.TryRedact(outputEvent.Scope, out string safeScope)
                || !_redactor.TryRedact(outputEvent.Text, out string safeText)
                || !_redactor.TryRedact(outputEvent.Recovery ?? string.Empty, out string safeRecovery))
            {
                throw new InvalidOperationException("Output could not be redacted safely.");
            }

            OutputEvent safeEvent = outputEvent with
            {
                Scope = safeScope,
                Text = safeText,
                Recovery = outputEvent.Recovery is null ? null : safeRecovery,
                Property = property,
            };
            if (outputEvent.Kind is not (OutputKind.Console or OutputKind.ConsoleError))
            {
                _redactor.EnsureCompleteBoundary(safeText + "\n");
                if (outputEvent.Recovery is not null)
                {
                    _redactor.EnsureCompleteBoundary(safeRecovery + "\n");
                }
            }
            bool standardError = IsStandardError(safeEvent.Kind);
            TextWriter writer = standardError ? _standardError : _standardOutput;
            OutputCapabilities capabilities = standardError ? _errorCapabilities : _outputCapabilities;
            string rendered = OutputPresentation.Render(safeEvent, capabilities);
            if (_redactor.ContainsPattern(rendered))
            {
                throw new InvalidOperationException("Rendered output failed redaction verification.");
            }

            TerminalPublication.Write(writer, rendered, this);
        }
        catch (Exception exception)
        {
            lock (_sync)
            {
                _failure ??= exception;
            }
        }
    }

    private static bool IsStandardError(OutputKind kind)
        => kind is OutputKind.Warning or OutputKind.Error or OutputKind.ConsoleError;

    private List<OutputEvent> TakeConsole(bool standardError)
    {
        lock (_consoleSync)
        {
            try
            {
                return (standardError ? _consoleError : _consoleOutput).Flush(final: false);
            }
            catch (Exception exception)
            {
                Fail(exception);
                return [];
            }
        }
    }

    private void FlushConsole()
    {
        using Lock.Scope publication = TerminalPublication.Enter();
        List<OutputEvent> events = [];
        lock (_consoleSync)
        {
            events.AddRange(_consoleOutput.Flush(final: true));
            events.AddRange(_consoleError.Flush(final: true));
        }

        foreach (OutputEvent outputEvent in events)
        {
            PublishCore(outputEvent);
        }
    }

    private static string SnapshotCollection(
        string name,
        System.Collections.IEnumerable values,
        string parameterName)
    {
        List<string> items = [];
        long characterCount = name.Length + 3L;
        System.Collections.IEnumerator enumerator = values.GetEnumerator();
        try
        {
            while (enumerator.MoveNext())
            {
                if (items.Count == MaximumPropertyItems)
                {
                    throw new ArgumentException(
                        $"Property collections cannot contain more than {MaximumPropertyItems} items.",
                        parameterName);
                }

                object? item = enumerator.Current;
                ValidateCollectionItem(item, parameterName);
                string formatted = FormatScalar(item);
                characterCount += formatted.Length + (items.Count == 0 ? 0 : 1);
                if (characterCount > MaximumPropertyCharacters)
                {
                    throw new ArgumentException(
                        $"A formatted property cannot exceed {MaximumPropertyCharacters} characters.",
                        parameterName);
                }

                items.Add(formatted);
            }
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }

        return $"[{string.Join(',', items)}]";
    }

    private static void ValidateCollectionItem(object? item, string parameterName)
    {
        if (item is not null
            && item is not string
            && (item is System.Collections.IEnumerable
                || ImplementsOpenGeneric(item.GetType(), typeof(IAsyncEnumerable<>))))
        {
            throw new ArgumentException("Nested collections are not supported.", parameterName);
        }

        if (item is System.Collections.DictionaryEntry || IsKeyValuePair(item?.GetType()))
        {
            throw new ArgumentException("Structured key/value elements are not supported.", parameterName);
        }
    }

    private static string FormatScalar(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        if (value is string text)
        {
            return Quote(text);
        }

        if (value is bool boolean)
        {
            return boolean ? "true" : "false";
        }

        if (value is char or Enum or Guid or DateOnly or TimeOnly or DateTime or DateTimeOffset or TimeSpan)
        {
            return Quote(FormatInvariant(value));
        }

        string formatted = FormatInvariant(value);
        bool nonFinite = value is double d && !double.IsFinite(d)
            || value is float f && !float.IsFinite(f)
            || value is Half h && !Half.IsFinite(h);
        return IsJsonNumber(value.GetType()) && !nonFinite ? formatted : Quote(formatted);
    }

    private static string Quote(string value) => OutputProperty.Quote(value);

    private static string FormatInvariant(object value)
        => value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
                ?? throw new InvalidOperationException("A property value formatted to null.")
            : value.ToString() ?? throw new InvalidOperationException("A property value formatted to null.");

    private static void EnsurePropertySize(string name, string canonical, string parameterName)
    {
        if (name.Length + 1L + canonical.Length > MaximumPropertyCharacters)
        {
            throw new ArgumentException(
                $"A formatted property cannot exceed {MaximumPropertyCharacters} characters.",
                parameterName);
        }
    }

    private static bool ImplementsOpenGeneric(Type type, Type openGeneric)
        => type.GetInterfaces().Any(candidate =>
            candidate.IsGenericType && candidate.GetGenericTypeDefinition() == openGeneric);

    private static bool IsKeyValuePair(Type? type)
        => type is not null
            && type.IsGenericType
            && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>);

    private static bool IsJsonNumber(Type type)
    {
        Type effective = Nullable.GetUnderlyingType(type) ?? type;
        return effective == typeof(byte)
            || effective == typeof(sbyte)
            || effective == typeof(short)
            || effective == typeof(ushort)
            || effective == typeof(int)
            || effective == typeof(uint)
            || effective == typeof(long)
            || effective == typeof(ulong)
            || effective == typeof(float)
            || effective == typeof(double)
            || effective == typeof(decimal)
            || effective == typeof(Half)
            || effective == typeof(Int128)
            || effective == typeof(UInt128)
            || effective == typeof(nint)
            || effective == typeof(nuint);
    }

    private void Release()
    {
        lock (_sync)
        {
            _admittedCalls--;
            if (_sealed && _admittedCalls == 0)
            {
                _drained?.SetResult();
            }
        }
    }

    private Admission? TryAdmit()
    {
        lock (_sync)
        {
            if (_sealed)
            {
                return null;
            }

            _admittedCalls++;
            return new Admission(this);
        }
    }

    internal sealed class Admission : IDisposable
    {
        private InvocationOutput? _owner;

        internal Admission(InvocationOutput owner)
        {
            _owner = owner;
        }

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release();
    }

    private sealed class ConsoleBuffer
    {
        private const int MaximumSegmentCharacters = 65_536;
        private readonly StringBuilder _buffer = new();
        private readonly bool _standardError;
        private readonly StreamingTextRedactor _redactor;
        private bool _pendingInputCarriageReturn;
        private bool _skipInputLineFeed;
        private bool _pendingCarriageReturn;
        private bool _continued;
        private string? _pendingInputScope;
        private string? _scope;

        internal ConsoleBuffer(bool standardError, TextRedactor redactor)
        {
            _standardError = standardError;
            _redactor = new StreamingTextRedactor(redactor);
        }

        internal List<OutputEvent> Append(string scope, string text)
        {
            List<OutputEvent> events = [];
            foreach (char character in text)
            {
                if (_skipInputLineFeed)
                {
                    _skipInputLineFeed = false;
                    if (character == '\n')
                    {
                        continue;
                    }
                }
                if (_pendingInputCarriageReturn)
                {
                    AppendNormalized(_pendingInputScope!, '\n', events);
                    _pendingInputCarriageReturn = false;
                    _pendingInputScope = null;
                    if (character == '\n')
                    {
                        continue;
                    }
                }

                if (character == '\r')
                {
                    _pendingInputCarriageReturn = true;
                    _pendingInputScope = scope;
                }
                else
                {
                    AppendNormalized(scope, character, events);
                }
            }

            return events;
        }

        internal List<OutputEvent> Flush(bool final)
        {
            List<OutputEvent> events = [];
            if (_pendingInputCarriageReturn)
            {
                AppendNormalized(_pendingInputScope!, '\n', events);
                _pendingInputCarriageReturn = false;
                _pendingInputScope = null;
                // Publishing a pending CR must not turn a later LF into a second source newline.
                _skipInputLineFeed = true;
            }

            if (final)
            {
                _redactor.Complete((safeScope, safeText) => AppendSafe(safeScope, safeText, events));
            }
            else
            {
                _redactor.CompleteBoundary((safeScope, safeText) => AppendSafe(safeScope, safeText, events));
            }
            FlushInto(events, includeEmptyLine: _pendingCarriageReturn, continues: !final);
            return events;
        }

        private void AppendNormalized(string scope, char character, List<OutputEvent> events)
            => _redactor.Append(
                scope,
                character.ToString(),
                (safeScope, safeText) => AppendSafe(safeScope, safeText, events));

        private void AppendSafe(string scope, string text, List<OutputEvent> events)
        {
            if (_scope is not null && !string.Equals(_scope, scope, StringComparison.Ordinal))
            {
                FlushInto(events, includeEmptyLine: _pendingCarriageReturn, continues: true);
            }

            _scope = scope;
            if (text.Length > 1 && _buffer.Length != 0 && _buffer.Length + text.Length > MaximumSegmentCharacters)
            {
                FlushInto(events, includeEmptyLine: false, continues: true);
                _scope = scope;
            }

            foreach (char character in text)
            {
                AppendSafeCharacter(character, events);
            }
        }

        private void AppendSafeCharacter(char character, List<OutputEvent> events)
        {
            if (_pendingCarriageReturn)
            {
                FlushInto(events, includeEmptyLine: true);
                if (character == '\n')
                {
                    return;
                }
            }

            if (character == '\r')
            {
                _pendingCarriageReturn = true;
            }
            else if (character == '\n')
            {
                FlushInto(events, includeEmptyLine: true);
            }
            else
            {
                if (_buffer.Length == MaximumSegmentCharacters)
                {
                    string? scope = _scope;
                    FlushInto(events, includeEmptyLine: false, continues: true);
                    _scope = scope;
                }
                _buffer.Append(character);
            }
        }

        private void FlushInto(List<OutputEvent> events, bool includeEmptyLine, bool continues = false)
        {
            if (_buffer.Length != 0 || includeEmptyLine)
            {
                events.Add(new OutputEvent(
                    _scope ?? "command",
                    _standardError ? OutputKind.ConsoleError : OutputKind.Console,
                    (_continued ? "[continued] " : string.Empty) + _buffer
                        + (continues ? " [continues]" : string.Empty)));
                _continued = continues;
            }

            _buffer.Clear();
            _pendingCarriageReturn = false;
            _scope = null;
        }
    }
}
