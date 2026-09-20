using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Sotsera.Rafter;

internal sealed class InvocationOutput
{
    private const int MaximumPropertyCharacters = 1_048_576;
    private const int MaximumPropertyItems = 1_024;
    private const int MaximumBindingCharacters = 1_048_576;
    private static readonly AsyncLocal<int> PublicationDepth = new();
    private static readonly Lock TerminalSync = new();
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

    internal static bool IsPublishing => PublicationDepth.Value != 0;

    internal TextRedactor Redactor => _redactor;

    internal void CompleteBinding(TextRedactor? redactor)
    {
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
        FlushConsole();
    }

    internal void Publish(OutputEvent outputEvent)
    {
        bool standardError = IsStandardError(outputEvent.Kind);
        foreach (OutputEvent buffered in TakeConsole(standardError))
        {
            PublishCore(buffered);
        }

        PublishCore(outputEvent);
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

    internal void FlushConsoleForOrdering(bool standardError)
    {
        foreach (OutputEvent outputEvent in TakeConsole(standardError))
        {
            PublishCore(outputEvent);
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
            if (!_redactor.TryRedact(outputEvent.Text, out string safeText)
                || !_redactor.TryRedact(outputEvent.Recovery ?? string.Empty, out string safeRecovery))
            {
                throw new InvalidOperationException("Output could not be redacted safely.");
            }

            OutputEvent safeEvent = outputEvent with
            {
                Text = safeText,
                Recovery = outputEvent.Recovery is null ? null : safeRecovery,
            };
            bool standardError = IsStandardError(safeEvent.Kind);
            TextWriter writer = standardError ? _standardError : _standardOutput;
            OutputCapabilities capabilities = standardError ? _errorCapabilities : _outputCapabilities;
            string rendered = OutputPresentation.Render(safeEvent, capabilities);
            if (_redactor.ContainsPattern(rendered))
            {
                throw new InvalidOperationException("Rendered output failed redaction verification.");
            }

            lock (TerminalSync)
            {
                lock (_sync)
                {
                    if (_failure is not null)
                    {
                        return;
                    }
                }

                PublicationDepth.Value++;
                try
                {
                    writer.Write(rendered);
                }
                finally
                {
                    PublicationDepth.Value--;
                }
            }
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
            return (standardError ? _consoleError : _consoleOutput).Flush();
        }
    }

    private void FlushConsole()
    {
        List<OutputEvent> events = [];
        lock (_consoleSync)
        {
            events.AddRange(_consoleOutput.Flush());
            events.AddRange(_consoleError.Flush());
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
        return IsJsonNumber(value.GetType()) ? formatted : Quote(formatted);
    }

    private static string Quote(string value) => $"\"{JsonEncodedText.Encode(value)}\"";

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
        private bool _pendingCarriageReturn;
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

        internal List<OutputEvent> Flush()
        {
            List<OutputEvent> events = [];
            if (_pendingInputCarriageReturn)
            {
                AppendNormalized(_pendingInputScope!, '\n', events);
                _pendingInputCarriageReturn = false;
                _pendingInputScope = null;
            }

            _redactor.Complete((safeScope, safeText) => AppendSafe(safeScope, safeText, events));
            FlushInto(events, includeEmptyLine: _pendingCarriageReturn);
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
                FlushInto(events, includeEmptyLine: _pendingCarriageReturn);
            }

            _scope = scope;
            if (text.Length > 1 && _buffer.Length != 0 && _buffer.Length + text.Length > MaximumSegmentCharacters)
            {
                FlushInto(events, includeEmptyLine: false);
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
                _buffer.Append(character);
                if (_buffer.Length == MaximumSegmentCharacters)
                {
                    FlushInto(events, includeEmptyLine: false);
                }
            }
        }

        private void FlushInto(List<OutputEvent> events, bool includeEmptyLine)
        {
            if (_buffer.Length != 0 || includeEmptyLine)
            {
                events.Add(new OutputEvent(
                    _scope ?? "command",
                    _standardError ? OutputKind.ConsoleError : OutputKind.Console,
                    _buffer.ToString()));
            }

            _buffer.Clear();
            _pendingCarriageReturn = false;
            _scope = null;
        }
    }
}
