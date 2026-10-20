using Sotsera.Rafter.Internal;

namespace Sotsera.Rafter;

/// <summary>The invocation's prepared input values, output, and cooperative cancellation scope.</summary>
public sealed class RafterContext
{
    private readonly InvocationOutput _output;
    private readonly InputValues _values;

    /// <summary>Signals that the invocation should stop and finish its cleanup.</summary>
    public CancellationToken CancellationToken { get; }

    internal string RootDirectory { get; }

    internal RafterContext(string rootDirectory, InvocationOutput output, CancellationToken cancellationToken,
        InputValues values)
    {
        RootDirectory = rootDirectory;
        _output = output;
        _values = values;
        CancellationToken = cancellationToken;
    }

    /// <summary>Writes a literal result line to stdout, preserving embedded newlines and indentation.</summary>
    public void WriteLine(string? text = null) => _output.WriteLine(text);

    /// <summary>Returns the value prepared before execution for a required or defaulted input.</summary>
    public T GetValue<T>(ValueOption<T> option) => _values.GetRequired(option);

    /// <summary>Returns the supplied string, or null when the optional input was omitted.</summary>
    public string? GetValue(Option<string> option) => _values.GetString(option);

    /// <summary>Returns the supplied value, or null when the optional input was omitted.</summary>
    public T? GetValue<T>(Option<T> option) where T : struct => _values.GetOptional(option);
}
