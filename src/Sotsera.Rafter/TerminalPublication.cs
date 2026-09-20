namespace Sotsera.Rafter;

internal static class TerminalPublication
{
    private static readonly AsyncLocal<int> PublicationDepth = new();
    private static readonly Lock Sync = new();

    internal static bool IsPublishing => PublicationDepth.Value != 0;

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

        PublicationDepth.Value++;
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
            PublicationDepth.Value--;
        }
    }
}
