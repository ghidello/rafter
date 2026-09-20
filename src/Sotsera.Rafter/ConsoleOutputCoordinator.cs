using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Sotsera.Rafter;

internal static class ConsoleOutputCoordinator
{
    private static readonly AsyncLocal<Scope?> CurrentScope = new();
    private static readonly Dictionary<Guid, Registration> Registrations = [];
    private static readonly Lock Sync = new();
    private static TextWriter? _coordinatingError;
    private static TextWriter? _coordinatingOutput;
    private static TextWriter? _hostError;
    private static TextWriter? _hostOutput;

    internal static Lease Register(InvocationOutput output)
    {
        lock (Sync)
        {
            if (Registrations.Count == 0)
            {
                _hostOutput = Console.Out;
                _hostError = Console.Error;
                InstallWriters();
            }

            Registration registration = new(Guid.NewGuid(), output);
            Registrations.Add(registration.Id, registration);
            Scope? previous = CurrentScope.Value;
            CurrentScope.Value = new Scope(registration, Target: null);
            return new Lease(registration, previous);
        }
    }

    internal static TargetLease EnterTarget(OutputScope? target)
    {
        Scope? current = CurrentScope.Value;
        if (current is null || !current.Registration.IsActive || target is null)
        {
            return new TargetLease(previous: null, active: false);
        }

        CurrentScope.Value = new Scope(current.Registration, target);
        return new TargetLease(current, active: true);
    }

    internal static (TextWriter Output, TextWriter Error)? TryGetHostWriters()
    {
        lock (Sync)
        {
            return Registrations.Count == 0 ? null : (_hostOutput!, _hostError!);
        }
    }

    internal static void VerifyActiveOwnership()
    {
        lock (Sync)
        {
            if (Registrations.Count != 0)
            {
                VerifyOwnership();
            }
        }
    }

    internal static void FlushBeforeReport(TextWriter writer)
    {
        Registration[] registrations;
        lock (Sync)
        {
            registrations = [.. Registrations.Values];
        }

        foreach (Registration registration in registrations)
        {
            registration.Output.FlushConsoleForOrdering(writer);
        }
    }

    private static void Route(bool standardError, TextWriter fallback, string text)
    {
        Scope? scope = CurrentScope.Value;
        if (!TerminalPublication.IsPublishing
            && scope is not null
            && scope.Registration.IsActive
            && scope.Registration.Output.TryPublishConsole(standardError, scope.Target, text))
        {
            return;
        }

        Registration[] registrations;
        TextWriter writer;
        lock (Sync)
        {
            registrations = [.. Registrations.Values];
            writer = Registrations.Count == 0
                ? fallback
                : standardError ? _hostError! : _hostOutput!;
        }

        foreach (Registration registration in registrations)
        {
            registration.Output.FlushConsoleForOrdering(standardError);
        }

        writer.Write(text);
    }

    private static string GetNewLine(bool standardError, TextWriter fallback)
    {
        lock (Sync)
        {
            try
            {
                return fallback.NewLine;
            }
            catch (Exception exception)
            {
                FailActiveRegistrations(standardError, exception);
                throw;
            }
        }
    }

    private static void SetNewLine(bool standardError, TextWriter fallback, string value)
    {
        lock (Sync)
        {
            try
            {
                fallback.NewLine = value;
            }
            catch (Exception exception)
            {
                FailActiveRegistrations(standardError, exception);
                throw;
            }
        }
    }

    private static void Unregister(Registration registration, Scope? previous)
    {
        lock (Sync)
        {
            VerifyOwnership();
            registration.Deactivate();
            _ = Registrations.Remove(registration.Id);
            CurrentScope.Value = previous is not null && previous.Registration.IsActive ? previous : null;
            if (Registrations.Count != 0)
            {
                return;
            }

            RestoreHostWriter(standardError: false, _hostOutput!, registration.Output);
            RestoreHostWriter(standardError: true, _hostError!, registration.Output);
            _coordinatingOutput = null;
            _coordinatingError = null;
            _hostOutput = null;
            _hostError = null;
        }
    }

    private static void VerifyOwnership()
    {
        bool outputLost = !ReferenceEquals(Console.Out, _coordinatingOutput);
        bool errorLost = !ReferenceEquals(Console.Error, _coordinatingError);
        if (!outputLost && !errorLost)
        {
            return;
        }

        foreach (Registration active in Registrations.Values)
        {
            active.Output.Fail(new InvalidOperationException(
                outputLost && errorLost
                    ? "Console output and error ownership were replaced during execution."
                    : outputLost
                        ? "Console output ownership was replaced during execution."
                        : "Console error ownership was replaced during execution."));
        }

        if (outputLost)
        {
            ReinstallWriter(standardError: false, _coordinatingOutput!);
        }

        if (errorLost)
        {
            ReinstallWriter(standardError: true, _coordinatingError!);
        }
    }

    private static void InstallWriters()
    {
        try
        {
            Console.SetOut(new CoordinatingWriter(standardError: false, _hostOutput!));
            _coordinatingOutput = Console.Out;
            Console.SetError(new CoordinatingWriter(standardError: true, _hostError!));
            _coordinatingError = Console.Error;
        }
        catch
        {
            if (_coordinatingOutput is not null)
            {
                Console.SetOut(_hostOutput!);
            }

            _coordinatingOutput = null;
            _coordinatingError = null;
            _hostOutput = null;
            _hostError = null;
            throw;
        }
    }

    private static void ReinstallWriter(bool standardError, TextWriter writer)
    {
        try
        {
            if (standardError)
            {
                Console.SetError(writer);
                _coordinatingError = Console.Error;
            }
            else
            {
                Console.SetOut(writer);
                _coordinatingOutput = Console.Out;
            }
        }
        catch (Exception exception)
        {
            FailActiveRegistrations(standardError, exception);
        }
    }

    private static void RestoreHostWriter(bool standardError, TextWriter writer, InvocationOutput output)
    {
        try
        {
            if (standardError)
            {
                Console.SetError(writer);
            }
            else
            {
                Console.SetOut(writer);
            }
        }
        catch (Exception exception)
        {
            output.Fail(new IOException("The host console writer could not be restored.", exception));
        }
    }

    private static void FailActiveRegistrations(bool standardError, Exception exception)
    {
        string stream = standardError ? "error" : "output";
        foreach (Registration active in Registrations.Values)
        {
            active.Output.Fail(new IOException($"The host console {stream} writer failed.", exception));
        }
    }

    internal sealed class Lease : IDisposable
    {
        private readonly Scope? _previous;
        private Registration? _registration;

        internal Lease(Registration registration, Scope? previous)
        {
            _registration = registration;
            _previous = previous;
        }

        public void Dispose()
        {
            Registration? registration = Interlocked.Exchange(ref _registration, null);
            if (registration is not null)
            {
                Unregister(registration, _previous);
            }
        }
    }

    internal sealed class TargetLease : IDisposable
    {
        private readonly bool _active;
        private Scope? _previous;

        internal TargetLease(Scope? previous, bool active)
        {
            _previous = previous;
            _active = active;
        }

        public void Dispose()
        {
            if (_active)
            {
                CurrentScope.Value = Interlocked.Exchange(ref _previous, null);
            }
        }
    }

    private sealed class CoordinatingWriter : TextWriter
    {
        private readonly TextWriter _fallback;
        private readonly bool _standardError;

        internal CoordinatingWriter(bool standardError, TextWriter fallback)
        {
            _standardError = standardError;
            _fallback = fallback;
        }

        public override Encoding Encoding => _fallback.Encoding;

        [AllowNull]
        public override string NewLine
        {
            get => GetNewLine(_standardError, _fallback);
            set => SetNewLine(_standardError, _fallback, value ?? Environment.NewLine);
        }

        public override void Write(char value) => Route(_standardError, _fallback, value.ToString());

        public override void Write(char[] buffer, int index, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            Route(_standardError, _fallback, new string(buffer, index, count));
        }

        public override void Write(string? value)
        {
            if (value is not null)
            {
                Route(_standardError, _fallback, value);
            }
        }

        public override void WriteLine(string? value)
            => Route(_standardError, _fallback, string.Concat(value, NewLine));

        public override void WriteLine() => Route(_standardError, _fallback, NewLine);

        public override Task WriteAsync(char value)
        {
            Write(value);
            return Task.CompletedTask;
        }

        public override Task WriteAsync(string? value)
        {
            Write(value);
            return Task.CompletedTask;
        }

        public override Task WriteAsync(char[] buffer, int index, int count)
        {
            Write(buffer, index, count);
            return Task.CompletedTask;
        }

        public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Route(_standardError, _fallback, buffer.ToString());
            return Task.CompletedTask;
        }

        public override Task WriteLineAsync(
            ReadOnlyMemory<char> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Route(_standardError, _fallback, string.Concat(buffer, NewLine));
            return Task.CompletedTask;
        }
    }

    internal sealed class Registration
    {
        private int _active = 1;

        internal Registration(Guid id, InvocationOutput output)
        {
            Id = id;
            Output = output;
        }

        internal Guid Id { get; }

        internal InvocationOutput Output { get; }

        internal bool IsActive => Volatile.Read(ref _active) != 0;

        internal void Deactivate() => Volatile.Write(ref _active, 0);
    }

    internal sealed record Scope(Registration Registration, OutputScope? Target);
}
