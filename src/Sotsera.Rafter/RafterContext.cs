using Sotsera.Rafter.Internal;

namespace Sotsera.Rafter;

/// <summary>The invocation's output and cooperative cancellation scope.</summary>
public sealed class RafterContext
{
    private readonly InvocationOutput _output;

    /// <summary>Signals that the invocation should stop and finish its cleanup.</summary>
    public CancellationToken CancellationToken { get; }

    internal string RootDirectory { get; }

    internal RafterContext(string rootDirectory, InvocationOutput output, CancellationToken cancellationToken)
    {
        RootDirectory = rootDirectory;
        _output = output;
        CancellationToken = cancellationToken;
    }

    /// <summary>Writes a literal result line to stdout, preserving embedded newlines and indentation.</summary>
    public void WriteLine(string? text = null) => _output.WriteLine(text);
}
