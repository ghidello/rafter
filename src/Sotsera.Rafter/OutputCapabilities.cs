using Spectre.Console;

namespace Sotsera.Rafter;

internal sealed record OutputCapabilities(
    bool IsRedirected,
    bool SupportsStaticLayout,
    bool SupportsAnsi,
    bool SupportsColor,
    bool SupportsUnicode,
    bool SupportsCursor,
    int? Width)
{
    internal static OutputCapabilities Capture(Func<OutputCapabilities> probe)
    {
        try
        {
            return probe();
        }
        catch
        {
            // Cosmetic detection must not turn an otherwise usable writer into an invocation failure.
            return Plain;
        }
    }

    internal static OutputCapabilities Probe(TextWriter writer, bool redirected)
    {
        if (redirected)
        {
            return Plain with { IsRedirected = true };
        }

        AnsiCapabilities ansi = AnsiCapabilities.Create(writer, new AnsiWriterSettings
        {
            Ansi = AnsiSupport.Detect,
            // Color policy belongs to the invocation; avoid Spectre's NO_COLOR/FORCE_COLOR detection.
            ColorSystem = ColorSystemSupport.Standard,
        });
        bool unicode = writer.Encoding.CodePage is 65001 or 1200 or 1201 or 12000 or 12001;
        return new OutputCapabilities(false, true, ansi.Ansi, ansi.Ansi, unicode, ansi.AlternateBuffer,
            Console.WindowWidth);
    }

    internal static OutputCapabilities Plain { get; } = new(false, false, false, false, false, false, null);

    internal bool IsRich => !IsRedirected && SupportsStaticLayout && Width is >= 20 and <= 4096;

    internal OutputCapabilities Resolve(bool plain, bool suppressColor)
        => plain || !IsRich
            ? Plain
            : this with { SupportsColor = SupportsColor && SupportsAnsi && !suppressColor };
}
