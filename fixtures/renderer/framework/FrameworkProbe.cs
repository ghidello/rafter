namespace Rafter.Evaluation;

internal static class FrameworkProbe
{
    public static int Run(string[] args)
    {
        return BenchmarkProbe.Run(() => () => TextWriter.Null.WriteLine(BenchmarkProbe.Message),
            args.Contains("--steady"));
    }
}
