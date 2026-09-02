using System.Collections.Immutable;
using System.Globalization;
using static Sotsera.Rafter.PathRuntime;

namespace Sotsera.Rafter.Tests;

public sealed class PhaseFourPathTests
{
    [Fact]
    public async Task ResolvesEveryRootPolicyOnceAndKeepsHelpFilesystemFree()
    {
        using TemporaryDirectory temporary = new();
        int invocationReads = 0;
        int sourceReads = 0;
        Command helpCommand = NewCommand(Root.Invocation);
        Target helpEntry = helpCommand.Target("entry").Description("Entry.");
        ConfigureServices(helpCommand, temporary.Path, () =>
        {
            invocationReads++;
            return temporary.Path;
        }, () =>
        {
            sourceReads++;
            return Path.Combine(temporary.Path, "script.cs");
        });

        int helpExit = await helpCommand.RunAsync(helpEntry, ["--help", "--plain"]);

        helpExit.Should().Be(0);
        invocationReads.Should().Be(0);
        sourceReads.Should().Be(0);

        Command invocation = NewCommand(Root.Invocation);
        Target invocationEntry = invocation.Target("entry").Description("Entry.");
        ConfigureServices(invocation, temporary.Path, () =>
        {
            invocationReads++;
            return temporary.Path;
        });
        await FluentActions.Awaiting(() => invocation.RunAsync(invocationEntry, []))
            .Should().ThrowAsync<NotSupportedException>();
        invocation.LastInvocationPaths!.Root.Should().Be(PathPolicy.NormalizeAbsolute(temporary.Path));
        invocationReads.Should().Be(1);

        Command source = NewCommand(Root.Source);
        Target sourceEntry = source.Target("entry").Description("Entry.");
        ConfigureServices(source, temporary.Path, () => temporary.Path, () =>
        {
            sourceReads++;
            return Path.Combine(temporary.Path, "script.cs");
        });
        await FluentActions.Awaiting(() => source.RunAsync(sourceEntry, []))
            .Should().ThrowAsync<NotSupportedException>();
        source.LastInvocationPaths!.Root.Should().Be(PathPolicy.NormalizeAbsolute(temporary.Path));
        sourceReads.Should().Be(1);

        Directory.CreateDirectory(Path.Combine(temporary.Path, "explicit"));
        Command explicitCommand = NewCommand(Root.At("explicit"));
        Target explicitEntry = explicitCommand.Target("entry").Description("Entry.");
        ConfigureServices(explicitCommand, temporary.Path, () => temporary.Path);
        await FluentActions.Awaiting(() => explicitCommand.RunAsync(explicitEntry, []))
            .Should().ThrowAsync<NotSupportedException>();
        explicitCommand.LastInvocationPaths!.Root.Should().Be(
            PathPolicy.NormalizeAbsolute("explicit", temporary.Path));
    }

    [Fact]
    public async Task ResolvesTargetDirectoriesAndCreatesScopedContextsWithoutChangingCurrentDirectory()
    {
        using TemporaryDirectory temporary = new();
        string originalCurrentDirectory = Environment.CurrentDirectory;
        Directory.CreateDirectory(Path.Combine(temporary.Path, "nested"));
        Command command = NewCommand(Root.At(temporary.Path));
        DefaultedOption<string> directory = command.Option<string>("directory")
            .Description("Directory.")
            .Default("nested");
        Target first = command.Target("first").Description("First.").WorkingDirectory(directory);
        Target second = command.Target("second").Description("Second.").WorkingDirectory("other");
        ConfigureServices(command, temporary.Path, () => temporary.Path);

        await FluentActions.Awaiting(() => command.RunAsync(first, []))
            .Should().ThrowAsync<NotSupportedException>();

        PathRuntime.InvocationPaths paths = command.LastInvocationPaths!;
        RafterContext firstContext = PathRuntime.CreateTargetContext(
            command.LastBindingResult!.Snapshot!,
            paths,
            first.Authored.Id,
            PhysicalFileSystemPrimitives.Instance);
        RafterContext secondContext = PathRuntime.CreateTargetContext(
            command.LastBindingResult.Snapshot!,
            paths,
            second.Authored.Id,
            PhysicalFileSystemPrimitives.Instance);
        firstContext.Root.Should().Be(PathPolicy.NormalizeAbsolute(temporary.Path));
        firstContext.WorkingDirectory.Should().Be(PathPolicy.NormalizeAbsolute("nested", temporary.Path));
        secondContext.WorkingDirectory.Should().Be(PathPolicy.NormalizeAbsolute("other", temporary.Path));
        Environment.CurrentDirectory.Should().Be(originalCurrentDirectory);
    }

    [Fact]
    public async Task MissingSourceMetadataIsAPathDiagnosticWhileMetadataFailureIsInfrastructure()
    {
        using TemporaryDirectory temporary = new();
        Command missing = NewCommand(Root.Source);
        Target missingEntry = missing.Target("entry").Description("Entry.");
        StringWriter missingError = new(CultureInfo.InvariantCulture);
        missing.InvocationServicesFactory = () => Services(
            temporary.Path,
            () => temporary.Path,
            () => null,
            PhysicalFileSystemPrimitives.Instance,
            missingError);

        int missingExit = await missing.RunAsync(missingEntry, []);

        missingExit.Should().Be(2);
        missing.LastInvocationStatus.Should().Be(Command.InvocationStatus.PathFailure);
        missingError.ToString().Should().Contain("Source-root metadata is unavailable").And.NotContain("Usage");

        Command failing = NewCommand(Root.Invocation);
        Target failingEntry = failing.Target("entry").Description("Entry.");
        failing.InvocationServicesFactory = () => Services(
            temporary.Path,
            () => throw new IOException("host details"),
            () => null,
            PhysicalFileSystemPrimitives.Instance,
            new StringWriter(CultureInfo.InvariantCulture));

        int failingExit = await failing.RunAsync(failingEntry, []);

        failingExit.Should().Be(1);
        failing.LastInvocationStatus.Should().Be(Command.InvocationStatus.InfrastructureFailure);
    }

    [Fact]
    public void ContainmentRejectsTraversalAndSiblingPrefixTraps()
    {
        using TemporaryDirectory temporary = new();
        string root = Path.Combine(temporary.Path, "root");
        string sibling = Path.Combine(temporary.Path, "root-other");
        Directory.CreateDirectory(root);

        PathPolicy.ResolveUnderRoot(root, root, ".").Should().Be(PathPolicy.NormalizeAbsolute(root));
        PathPolicy.ResolveUnderRoot(root, root, "child/../target").Should().Be(
            PathPolicy.NormalizeAbsolute("target", root));
        Action parentTraversal = () => PathPolicy.ResolveUnderRoot(root, root, "../outside");
        Action siblingPrefix = () => PathPolicy.ResolveUnderRoot(root, root, sibling);

        parentTraversal.Should().Throw<PathPolicyException>();
        siblingPrefix.Should().Throw<PathPolicyException>();
    }

    [Fact]
    public void RejectsMalformedAndUnsupportedPathNamespaces()
    {
        Action empty = () => PathPolicy.NormalizeAbsolute(" ");

        empty.Should().Throw<PathPolicyException>();
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Action driveRelative = () => PathPolicy.NormalizeAbsolute("C:relative");
        Action extended = () => PathPolicy.NormalizeAbsolute("\\\\?\\C:\\root");
        Action device = () => PathPolicy.NormalizeAbsolute("\\\\.\\C:");

        driveRelative.Should().Throw<PathPolicyException>();
        extended.Should().Throw<PathPolicyException>();
        device.Should().Throw<PathPolicyException>();
        PathPolicy.NormalizeAbsolute("\\\\server\\share\\directory")
            .Should().StartWith("\\\\server\\share");
    }

    [Fact]
    public async Task EnsureOperationsUseOptionSnapshotsAndReachTheDocumentedState()
    {
        using TemporaryDirectory temporary = new();
        Command command = NewCommand(Root.At(temporary.Path));
        DefaultedOption<string> output = command.Option<string>("output")
            .Description("Output.")
            .Default("generated");
        Target entry = command.Target("entry").Description("Entry.");
        ConfigureServices(command, temporary.Path, () => temporary.Path);
        await FluentActions.Awaiting(() => command.RunAsync(entry, []))
            .Should().ThrowAsync<NotSupportedException>();
        RafterContext context = PathRuntime.CreateTargetContext(
            command.LastBindingResult!.Snapshot!,
            command.LastInvocationPaths!,
            entry.Authored.Id,
            PhysicalFileSystemPrimitives.Instance);

        context.FileSystem.EnsureDirectory("artifacts/deep");
        context.FileSystem.EnsureDirectory("artifacts/deep");
        context.FileSystem.EnsureDirectory(Path.Combine(temporary.Path, "absolute"));
        context.FileSystem.EnsureDirectory(output);
        string occupied = Path.Combine(temporary.Path, "occupied");
        File.WriteAllText(occupied, "keep");
        Action createOverFile = () => context.FileSystem.EnsureDirectory("occupied");
        createOverFile.Should().Throw<IOException>();
        File.ReadAllText(occupied).Should().Be("keep");
        File.WriteAllText(Path.Combine(temporary.Path, "generated", "file.txt"), "content");
        Directory.CreateDirectory(Path.Combine(temporary.Path, "generated", "nested"));
        File.WriteAllText(Path.Combine(temporary.Path, "generated", "nested", "file.txt"), "content");

        context.FileSystem.EnsureEmptyDirectory(output);
        context.FileSystem.EnsureEmptyDirectory(output);

        Directory.Exists(Path.Combine(temporary.Path, "artifacts", "deep")).Should().BeTrue();
        Directory.Exists(Path.Combine(temporary.Path, "generated")).Should().BeTrue();
        Directory.EnumerateFileSystemEntries(Path.Combine(temporary.Path, "generated")).Should().BeEmpty();

        Command other = NewCommand(Root.At(temporary.Path));
        DefaultedOption<string> foreign = other.Option<string>("foreign").Description("Foreign.").Default("foreign");
        other.Target("entry").Description("Entry.");
        Action useForeign = () => context.FileSystem.EnsureDirectory(foreign);
        useForeign.Should().Throw<InvalidOperationException>().WithMessage("*different command*");
    }

    [Fact]
    public async Task LinkTraversalAndDestructiveProtectedTargetsFailWithoutMutation()
    {
        using TemporaryDirectory temporary = new();
        using TemporaryDirectory external = new();
        string sentinel = Path.Combine(external.Path, "sentinel.txt");
        File.WriteAllText(sentinel, "keep");
        string link = Path.Combine(temporary.Path, "redirect");
        try
        {
            _ = Directory.CreateSymbolicLink(link, external.Path);
        }
        catch (IOException) when (OperatingSystem.IsWindows())
        {
            return;
        }

        Command command = NewCommand(Root.At(temporary.Path));
        Target entry = command.Target("entry").Description("Entry.");
        ConfigureServices(command, temporary.Path, () => temporary.Path);
        await FluentActions.Awaiting(() => command.RunAsync(entry, []))
            .Should().ThrowAsync<NotSupportedException>();
        RafterContext context = PathRuntime.CreateTargetContext(
            command.LastBindingResult!.Snapshot!,
            command.LastInvocationPaths!,
            entry.Authored.Id,
            PhysicalFileSystemPrimitives.Instance);

        Action traverse = () => context.FileSystem.EnsureDirectory("redirect/new");
        Action emptyRoot = () => context.FileSystem.EnsureEmptyDirectory(".");

        traverse.Should().Throw<PathPolicyException>();
        emptyRoot.Should().Throw<PathPolicyException>();
        File.ReadAllText(sentinel).Should().Be("keep");
    }

    [Fact]
    public async Task RevalidationStopsReplacementBeforeDeletion()
    {
        using TemporaryDirectory temporary = new();
        string target = Path.Combine(temporary.Path, "target");
        string file = Path.Combine(target, "file.txt");
        ReplacingFileSystem fileSystem = new(temporary.Path, target, file);
        Command command = NewCommand(Root.At(temporary.Path));
        Target entry = command.Target("entry").Description("Entry.");
        ConfigureServices(command, temporary.Path, () => temporary.Path, fileSystem: fileSystem);
        await FluentActions.Awaiting(() => command.RunAsync(entry, []))
            .Should().ThrowAsync<NotSupportedException>();
        RafterContext context = PathRuntime.CreateTargetContext(
            command.LastBindingResult!.Snapshot!,
            command.LastInvocationPaths!,
            entry.Authored.Id,
            fileSystem);

        Action empty = () => context.FileSystem.EnsureEmptyDirectory("target");

        empty.Should().Throw<PathPolicyException>();
        fileSystem.DeleteCalls.Should().Be(0);
    }

    private static Command NewCommand(Root root)
        => global::Sotsera.Rafter.Rafter.Command(root).Description("Test command.");

    private static void ConfigureServices(
        Command command,
        string invocationName,
        Func<string> invocationDirectory,
        Func<string?>? sourceFile = null,
        IFileSystemPrimitives? fileSystem = null)
    {
        command.InvocationServicesFactory = () => Services(
            invocationName,
            invocationDirectory,
            sourceFile ?? (() => null),
            fileSystem ?? PhysicalFileSystemPrimitives.Instance,
            new StringWriter(CultureInfo.InvariantCulture));
    }

    private static InvocationServices Services(
        string invocationName,
        Func<string> invocationDirectory,
        Func<string?> sourceFile,
        IFileSystemPrimitives fileSystem,
        StringWriter error)
        => new(
            _ => null,
            new StringWriter(CultureInfo.InvariantCulture),
            error,
            false,
            false,
            invocationName)
        {
            ReadInvocationDirectory = invocationDirectory,
            ReadSourceFilePath = sourceFile,
            FileSystem = fileSystem,
        };

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"rafter-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private sealed class ReplacingFileSystem(string root, string target, string file) : IFileSystemPrimitives
    {
        private int _fileReads;

        internal int DeleteCalls { get; private set; }

        public FileSystemEntryKind GetEntryKind(string path)
        {
            if (PathPolicy.Equals(path, root) || PathPolicy.Equals(path, target))
            {
                return FileSystemEntryKind.Directory;
            }

            if (PathPolicy.Equals(path, file))
            {
                _fileReads++;
                return _fileReads == 1 ? FileSystemEntryKind.File : FileSystemEntryKind.DirectoryLink;
            }

            return FileSystemEntryKind.Missing;
        }

        public ImmutableArray<FileSystemEntry> Enumerate(string path)
            => [new FileSystemEntry(file, GetEntryKind(file))];

        public void CreateDirectory(string path)
        {
        }

        public void DeleteFile(string path) => DeleteCalls++;

        public void DeleteDirectory(string path) => DeleteCalls++;
    }
}
