using static Sotsera.Rafter.CommandModel;

namespace Sotsera.Rafter;

/// <summary>Describes how a command chooses its root directory.</summary>
public sealed class Root
{
    private Root(RootKind kind, string? path)
    {
        Kind = kind;
        Path = path;
    }

    /// <summary>Uses an explicitly supplied directory.</summary>
    /// <param name="path">The root path.</param>
    /// <returns>An explicit root policy.</returns>
    public static Root At(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return new Root(RootKind.Explicit, path);
    }

    /// <summary>Uses the directory from which the command was invoked.</summary>
    public static Root Invocation { get; } = new(RootKind.Invocation, null);

    /// <summary>Uses the directory containing the file-based application.</summary>
    public static Root Source { get; } = new(RootKind.Source, null);

    internal RootKind Kind { get; }

    internal string? Path { get; }
}
