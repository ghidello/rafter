using Spectre.Console;

namespace Rafter.Evaluation;

internal static class AnsiProbe
{
    public static int Run(string[] args)
    {
        return BenchmarkProbe.Run(() =>
        {
            var writer = new AnsiWriter(TextWriter.Null,
                new AnsiCapabilities { Ansi = true, ColorSystem = ColorSystem.TrueColor });
            return () => writer.WriteLine(BenchmarkProbe.Message, new Style(Color.Yellow));
        }, args.Contains("--steady"));
    }
}
