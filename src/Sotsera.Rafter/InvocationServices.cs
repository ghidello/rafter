using System.Reflection;
using static Sotsera.Rafter.PathRuntime;

namespace Sotsera.Rafter;

internal sealed record InvocationServices(
    Func<string, string?> ReadEnvironment,
    TextWriter StandardOutput,
    TextWriter StandardError,
    bool StandardOutputSupportsAnsi,
    bool StandardErrorSupportsAnsi,
    string InvocationName)
{
    internal Func<string> ReadInvocationDirectory { get; init; } = Directory.GetCurrentDirectory;

    internal Func<string?> ReadSourceFilePath { get; init; }
        = static () => AppContext.GetData("EntryPointFilePath") as string;

    internal IFileSystemPrimitives FileSystem { get; init; } = PhysicalFileSystemPrimitives.Instance;

    internal static InvocationServices Capture()
    {
        string? filePath = AppContext.GetData("EntryPointFilePath") as string;
        string? processPath = Environment.ProcessPath;
        string? entryAssembly = Assembly.GetEntryAssembly()?.GetName().Name;
        string? launchToken = Environment.GetCommandLineArgs().FirstOrDefault();
        string invocationName = InvocationNameResolver.Resolve(filePath, processPath, entryAssembly, launchToken);

        return new InvocationServices(
            Environment.GetEnvironmentVariable,
            Console.Out,
            Console.Error,
            !Console.IsOutputRedirected,
            !Console.IsErrorRedirected,
            invocationName)
        {
            ReadSourceFilePath = () => filePath,
        };
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
