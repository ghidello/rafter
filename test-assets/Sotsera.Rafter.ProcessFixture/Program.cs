using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Sotsera.Rafter.ProcessFixture;

internal static partial class Program
{
    private const int InvalidArgumentsExitCode = 64;
    private const int StandardErrorHandle = -12;
    private const int StandardOutputHandle = -11;

    private static async Task<int> Main(string[] args)
        => await RunAsync(args).ConfigureAwait(false);

    internal static async Task<int> RunAsync(string[] arguments)
    {
        if (arguments.Length == 0)
        {
            return InvalidArgumentsExitCode;
        }

        try
        {
            return arguments[0] switch
            {
                "inspect" => await InspectAsync(arguments[1..]).ConfigureAwait(false),
                "environment" => await EnvironmentAsync(arguments[1..]).ConfigureAwait(false),
                "working-directory" => await WorkingDirectoryAsync(arguments[1..]).ConfigureAwait(false),
                "json" => await JsonAsync(arguments[1..]).ConfigureAwait(false),
                "emit" => await EmitAsync(arguments[1..]).ConfigureAwait(false),
                "wait" => await WaitAsync(arguments[1..]).ConfigureAwait(false),
                "spawn-child" => await SpawnChildAsync(arguments[1..]).ConfigureAwait(false),
                "retain-pipe" => await RetainPipeAsync(arguments[1..]).ConfigureAwait(false),
                _ => InvalidArgumentsExitCode,
            };
        }
        catch (FixtureArgumentException)
        {
            return InvalidArgumentsExitCode;
        }
    }

    private static async Task<int> InspectAsync(string[] arguments)
    {
        await Console.Out.WriteAsync(JsonSerializer.Serialize(new
        {
            arguments,
            workingDirectory = Directory.GetCurrentDirectory(),
        })).ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> EnvironmentAsync(string[] arguments)
    {
        EnsureEmpty(arguments);
        string[] names = ["CI", "DISCARDED", "EMPTY", "PATH", "RAFTER_CHILD_SECRET", "RAFTER_EXAMPLE_REMOVE"];
        Dictionary<string, string?> values = names.ToDictionary(
            static name => name,
            Environment.GetEnvironmentVariable,
            StringComparer.Ordinal);
        await Console.Out.WriteAsync(JsonSerializer.Serialize(values)).ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> WorkingDirectoryAsync(string[] arguments)
    {
        EnsureEmpty(arguments);
        await Console.Out.WriteAsync(Path.TrimEndingDirectorySeparator(Directory.GetCurrentDirectory()))
            .ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> JsonAsync(string[] arguments)
    {
        EnsureEmpty(arguments);
        await Console.Out.WriteAsync("{\"name\":\"rafter\",\"ready\":true}").ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> EmitAsync(string[] arguments)
    {
        FixtureOptions options = FixtureOptions.Parse(arguments);
        byte[] stdout = options.GetPayload("--stdout", "--stdout-base64");
        byte[] stderr = options.GetPayload("--stderr", "--stderr-base64");
        int repeat = options.GetInt32("--repeat", 1, minimum: 1);
        int chunkBytes = options.GetInt32("--chunk-bytes", 16 * 1024, minimum: 1);
        int delay = options.GetInt32("--delay-ms", 0, minimum: 0);
        int exitCode = options.GetInt32("--exit-code", 0);
        options.EnsureConsumed();

        if (delay != 0)
        {
            await Task.Delay(delay).ConfigureAwait(false);
        }

        await Task.WhenAll(
            WriteAsync(Console.OpenStandardOutput(), stdout, repeat, chunkBytes),
            WriteAsync(Console.OpenStandardError(), stderr, repeat, chunkBytes)).ConfigureAwait(false);
        return exitCode;
    }

    private static async Task<int> WaitAsync(string[] arguments)
    {
        FixtureOptions options = FixtureOptions.Parse(arguments);
        string? controlDirectory = options.GetOptional("--control-directory");
        int? parentProcessId = options.GetOptionalInt32("--parent-process-id", minimum: 1);
        string? closeStream = options.GetOptional("--close-stream");
        if (closeStream is not (null or "stdout" or "stderr"))
        {
            throw new FixtureArgumentException();
        }

        options.EnsureConsumed();
        if (string.Equals(closeStream, "stdout", StringComparison.Ordinal))
        {
            CloseStandardStream(standardError: false);
        }
        else if (string.Equals(closeStream, "stderr", StringComparison.Ordinal))
        {
            CloseStandardStream(standardError: true);
        }

        WriteMetadata(controlDirectory, parentProcessId, "wait", "ready");
        await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> SpawnChildAsync(string[] arguments)
    {
        FixtureOptions options = FixtureOptions.Parse(arguments);
        string? controlDirectory = options.GetOptional("--control-directory");
        int? parentProcessId = options.GetOptionalInt32("--parent-process-id", minimum: 1);
        int depth = options.GetInt32("--depth", 1, minimum: 1);
        options.EnsureConsumed();
        WriteMetadata(controlDirectory, parentProcessId, "spawn-child", "ready");

        using Process child = StartFixture("spawn-child", controlDirectory, depth - 1);
        await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> RetainPipeAsync(string[] arguments)
    {
        FixtureOptions options = FixtureOptions.Parse(arguments);
        string? controlDirectory = options.GetOptional("--control-directory");
        int? parentProcessId = options.GetOptionalInt32("--parent-process-id", minimum: 1);
        string stream = options.GetRequired("--stream");
        if (stream is not ("stdout" or "stderr" or "both"))
        {
            throw new FixtureArgumentException();
        }

        options.EnsureConsumed();
        WriteMetadata(controlDirectory, parentProcessId, "retain-pipe", "ready");
        using Process child = StartFixture("wait", controlDirectory, depth: 0, retainedStream: stream);
        await Task.Yield();
        return 0;
    }

    private static Process StartFixture(
        string verb,
        string? controlDirectory,
        int depth,
        string? retainedStream = null)
    {
        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Fixture path unavailable.");
        ProcessStartInfo startInfo = new(executable)
        {
            UseShellExecute = false,
            // On Unix, isolate the unused pipe before the managed child runtime can duplicate its descriptor.
            RedirectStandardOutput = !OperatingSystem.IsWindows()
                && string.Equals(retainedStream, "stderr", StringComparison.Ordinal),
            RedirectStandardError = !OperatingSystem.IsWindows()
                && string.Equals(retainedStream, "stdout", StringComparison.Ordinal),
        };
        startInfo.ArgumentList.Add(depth > 0 ? verb : "wait");
        if (controlDirectory is not null)
        {
            startInfo.ArgumentList.Add("--control-directory");
            startInfo.ArgumentList.Add(controlDirectory);
        }

        startInfo.ArgumentList.Add("--parent-process-id");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));

        if (depth > 1)
        {
            startInfo.ArgumentList.Add("--depth");
            startInfo.ArgumentList.Add(depth.ToString(CultureInfo.InvariantCulture));
        }

        if (OperatingSystem.IsWindows() && retainedStream is "stdout" or "stderr")
        {
            startInfo.ArgumentList.Add("--close-stream");
            startInfo.ArgumentList.Add(
                string.Equals(retainedStream, "stdout", StringComparison.Ordinal) ? "stderr" : "stdout");
        }

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Fixture child did not start.");
    }

    private static void CloseStandardStream(bool standardError)
    {
        if (OperatingSystem.IsWindows())
        {
            nint handle = GetStdHandle(standardError ? StandardErrorHandle : StandardOutputHandle);
            _ = CloseHandle(handle);
        }
        else
        {
            _ = CloseFileDescriptor(standardError ? 2 : 1);
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    private static partial int CloseFileDescriptor(int fileDescriptor);

    [LibraryImport("kernel32.dll")]
    private static partial nint GetStdHandle(int standardHandle);

    private static async Task WriteAsync(Stream stream, byte[] payload, int repeat, int chunkBytes)
    {
        await using (stream.ConfigureAwait(false))
        {
            for (int repetition = 0; repetition < repeat; repetition++)
            {
                for (int offset = 0; offset < payload.Length; offset += chunkBytes)
                {
                    int count = Math.Min(chunkBytes, payload.Length - offset);
                    await stream.WriteAsync(payload.AsMemory(offset, count)).ConfigureAwait(false);
                }
            }
        }
    }

    private static void WriteMetadata(string? controlDirectory, int? parentProcessId, string verb, string state)
    {
        if (controlDirectory is null)
        {
            return;
        }

        Directory.CreateDirectory(controlDirectory);
        int processId = Environment.ProcessId;
        string destination = Path.Combine(controlDirectory, $"{processId}.json");
        string temporary = Path.Combine(controlDirectory, $".{processId}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temporary, JsonSerializer.Serialize(new
        {
            processId,
            parentProcessId,
            verb,
            state,
        }));
        File.Move(temporary, destination);
    }

    private static void EnsureEmpty(string[] arguments)
    {
        if (arguments.Length != 0)
        {
            throw new FixtureArgumentException();
        }
    }

    private sealed class FixtureOptions
    {
        private readonly Dictionary<string, string> _values;

        private FixtureOptions(Dictionary<string, string> values)
        {
            _values = values;
        }

        internal static FixtureOptions Parse(string[] arguments)
        {
            if (arguments.Length % 2 != 0)
            {
                throw new FixtureArgumentException();
            }

            Dictionary<string, string> values = new(StringComparer.Ordinal);
            for (int index = 0; index < arguments.Length; index += 2)
            {
                if (!arguments[index].StartsWith("--", StringComparison.Ordinal)
                    || !values.TryAdd(arguments[index], arguments[index + 1]))
                {
                    throw new FixtureArgumentException();
                }
            }

            return new FixtureOptions(values);
        }

        internal byte[] GetPayload(string textName, string base64Name)
        {
            string? text = GetOptional(textName);
            string? base64 = GetOptional(base64Name);
            if (text is not null && base64 is not null)
            {
                throw new FixtureArgumentException();
            }

            try
            {
                return base64 is null ? Encoding.UTF8.GetBytes(text ?? string.Empty) : Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                throw new FixtureArgumentException();
            }
        }

        internal int GetInt32(string name, int defaultValue, int? minimum = null)
        {
            string? value = GetOptional(name);
            if (value is null)
            {
                return defaultValue;
            }

            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                || minimum is not null && parsed < minimum)
            {
                throw new FixtureArgumentException();
            }

            return parsed;
        }

        internal int? GetOptionalInt32(string name, int? minimum = null)
        {
            string? value = GetOptional(name);
            if (value is null)
            {
                return null;
            }

            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                || minimum is not null && parsed < minimum)
            {
                throw new FixtureArgumentException();
            }

            return parsed;
        }

        internal string GetRequired(string name)
            => GetOptional(name) ?? throw new FixtureArgumentException();

        internal string? GetOptional(string name)
        {
            if (!_values.Remove(name, out string? value))
            {
                return null;
            }

            return value;
        }

        internal void EnsureConsumed()
        {
            if (_values.Count != 0)
            {
                throw new FixtureArgumentException();
            }
        }
    }

    private sealed class FixtureArgumentException : Exception;
}
