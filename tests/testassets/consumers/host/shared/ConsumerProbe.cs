using System;
using System.Runtime.InteropServices;
using System.Text.Json;

// This is a harness fixture, not the proposed Rafter runtime API.
internal static class ConsumerProbe
{
    public static int Run(string[] args)
    {
        if (args.Length == 1 && args[0] == "--fail")
        {
            Console.Out.WriteLine("output before failure");
            Console.Error.WriteLine("intentional consumer failure");
            return 23;
        }

        Console.Out.WriteLine(JsonSerializer.Serialize(new
        {
            Arguments = args,
            WorkingDirectory = Environment.CurrentDirectory,
            Framework = RuntimeInformation.FrameworkDescription
        }));

        return 0;
    }
}
