namespace Sotsera.Rafter;

internal static class TerminalPublication
{
    private static readonly AsyncLocal<PublicationScope?> CurrentPublication = new();
    private static readonly Lock Sync = new();
    private static readonly Dictionary<InvocationOutput, LiveSurface> Surfaces = [];
    private static long _sequence;
    private static bool _atLineBoundary = true;

    internal static bool IsPublishing => FindActivePublication() is not null;

    internal static Lock.Scope Enter() => Sync.EnterScope();

    internal static long NextSequence() => Interlocked.Increment(ref _sequence);

    internal static void UpdateLive(InvocationOutput invocation, TextWriter writer, string frame)
    {
        lock (Sync)
        {
            ClearLive();
            if (invocation.Failure is null)
            {
                Surfaces[invocation] = new LiveSurface(writer, frame);
            }
            DrawLive();
        }
    }

    internal static void EndLive(InvocationOutput invocation)
    {
        lock (Sync)
        {
            if (Surfaces.ContainsKey(invocation))
            {
                ClearLive();
                Surfaces.Remove(invocation);
                DrawLive();
            }
        }
    }

    // Async-facing report APIs complete only after this synchronous terminal boundary has published the document.
    internal static void WriteReport(TextWriter writer, string text)
    {
        lock (Sync)
        {
            if (!IsPublishing)
            {
                ConsoleOutputCoordinator.FlushBeforeReport(writer);
            }
            WriteAroundLive(writer, text, invocation: null);
        }
    }

    internal static void Write(TextWriter writer, string text, InvocationOutput invocation)
    {
        lock (Sync)
        {
            WriteAroundLive(writer, text, invocation);
        }
    }

    internal static void WriteHost(TextWriter writer, string text)
    {
        lock (Sync)
        {
            _ = NextSequence();
            WriteAroundLive(writer, text, invocation: null);
        }
    }

    internal static void Flush(TextWriter writer)
    {
        lock (Sync)
        {
            PublicationScope? previous = CurrentPublication.Value;
            PublicationScope publication = new(FindActivePublication());
            CurrentPublication.Value = publication;
            try
            {
                writer.Flush();
            }
            finally
            {
                publication.Close();
                CurrentPublication.Value = previous;
            }
        }
    }

    private static void WriteAroundLive(TextWriter writer, string text, InvocationOutput? invocation)
    {
        if (IsPublishing)
        {
            WriteCore(writer, text, invocation);
            return;
        }

        ClearLive();
        try
        {
            WriteCore(writer, text, invocation);
            if (text.Length != 0)
            {
                _atLineBoundary = text.EndsWith('\n');
            }
        }
        finally
        {
            DrawLive();
        }
    }

    private static void ClearLive()
    {
        foreach ((InvocationOutput invocation, LiveSurface surface) in Surfaces.Reverse().ToArray())
        {
            if (surface.VisibleLines == 0)
            {
                continue;
            }

            string erase = string.Concat(Enumerable.Repeat("\u001b[1A\r\u001b[2K", surface.VisibleLines));
            surface.VisibleLines = 0;
            try
            {
                // Erasure remains necessary after a managed failure, before the guarded final diagnostic.
                WriteCore(surface.Writer, erase, invocation: null);
            }
            catch (Exception exception)
            {
                invocation.Fail(exception);
                Surfaces.Remove(invocation);
            }
        }
    }

    private static void DrawLive()
    {
        if (!_atLineBoundary)
        {
            return;
        }

        foreach ((InvocationOutput invocation, LiveSurface surface) in Surfaces.ToArray())
        {
            if (invocation.Failure is not null)
            {
                Surfaces.Remove(invocation);
                continue;
            }

            try
            {
                WriteCore(surface.Writer, surface.Frame, invocation);
                surface.VisibleLines = surface.Frame.Count(character => character == '\n');
            }
            catch
            {
                Surfaces.Remove(invocation);
            }
        }
    }

    private static void WriteCore(TextWriter writer, string text, InvocationOutput? invocation)
    {
        if (invocation?.Failure is not null)
        {
            return;
        }

        PublicationScope? previous = CurrentPublication.Value;
        PublicationScope publication = new(FindActivePublication());
        CurrentPublication.Value = publication;
        try
        {
            writer.Write(text);
        }
        catch (Exception exception)
        {
            // Mark failure before another publication can enter and observe this invocation as healthy.
            invocation?.Fail(exception);
            throw;
        }
        finally
        {
            // Child execution contexts retain this object, so closing it ends their bypass permission too.
            publication.Close();
            CurrentPublication.Value = previous;
        }
    }

    private static PublicationScope? FindActivePublication()
    {
        for (PublicationScope? scope = CurrentPublication.Value; scope is not null; scope = scope.Parent)
        {
            if (scope.IsActive)
            {
                return scope;
            }
        }

        return null;
    }

    private sealed class PublicationScope(PublicationScope? parent)
    {
        private int _active = 1;

        internal PublicationScope? Parent { get; } = parent;

        internal bool IsActive => Volatile.Read(ref _active) != 0;

        internal void Close() => Volatile.Write(ref _active, 0);
    }

    private sealed class LiveSurface(TextWriter writer, string frame)
    {
        internal TextWriter Writer { get; } = writer;

        internal string Frame { get; } = frame;

        internal int VisibleLines { get; set; }
    }
}
