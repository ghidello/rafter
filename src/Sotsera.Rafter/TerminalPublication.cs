namespace Sotsera.Rafter;

internal static class TerminalPublication
{
    private static readonly AsyncLocal<PublicationScope?> CurrentPublication = new();
    private static readonly Lock Sync = new();

    internal static bool IsPublishing => FindActivePublication() is not null;

    // Async-facing report APIs complete only after this synchronous terminal boundary has published the document.
    internal static void WriteReport(TextWriter writer, string text)
    {
        lock (Sync)
        {
            if (!IsPublishing)
            {
                ConsoleOutputCoordinator.FlushBeforeReport(writer);
            }
            WriteCore(writer, text, invocation: null);
        }
    }

    internal static void Write(TextWriter writer, string text, InvocationOutput invocation)
    {
        lock (Sync)
        {
            WriteCore(writer, text, invocation);
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
}
