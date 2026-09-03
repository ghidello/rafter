using System.Collections.Immutable;
using static Sotsera.Rafter.CommandModel;
using static Sotsera.Rafter.PathRuntime;

namespace Sotsera.Rafter;

/// <summary>Provides filesystem operations scoped to a Rafter invocation.</summary>
public sealed class RafterFileSystem
{
    private readonly Guid _commandId;
    private readonly BindingEngine.InvocationSnapshot _snapshot;
    private readonly string _root;
    private readonly string _workingDirectory;
    private readonly IFileSystemPrimitives _fileSystem;

    internal RafterFileSystem(
        Guid commandId,
        BindingEngine.InvocationSnapshot snapshot,
        string root,
        string workingDirectory,
        IFileSystemPrimitives fileSystem)
    {
        _commandId = commandId;
        _snapshot = snapshot;
        _root = root;
        _workingDirectory = workingDirectory;
        _fileSystem = fileSystem;
    }

    /// <summary>Creates a directory and any missing parents when they do not already exist.</summary>
    public void EnsureDirectory(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        string resolved = Resolve(path);
        ValidateExistingPath(resolved);
        _fileSystem.CreateDirectory(resolved);
    }

    /// <summary>Creates the directory selected by a required option when it does not already exist.</summary>
    public void EnsureDirectory(RequiredOption<string> option) => EnsureDirectory(ResolveOption(option?.Authored));

    /// <summary>Creates the directory selected by a defaulted option when it does not already exist.</summary>
    public void EnsureDirectory(DefaultedOption<string> option) => EnsureDirectory(ResolveOption(option?.Authored));

    /// <summary>Creates a directory or removes all of its existing contents without following links.</summary>
    public void EnsureEmptyDirectory(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        string resolved = Resolve(path);
        if (PathPolicy.Equals(resolved, Path.GetPathRoot(resolved)!)
            || PathPolicy.Equals(resolved, _root)
            || PathPolicy.Equals(resolved, _workingDirectory))
        {
            throw new PathPolicyException("The requested directory is protected from destructive cleanup.");
        }

        ValidateExistingPath(resolved);
        FileSystemEntryKind kind = _fileSystem.GetEntryKind(resolved);
        if (kind == FileSystemEntryKind.Missing)
        {
            _fileSystem.CreateDirectory(resolved);
            return;
        }

        if (kind != FileSystemEntryKind.Directory)
        {
            throw new PathPolicyException("The requested path is not an ordinary directory.");
        }

        DirectoryPlan plan = InspectDirectory(resolved);
        Empty(plan);
    }

    /// <summary>Empties the directory selected by a required option without following links.</summary>
    public void EnsureEmptyDirectory(RequiredOption<string> option)
        => EnsureEmptyDirectory(ResolveOption(option?.Authored));

    /// <summary>Empties the directory selected by a defaulted option without following links.</summary>
    public void EnsureEmptyDirectory(DefaultedOption<string> option)
        => EnsureEmptyDirectory(ResolveOption(option?.Authored));

    private string Resolve(string path) => PathPolicy.ResolveUnderRoot(_root, _workingDirectory, path);

    private string ResolveOption(AuthoredOption? option)
    {
        ArgumentNullException.ThrowIfNull(option);
        if (option.Command.Id != _commandId)
        {
            throw new InvalidOperationException("The option belongs to a different command.");
        }

        return _snapshot.GetUntyped(option.Id) as string
            ?? throw new InvalidOperationException("The working-directory option did not resolve to text.");
    }

    private void ValidateExistingPath(string target)
    {
        string relative = Path.GetRelativePath(_root, target);
        if (string.Equals(relative, ".", StringComparison.Ordinal))
        {
            return;
        }

        string current = _root;
        foreach (string component in Split(relative))
        {
            current = Path.Combine(current, component);
            FileSystemEntryKind kind = _fileSystem.GetEntryKind(current);
            if (kind == FileSystemEntryKind.Missing)
            {
                return;
            }

            if (kind is FileSystemEntryKind.FileLink or FileSystemEntryKind.DirectoryLink)
            {
                throw new PathPolicyException("A path component redirects through a link or reparse point.");
            }

            if (kind == FileSystemEntryKind.File && !PathPolicy.Equals(current, target))
            {
                throw new PathPolicyException("A parent path component is not a directory.");
            }
        }
    }

    private DirectoryPlan InspectDirectory(string path)
    {
        ImmutableArray<FileSystemEntry>.Builder entries = ImmutableArray.CreateBuilder<FileSystemEntry>();
        ImmutableDictionary<string, DirectoryPlan>.Builder children = ImmutableDictionary.CreateBuilder<string, DirectoryPlan>(
            PathPolicy.Comparer);
        foreach (FileSystemEntry entry in _fileSystem.Enumerate(path))
        {
            if (entry.Kind == FileSystemEntryKind.Missing)
            {
                throw new PathPolicyException("The directory changed while it was being inspected.");
            }

            entries.Add(entry);
            if (entry.Kind == FileSystemEntryKind.Directory)
            {
                children.Add(entry.Path, InspectDirectory(entry.Path));
            }
        }

        return new DirectoryPlan(path, entries.ToImmutable(), children.ToImmutable());
    }

    private void Empty(DirectoryPlan plan)
    {
        RequireKind(plan.Path, FileSystemEntryKind.Directory);
        foreach (FileSystemEntry entry in plan.Entries)
        {
            RequireKind(entry.Path, entry.Kind);
            switch (entry.Kind)
            {
                case FileSystemEntryKind.Directory:
                    DirectoryPlan child = plan.Children[entry.Path];
                    Empty(child);
                    RequireKind(entry.Path, FileSystemEntryKind.Directory);
                    _fileSystem.DeleteDirectory(entry.Path);
                    break;
                case FileSystemEntryKind.DirectoryLink:
                    _fileSystem.DeleteDirectory(entry.Path);
                    break;
                case FileSystemEntryKind.File:
                case FileSystemEntryKind.FileLink:
                    _fileSystem.DeleteFile(entry.Path);
                    break;
                default:
                    throw new PathPolicyException("The directory changed after preflight.");
            }
        }
    }

    private void RequireKind(string path, FileSystemEntryKind expected)
    {
        if (_fileSystem.GetEntryKind(path) != expected)
        {
            throw new PathPolicyException("The directory changed after preflight.");
        }
    }

    private static string[] Split(string relative)
        => relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

    private sealed record DirectoryPlan(
        string Path,
        ImmutableArray<FileSystemEntry> Entries,
        ImmutableDictionary<string, DirectoryPlan> Children);
}
