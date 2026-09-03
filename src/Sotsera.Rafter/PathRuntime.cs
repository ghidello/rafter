using System.Collections.Immutable;
using System.Diagnostics;
using static Sotsera.Rafter.BindingEngine;
using static Sotsera.Rafter.CommandModel;

namespace Sotsera.Rafter;

internal static class PathRuntime
{
    internal sealed record InvocationPaths(string Root, ImmutableDictionary<Guid, string> TargetDirectories)
    {
        internal string GetTargetDirectory(Guid targetId)
            => TargetDirectories.TryGetValue(targetId, out string? path)
                ? path
                : throw new InvalidOperationException("The target is missing from the invocation path snapshot.");
    }

    internal static InvocationPaths Resolve(
        CommandDefinition model,
        InvocationSnapshot snapshot,
        InvocationServices services)
    {
        string root = ResolveRoot(model.Root, services);
        ImmutableDictionary<Guid, string>.Builder targets = ImmutableDictionary.CreateBuilder<Guid, string>();
        foreach (TargetDefinition target in model.Targets)
        {
            string workingDirectory = target.WorkingDirectory switch
            {
                null => root,
                LiteralWorkingDirectoryDefinition literal => PathPolicy.ResolveUnderRoot(root, root, literal.Path),
                OptionWorkingDirectoryDefinition option => PathPolicy.ResolveUnderRoot(
                    root,
                    root,
                    snapshot.GetUntyped(option.OptionId) as string
                        ?? throw new PathPolicyException("A working-directory option did not resolve to text.")),
                _ => throw new UnreachableException(),
            };
            targets.Add(target.Id, workingDirectory);
        }

        return new InvocationPaths(root, targets.ToImmutable());
    }

    internal static RafterContext CreateTargetContext(
        InvocationSnapshot snapshot,
        InvocationPaths paths,
        Guid targetId,
        IFileSystemPrimitives fileSystem)
        => new(snapshot, paths.Root, paths.GetTargetDirectory(targetId), fileSystem);

    internal static RafterContext CreateCommandContext(
        InvocationSnapshot snapshot,
        InvocationPaths paths,
        IFileSystemPrimitives fileSystem)
        => new(snapshot, paths.Root, paths.Root, fileSystem);

    private static string ResolveRoot(RootDefinition root, InvocationServices services)
    {
        string resolved = root.Kind switch
        {
            RootKind.Invocation => PathPolicy.NormalizeAbsolute(services.ReadInvocationDirectory()),
            RootKind.Source => ResolveSourceRoot(services.ReadSourceFilePath()),
            RootKind.Explicit => ResolveExplicitRoot(root.Path!, services),
            _ => throw new UnreachableException(),
        };
        FileSystemEntryKind kind = services.FileSystem.GetEntryKind(resolved);
        if (kind is not FileSystemEntryKind.Directory and not FileSystemEntryKind.DirectoryLink)
        {
            throw new PathPolicyException("The command root must be an existing directory.");
        }

        return resolved;
    }

    private static string ResolveSourceRoot(string? sourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath) || !Path.IsPathFullyQualified(sourceFilePath))
        {
            throw new PathPolicyException("Source-root metadata is unavailable for this application.");
        }

        string source = PathPolicy.NormalizeAbsolute(sourceFilePath);
        return Path.GetDirectoryName(source)
            ?? throw new PathPolicyException("Source-root metadata does not identify a containing directory.");
    }

    private static string ResolveExplicitRoot(string path, InvocationServices services)
        => Path.IsPathFullyQualified(path)
            ? PathPolicy.NormalizeAbsolute(path)
            : PathPolicy.NormalizeAbsolute(path, services.ReadInvocationDirectory());
    internal static class PathPolicy
    {
        internal static StringComparer Comparer { get; } = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        internal static string NormalizeAbsolute(string path, string? basePath = null)
        {
            ArgumentNullException.ThrowIfNull(path);
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new PathPolicyException("A path cannot be empty or whitespace.");
            }

            RejectUnsupportedNamespace(path);
            if (Path.IsPathRooted(path) && !Path.IsPathFullyQualified(path))
            {
                throw new PathPolicyException("Drive-relative and partially qualified paths are not supported.");
            }

            try
            {
                string fullPath = basePath is null
                    ? Path.GetFullPath(path)
                    : Path.GetFullPath(path, NormalizeAbsolute(basePath));
                RejectUnsupportedNamespace(fullPath);
                return Path.TrimEndingDirectorySeparator(fullPath);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                throw new PathPolicyException("The path is malformed or unsupported.", exception);
            }
        }

        internal static string ResolveUnderRoot(string root, string workingDirectory, string path)
        {
            string resolved = Path.IsPathFullyQualified(path)
                ? NormalizeAbsolute(path)
                : NormalizeAbsolute(path, workingDirectory);
            EnsureContained(root, resolved);
            return resolved;
        }

        internal static bool Equals(string left, string right) => Comparer.Equals(
            Path.TrimEndingDirectorySeparator(left),
            Path.TrimEndingDirectorySeparator(right));

        internal static void EnsureContained(string root, string path)
        {
            string relative;
            try
            {
                relative = Path.GetRelativePath(root, path);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                throw new PathPolicyException("The path cannot be compared with the command root.", exception);
            }
            if (Path.IsPathRooted(relative)
                || string.Equals(relative, "..", StringComparison.Ordinal)
                || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
            {
                throw new PathPolicyException("The path is outside the command root.");
            }
        }

        private static void RejectUnsupportedNamespace(string path)
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            if (path.StartsWith("\\\\?\\", StringComparison.Ordinal)
                || path.StartsWith("\\\\.\\", StringComparison.Ordinal)
                || path.StartsWith("\\??\\", StringComparison.Ordinal))
            {
                throw new PathPolicyException("Windows extended and device path namespaces are not supported.");
            }
        }
    }

    internal sealed class PathPolicyException : IOException
    {
        internal PathPolicyException(string message)
            : base(message)
        {
        }

        internal PathPolicyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    internal enum FileSystemEntryKind
    {
        Missing,
        File,
        Directory,
        FileLink,
        DirectoryLink,
    }

    internal interface IFileSystemPrimitives
    {
        FileSystemEntryKind GetEntryKind(string path);

        ImmutableArray<FileSystemEntry> Enumerate(string path);

        void CreateDirectory(string path);

        void DeleteFile(string path);

        void DeleteDirectory(string path);
    }

    internal sealed record FileSystemEntry(string Path, FileSystemEntryKind Kind);

    internal sealed class PhysicalFileSystemPrimitives : IFileSystemPrimitives
    {
        internal static PhysicalFileSystemPrimitives Instance { get; } = new();

        private PhysicalFileSystemPrimitives()
        {
        }

        public FileSystemEntryKind GetEntryKind(string path)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                bool isDirectory = (attributes & FileAttributes.Directory) != FileAttributes.None;
                bool isLink = (attributes & FileAttributes.ReparsePoint) != FileAttributes.None;
                return (isDirectory, isLink) switch
                {
                    (true, true) => FileSystemEntryKind.DirectoryLink,
                    (false, true) => FileSystemEntryKind.FileLink,
                    (true, false) => FileSystemEntryKind.Directory,
                    _ => FileSystemEntryKind.File,
                };
            }
            catch (FileNotFoundException)
            {
                return FileSystemEntryKind.Missing;
            }
            catch (DirectoryNotFoundException)
            {
                return FileSystemEntryKind.Missing;
            }
        }

        public ImmutableArray<FileSystemEntry> Enumerate(string path)
            => Directory.EnumerateFileSystemEntries(path)
                .Select(entry => new FileSystemEntry(entry, GetEntryKind(entry)))
                .OrderBy(static entry => entry.Path, PathPolicy.Comparer)
                .ToImmutableArray();

        public void CreateDirectory(string path) => _ = Directory.CreateDirectory(path);

        public void DeleteFile(string path) => File.Delete(path);

        public void DeleteDirectory(string path) => Directory.Delete(path, recursive: false);
    }
}
