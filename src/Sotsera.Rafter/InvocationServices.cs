using System.Reflection;
using System.Runtime.ExceptionServices;
using static Sotsera.Rafter.PathRuntime;

namespace Sotsera.Rafter;

internal sealed record InvocationServices(
    Func<string, string?> ReadEnvironment,
    TextWriter StandardOutput,
    TextWriter StandardError,
    OutputCapabilities StandardOutputCapabilities,
    OutputCapabilities StandardErrorCapabilities,
    string InvocationName)
{
    internal static InvocationServices Capture()
    {
        string? filePath = AppContext.GetData("EntryPointFilePath") as string;
        string? processPath = Environment.ProcessPath;
        string? entryAssembly = Assembly.GetEntryAssembly()?.GetName().Name;
        string? launchToken = Environment.GetCommandLineArgs().FirstOrDefault();
        string invocationName = InvocationNameResolver.Resolve(filePath, processPath, entryAssembly, launchToken);

        (TextWriter Output, TextWriter Error)? coordinated = ConsoleOutputCoordinator.TryGetHostWriters();
        TextWriter output = coordinated?.Output ?? Console.Out;
        TextWriter error = coordinated?.Error ?? Console.Error;
        return new InvocationServices(
            Environment.GetEnvironmentVariable,
            output,
            error,
            // Spectre identifies the physical stream by the current Console writer, even during interception.
            OutputCapabilities.Capture(() => OutputCapabilities.Probe(Console.Out, Console.IsOutputRedirected)),
            OutputCapabilities.Capture(() => OutputCapabilities.Probe(Console.Error, Console.IsErrorRedirected)),
            invocationName)
        {
            ReadSourceFilePath = () => filePath,
        };
    }

    internal Func<string> ReadInvocationDirectory { get; init; } = Directory.GetCurrentDirectory;

    internal Func<string?> ReadSourceFilePath { get; init; }
        = static () => AppContext.GetData("EntryPointFilePath") as string;

    internal IFileSystemPrimitives FileSystem { get; init; } = PhysicalFileSystemPrimitives.Instance;

    internal Action<ExecutionRuntime.TargetNotification>? ExecutionObserver { get; init; }

    internal InvocationServices PreparePresentation(bool plain)
    {
        string? noColor = null;
        ExceptionDispatchInfo? noColorFailure = null;
        try
        {
            noColor = ReadEnvironment("NO_COLOR");
        }
        catch (Exception exception)
        {
            noColorFailure = ExceptionDispatchInfo.Capture(exception);
        }

        bool suppressColor = noColorFailure is not null || !string.IsNullOrEmpty(noColor);
        return this with
        {
            ReadEnvironment = ReadCapturedEnvironment,
            StandardOutputCapabilities = StandardOutputCapabilities.Resolve(plain, suppressColor),
            StandardErrorCapabilities = StandardErrorCapabilities.Resolve(plain, suppressColor),
        };

        string? ReadCapturedEnvironment(string name)
        {
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!string.Equals(name, "NO_COLOR", comparison))
            {
                return ReadEnvironment(name);
            }

            noColorFailure?.Throw();
            return noColor;
        }
    }

    internal static class InvocationNameResolver
    {
        internal static string Resolve(string? filePath, string? processPath, string? entryAssembly, string? launchToken)
        {
            string? fileName = GetStem(filePath);
            if (fileName is not null)
            {
                return fileName;
            }

            string? processName = GetStem(processPath);
            if (processName is not null && !string.Equals(processName, "dotnet", StringComparison.OrdinalIgnoreCase))
            {
                return processName;
            }

            string? assemblyName = GetLeaf(entryAssembly);
            if (assemblyName is not null)
            {
                return assemblyName;
            }

            return GetStem(launchToken) ?? "command";
        }

        private static string? GetStem(string? value)
        {
            string? leaf = GetLeaf(value);
            string? stem = leaf is null ? null : Path.GetFileNameWithoutExtension(leaf);
            return string.IsNullOrWhiteSpace(stem) ? null : stem;
        }

        private static string? GetLeaf(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            try
            {
                string leaf = Path.GetFileName(value);
                return string.IsNullOrWhiteSpace(leaf) ? null : leaf;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
