using System.Diagnostics;
using System.Text.Json;

namespace Rafter.Evaluation;

internal static class BenchmarkProbe
{
    public const string Message = "Cancellation requested.";

    public static int Run(Func<Action> create, bool steady)
    {
        var allocated = GC.GetTotalAllocatedBytes(precise: true);
        var started = Stopwatch.GetTimestamp();
        var render = create();
        render();
        var firstMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var firstBytes = GC.GetTotalAllocatedBytes(precise: true) - allocated;

        var iterations = steady ? 1000 : 0;
        var steadyMilliseconds = 0.0;
        var steadyBytes = 0L;
        if (steady)
        {
            for (var warmup = 0; warmup < 100; warmup++)
            {
                render();
            }

            allocated = GC.GetTotalAllocatedBytes(precise: true);
            started = Stopwatch.GetTimestamp();
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                render();
            }

            steadyMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            steadyBytes = GC.GetTotalAllocatedBytes(precise: true) - allocated;
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            FirstMilliseconds = firstMilliseconds, FirstBytes = firstBytes,
            Iterations = iterations, SteadyMilliseconds = steadyMilliseconds, SteadyBytes = steadyBytes
        }));
        return 0;
    }
}
