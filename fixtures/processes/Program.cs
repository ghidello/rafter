using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using Sotsera.Rafter;

namespace ProcessProbe;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.InputEncoding = Encoding.UTF8;
        var runtime = args[0];
        var mode = args[1];
        switch (mode)
        {
            case "owned-host":
                var command = Rafter.Command("Own a long-running fixture child.");
                var result = await command.RunAsync(["--plain"], async context =>
                {
                    await context.Process("dotnet").Arguments("exec", "--runtimeconfig", runtime,
                        Assembly.GetExecutingAssembly().Location, runtime, "hold", args[2]).RunAsync();
                });
                if (args.Length > 3)
                {
                    File.WriteAllText(args[3], result.ToString());
                }
                return result;
            case "headless-host":
                // Some hosts attach to their parent's console during startup despite CREATE_NO_WINDOW.
                // Detach only this fixture process, leaving redirected streams and the test runner untouched.
                if (!FreeConsole())
                {
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
                }
                if (GetConsoleProcessList(new uint[1], 1) != 0)
                {
                    throw new InvalidOperationException("The headless fixture unexpectedly has a console.");
                }
                return await Rafter.Command("Clean up without a console.").RunAsync(["--quiet"], async context =>
                {
                    await context.Process("dotnet").Arguments("exec", "--runtimeconfig", runtime,
                        Assembly.GetExecutingAssembly().Location, runtime, "tree", args[2]).RunAsync();
                });
            case "breakaway":
                return Breakaway.TryLaunch() ? 91 : 0;
            case "echo":
                Console.Write(JsonSerializer.Serialize(args.Skip(2).ToArray()));
                return 0;
            case "exit":
                return int.Parse(args[2]);
            case "environment":
                Console.Write(JsonSerializer.Serialize(new[] { Directory.GetCurrentDirectory(),
                    Environment.GetEnvironmentVariable(args[2]) }));
                return 0;
            case "output":
                Console.Write("  first\n\nlast");
                Console.Error.Write("  error\npartial");
                return args.Length > 2 ? int.Parse(args[2]) : 0;
            case "large-output":
                for (var index = 0; index < 10000; index++)
                {
                    Console.Write("abcdefghij");
                    Console.Error.Write("0123456789");
                }
                return 0;
            case "output-hold":
                Console.Write("child output");
                goto case "hold";
            case "hold":
                Console.CancelKeyPress += (_, signal) => { signal.Cancel = true; };
                File.WriteAllText(args[2], Environment.ProcessId.ToString());
                await Task.Delay(Timeout.InfiniteTimeSpan);
                return 0;
            case "graceful":
                var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Console.CancelKeyPress += (_, signal) =>
                {
                    signal.Cancel = true;
                    Console.Write("graceful stop");
                    stopped.TrySetResult();
                };
                File.WriteAllText(args[2], Environment.ProcessId.ToString());
                await stopped.Task;
                return 0;
            case "tree":
                using (var child = Child(runtime, "hold", args[2]))
                {
                    var deadline = Stopwatch.StartNew();
                    while (!File.Exists(args[2]) && deadline.Elapsed < TimeSpan.FromSeconds(10))
                    {
                        await Task.Delay(10);
                    }
                    if (!File.Exists(args[2]))
                    {
                        return 90;
                    }
                    Console.Write("parent finished");
                    return 0;
                }
            default:
                throw new ArgumentException("Unknown probe mode.");
        }
    }

    private static Process Child(string runtime, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        foreach (var argument in new[] { "exec", "--runtimeconfig", runtime, Assembly.GetExecutingAssembly().Location,
            runtime }.Concat(arguments))
        {
            start.ArgumentList.Add(argument);
        }
        return Process.Start(start)!;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetConsoleProcessList([Out] uint[] processes, uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();
}
